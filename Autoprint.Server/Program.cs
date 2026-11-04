using System.Text;
using Autoprint.Server.Data;
using Autoprint.Server.Hubs;
using Autoprint.Server.Services;
using Autoprint.Web.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.IdentityModel.Tokens;

// PDFsharp 6 (build "Core") : utilise les polices installées sous Windows pour les rapports PDF
PdfSharp.Fonts.GlobalFontSettings.UseWindowsFontsUnderWindows = true;

var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    Args = args,
    ContentRootPath = AppContext.BaseDirectory
});

var dbProvider = builder.Configuration["Database:Provider"] ?? "SqlServer";
var connectionStrings = builder.Configuration.GetSection("Database:ConnectionStrings");

builder.Services.AddDbContext<ApplicationDbContext>(options =>
{
    if (dbProvider.Equals("Sqlite", StringComparison.OrdinalIgnoreCase))
    {
        var connectionString = connectionStrings["Sqlite"] ?? "Data Source=Autoprint.db";
        if (connectionString.StartsWith("Data Source=", StringComparison.OrdinalIgnoreCase))
        {
            var dbFile = connectionString.Substring("Data Source=".Length).Trim();
            if (!Path.IsPathRooted(dbFile))
            {
                connectionString = $"Data Source={Path.Combine(AppContext.BaseDirectory, dbFile)}";
            }
        }
        options.UseSqlite(connectionString);
    }
    else
    {
        var connectionString = connectionStrings["SqlServer"];
        options.UseSqlServer(connectionString, sqlOptions =>
        {
            sqlOptions.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery);
        });
    }
});

builder.Services.AddScoped<IPrintSpoolerService, WindowsPrintSpoolerService>();
builder.Services.AddScoped<IDriverService, DriverService>();
builder.Services.AddScoped<INamingService, NamingService>();
builder.Services.AddScoped<Autoprint.Server.Services.IAuthService, Autoprint.Server.Services.AuthService>();
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddScoped<Autoprint.Server.Services.IReportGeneratorService, Autoprint.Server.Services.ReportGeneratorService>();
builder.Services.AddScoped<ISyncSpoolerService, SyncSpoolerService>();
builder.Services.AddScoped<Autoprint.Server.Services.ISnmpService, Autoprint.Server.Services.SnmpService>();
builder.Services.AddScoped<Autoprint.Server.Services.IPredictiveService, Autoprint.Server.Services.PredictiveService>();
builder.Services.AddSignalR();
builder.Services.AddScoped<AuditService>();
builder.Services.AddScoped<Autoprint.Server.Services.DiscoveryService>();
builder.Services.AddHostedService<Autoprint.Server.Services.DiscoveryWorker>();

builder.Services.AddHostedService<LogCleanupWorker>();

// Chiffrement des secrets en base (audit #9) : clés Data Protection stockées hors du site web et protégées par DPAPI machine.
// ⚠️ Sans ce dossier, les secrets chiffrés (mots de passe AD / SMTP, clé agent) sont perdus : il fait partie de la sauvegarde du serveur.
var keysPath = builder.Configuration["DataProtection:KeysPath"]
    ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Autoprint", "Keys");
Directory.CreateDirectory(keysPath);
var dataProtection = builder.Services.AddDataProtection()
    .SetApplicationName("Autoprint")
    .PersistKeysToFileSystem(new DirectoryInfo(keysPath));
if (OperatingSystem.IsWindows())
{
    dataProtection.ProtectKeysWithDpapi(protectToLocalMachine: true);
}
builder.Services.AddSingleton<SecretProtector>();

// Anti brute-force (CWE-307) : 10 tentatives de connexion par minute et par IP, plus un délai par compte
builder.Services.AddSingleton<LoginThrottleService>();
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = async (ctx, token) =>
        await ctx.HttpContext.Response.WriteAsync("Trop de tentatives de connexion. Réessayez dans une minute.", token);
    options.AddPolicy("login", httpContext =>
        System.Threading.RateLimiting.RateLimitPartition.GetFixedWindowLimiter(
            httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new System.Threading.RateLimiting.FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            }));
});
builder.Services.AddHostedService<Autoprint.Server.Services.PrinterMonitoringWorker>();

var jwtKey = builder.Configuration["Jwt:Key"] ?? throw new InvalidOperationException("Clé JWT introuvable !");
if (jwtKey == "CeciEstUneCleSecreteTresLonguePourLaSecuriteDeAutoprint_ChangeMoi_En_Prod!" && !builder.Environment.IsDevelopment())
{
    throw new InvalidOperationException("CRITICAL SECURITY ERROR: The default JWT development key cannot be used in production. The server installer must generate a random key, or it must be overridden via environment variables.");
}
var key = Encoding.ASCII.GetBytes(jwtKey);

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.RequireHttpsMetadata = false;
    options.SaveToken = true;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(key),
        ValidateIssuer = true,
        ValidIssuer = builder.Configuration["Jwt:Issuer"],
        ValidateAudience = true,
        ValidAudience = builder.Configuration["Jwt:Audience"],
        ValidateLifetime = true,
        ClockSkew = TimeSpan.Zero
    };

    // Révocation des sessions (audit #10) : compte désactivé / supprimé ou empreinte de sécurité changée
    // (mot de passe, statut, rôles) => le jeton est refusé. Résultat mis en cache 30 s par utilisateur.
    options.Events = new JwtBearerEvents
    {
        OnTokenValidated = async context =>
        {
            var principal = context.Principal;
            if (!int.TryParse(principal?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value, out int userId))
            {
                context.Fail("Jeton sans identifiant utilisateur.");
                return;
            }

            // Clé de cache = utilisateur + empreinte du jeton : un nouveau jeton (nouvelle empreinte) est vérifié tout de suite,
            // un ancien jeton révoqué l'est au plus tard 30 s après le changement
            var tokenStamp = principal!.FindFirst("sstamp")?.Value ?? "";
            var cache = context.HttpContext.RequestServices.GetRequiredService<IMemoryCache>();
            var isValid = await cache.GetOrCreateAsync($"user-session-{userId}-{tokenStamp}", async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(30);
                var db = context.HttpContext.RequestServices.GetRequiredService<ApplicationDbContext>();
                var state = await db.Users.AsNoTracking()
                    .Where(u => u.Id == userId)
                    .Select(u => new { u.IsActive, Stamp = u.SecurityStamp ?? "" })
                    .FirstOrDefaultAsync();
                return state != null && state.IsActive && state.Stamp == tokenStamp;
            });

            if (!isValid)
            {
                context.Fail("Session révoquée.");
            }
        }
    };
});
builder.Services.AddMemoryCache();

builder.Services.AddControllersWithViews()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
        options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
    });
builder.Services.AddRazorPages();
builder.Services.AddEndpointsApiExplorer();

builder.Services.AddOpenApi();

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowBlazorOrigin", policy =>
    {
        policy.WithOrigins("https://localhost:7159", "http://localhost:5139")
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials();
    });
});

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("ADMIN_ACCESS", policy => policy.RequireClaim("Permission", "ADMIN_ACCESS"));
    options.AddPolicy("PRINTER_READ", policy => policy.RequireClaim("Permission", "PRINTER_READ"));
    options.AddPolicy("PRINTER_WRITE", policy => policy.RequireClaim("Permission", "PRINTER_WRITE"));
    options.AddPolicy("PRINTER_DELETE", policy => policy.RequireClaim("Permission", "PRINTER_DELETE"));
    options.AddPolicy("PRINTER_SYNC", policy => policy.RequireClaim("Permission", "PRINTER_SYNC"));
    options.AddPolicy("LOCATION_READ", policy => policy.RequireClaim("Permission", "LOCATION_READ"));
    options.AddPolicy("LOCATION_WRITE", policy => policy.RequireClaim("Permission", "LOCATION_WRITE"));
    options.AddPolicy("LOCATION_DELETE", policy => policy.RequireClaim("Permission", "LOCATION_DELETE"));
    options.AddPolicy("BRAND_READ", policy => policy.RequireClaim("Permission", "BRAND_READ"));
    options.AddPolicy("BRAND_WRITE", policy => policy.RequireClaim("Permission", "BRAND_WRITE"));
    options.AddPolicy("BRAND_DELETE", policy => policy.RequireClaim("Permission", "BRAND_DELETE"));
    options.AddPolicy("MODEL_READ", policy => policy.RequireClaim("Permission", "MODEL_READ"));
    options.AddPolicy("MODEL_WRITE", policy => policy.RequireClaim("Permission", "MODEL_WRITE"));
    options.AddPolicy("MODEL_DELETE", policy => policy.RequireClaim("Permission", "MODEL_DELETE"));
    options.AddPolicy("DRIVER_READ", policy => policy.RequireClaim("Permission", "DRIVER_READ"));
    options.AddPolicy("DRIVER_SCAN", policy => policy.RequireClaim("Permission", "DRIVER_SCAN"));
    options.AddPolicy("USER_READ", policy => policy.RequireClaim("Permission", "USER_READ"));
    options.AddPolicy("USER_WRITE", policy => policy.RequireClaim("Permission", "USER_WRITE"));
    options.AddPolicy("USER_DELETE", policy => policy.RequireClaim("Permission", "USER_DELETE"));
    options.AddPolicy("ROLE_READ", policy => policy.RequireClaim("Permission", "ROLE_READ"));
    options.AddPolicy("ROLE_WRITE", policy => policy.RequireClaim("Permission", "ROLE_WRITE"));
    options.AddPolicy("ROLE_DELETE", policy => policy.RequireClaim("Permission", "ROLE_DELETE"));
    options.AddPolicy("SETTINGS_MANAGE", policy => policy.RequireClaim("Permission", "SETTINGS_MANAGE"));
    options.AddPolicy("AUDIT_READ", policy => policy.RequireClaim("Permission", "AUDIT_READ"));
    options.AddPolicy("SNMP_PROFILE_READ", policy => policy.RequireClaim("Permission", "SNMP_PROFILE_READ"));
    options.AddPolicy("SNMP_PROFILE_WRITE", policy => policy.RequireClaim("Permission", "SNMP_PROFILE_WRITE"));
    options.AddPolicy("SNMP_PROFILE_DELETE", policy => policy.RequireClaim("Permission", "SNMP_PROFILE_DELETE"));
    options.AddPolicy("REPORT_MANAGE", policy => policy.RequireClaim("Permission", "REPORT_MANAGE"));
    options.AddPolicy("PRINTER_ARCHIVE", policy => policy.RequireClaim("Permission", "PRINTER_ARCHIVE"));
    options.AddPolicy("NETWORK_SCAN", policy => policy.RequireClaim("Permission", "NETWORK_SCAN"));
});

var app = builder.Build();

if (args.Contains("--migrate-only"))
{
    Console.WriteLine("[MIGRATION_START]");
    using (var scope = app.Services.CreateScope())
    {
        try
        {
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var config = scope.ServiceProvider.GetRequiredService<IConfiguration>();
            var currentProvider = config["Database:Provider"];

            try
            {
                context.Database.Migrate();
            }
            catch
            {
                context.Database.EnsureCreated();
            }
            DbInitializer.Initialize(context);
            scope.ServiceProvider.GetRequiredService<SecretProtector>().EncryptPlaintextSecrets(context);

            Console.WriteLine("[MIGRATION_SUCCESS]");
            Environment.Exit(0);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[MIGRATION_ERROR] {ex.Message}");
            if (ex.InnerException != null)
            {
                Console.WriteLine($"[INTERNAL_ERROR] {ex.InnerException.Message}");
            }
            Environment.Exit(1);
        }
    }
}

if (app.Environment.IsDevelopment())
{
    app.UseWebAssemblyDebugging();
    app.MapOpenApi();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/openapi/v1.json", "Autoprint API");
    });
}

if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}

// En-têtes de sécurité (audit #12). La console n'est jamais affichée dans un iframe.
// CSP : Blazor WebAssembly exige 'wasm-unsafe-eval' ; Radzen utilise des styles en ligne ; Bootstrap / Chart.js viennent de jsDelivr (versions figées + SRI).
const string ContentSecurityPolicy =
    "default-src 'self'; " +
    "script-src 'self' 'wasm-unsafe-eval' https://cdn.jsdelivr.net; " +
    "style-src 'self' 'unsafe-inline' https://cdn.jsdelivr.net; " +
    "font-src 'self' data: https://cdn.jsdelivr.net; " +
    "img-src 'self' data: blob:; " +
    "connect-src 'self'; " +
    "frame-ancestors 'none'; " +
    "base-uri 'self'; " +
    "form-action 'self'; " +
    "object-src 'none'";

app.Use(async (context, next) =>
{
    var headers = context.Response.Headers;
    headers["X-Content-Type-Options"] = "nosniff";
    headers["X-Frame-Options"] = "DENY";
    headers["Referrer-Policy"] = "no-referrer";
    headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";
    // Swagger UI (développement uniquement) utilise des scripts en ligne
    if (!(app.Environment.IsDevelopment() && context.Request.Path.StartsWithSegments("/swagger")))
    {
        headers["Content-Security-Policy"] = ContentSecurityPolicy;
    }
    await next();
});

app.UseHttpsRedirection();
app.UseBlazorFrameworkFiles();
app.UseStaticFiles();
app.UseCors("AllowBlazorOrigin");
app.UseRateLimiter();

app.UseAuthentication();

// Mot de passe expiré / à changer : seul le changement de mot de passe est autorisé côté API (CWE-602)
app.Use(async (context, next) =>
{
    if (context.User.HasClaim("ForcePasswordChange", "true"))
    {
        var path = context.Request.Path;
        bool allowed = path.StartsWithSegments("/api/users/change-password", StringComparison.OrdinalIgnoreCase)
                    || path.StartsWithSegments("/api/users/profile", StringComparison.OrdinalIgnoreCase)
                    || path.StartsWithSegments("/api/auth", StringComparison.OrdinalIgnoreCase);

        if (!allowed && (path.StartsWithSegments("/api") || path.StartsWithSegments("/hubs")))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsync("Changement de mot de passe obligatoire avant toute autre action.");
            return;
        }
    }
    await next();
});

app.UseAuthorization();

app.MapRazorPages();
app.MapControllers();
app.MapHub<EventsHub>("/hubs/events");
app.MapFallbackToFile("index.html");

using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var context = services.GetRequiredService<ApplicationDbContext>();
        var config = services.GetRequiredService<IConfiguration>();
        var currentProvider = config["Database:Provider"];

        try
        {
            context.Database.Migrate();
        }
        catch
        {
            context.Database.EnsureCreated();
        }

        DbInitializer.Initialize(context);

        int encrypted = services.GetRequiredService<SecretProtector>().EncryptPlaintextSecrets(context);
        if (encrypted > 0) services.GetRequiredService<ILogger<Program>>().LogInformation("{Count} secret(s) chiffré(s) en base.", encrypted);
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "Erreur critique lors de l'initialisation de la Base de Données.");
    }
}

if (args.Contains("--migrate-only"))
{
    return;
}

if (args.Contains("--reset-admin"))
{
    Console.ForegroundColor = ConsoleColor.Yellow;
    Console.WriteLine("--- MODE RESET ADMIN ---");
    using (var scope = app.Services.CreateScope())
    {
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var admin = context.Users.FirstOrDefault(u => u.Username == "admin");
        if (admin != null)
        {
            // Mot de passe temporaire aléatoire, affiché une seule fois (audit #13)
            const string alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnpqrstuvwxyz23456789";
            var tempPassword = new string(Enumerable.Range(0, 16)
                .Select(_ => alphabet[System.Security.Cryptography.RandomNumberGenerator.GetInt32(alphabet.Length)])
                .ToArray());

            admin.PasswordHash = Autoprint.Server.Helpers.SecurityHelper.HashPassword(tempPassword);
            admin.ForceChangePassword = true;
            admin.IsActive = true;
            admin.RenewSecurityStamp();
            context.SaveChanges();
            Console.WriteLine($"SUCCES : Mot de passe temporaire de 'admin' : {tempPassword}");
            Console.WriteLine("Il devra etre change a la premiere connexion. Il ne sera plus affiche.");
        }
        else Console.WriteLine("ERREUR : Admin introuvable.");
    }
    return;
}

app.Run();

