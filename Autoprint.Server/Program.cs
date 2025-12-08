using System.Text;
using Autoprint.Server.Data;
using Autoprint.Server.Hubs;
using Autoprint.Server.Services;
using Autoprint.Web.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

// 1. Base de Données (Support Hybride SQLite / SQL Server)
var dbProvider = builder.Configuration["Database:Provider"] ?? "SqlServer";
var connectionStrings = builder.Configuration.GetSection("Database:ConnectionStrings");

builder.Services.AddDbContext<ApplicationDbContext>(options =>
{
    if (dbProvider.Equals("Sqlite", StringComparison.OrdinalIgnoreCase))
    {
        // MODE LIGHT (SQLite)
        var connectionString = connectionStrings["Sqlite"] ?? "Data Source=Autoprint.db";
        options.UseSqlite(connectionString);
    }
    else
    {
        // MODE ENTREPRISE (SQL Server)
        var connectionString = connectionStrings["SqlServer"];
        options.UseSqlServer(connectionString, sqlOptions =>
        {
            // On garde le SplitQuery pour SQL Server comme dans ton code original
            sqlOptions.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery);
        });
    }
});

// 2. Services
builder.Services.AddScoped<IPrintSpoolerService, WindowsPrintSpoolerService>();
builder.Services.AddScoped<IDriverService, DriverService>();
builder.Services.AddScoped<INamingService, NamingService>();
builder.Services.AddScoped<Autoprint.Server.Services.IAuthService, Autoprint.Server.Services.AuthService>();
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddScoped<ISyncSpoolerService, SyncSpoolerService>();
builder.Services.AddSignalR();
builder.Services.AddScoped<AuditService>();


// --- AJOUT : Worker de nettoyage des logs (Cron) ---
builder.Services.AddHostedService<LogCleanupWorker>();

// 3. Auth JWT
var jwtKey = builder.Configuration["Jwt:Key"] ?? throw new InvalidOperationException("Clé JWT introuvable !");
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
});

builder.Services.AddControllersWithViews()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
        options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
    });
builder.Services.AddRazorPages();
builder.Services.AddEndpointsApiExplorer();

// 4. OpenAPI Standard
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
    // Administration
    options.AddPolicy("ADMIN_ACCESS", policy => policy.RequireClaim("Permission", "ADMIN_ACCESS"));

    // Imprimantes
    options.AddPolicy("PRINTER_READ", policy => policy.RequireClaim("Permission", "PRINTER_READ"));
    options.AddPolicy("PRINTER_WRITE", policy => policy.RequireClaim("Permission", "PRINTER_WRITE"));
    options.AddPolicy("PRINTER_DELETE", policy => policy.RequireClaim("Permission", "PRINTER_DELETE"));
    options.AddPolicy("PRINTER_SYNC", policy => policy.RequireClaim("Permission", "PRINTER_SYNC"));

    // Lieux
    options.AddPolicy("LOCATION_READ", policy => policy.RequireClaim("Permission", "LOCATION_READ"));
    options.AddPolicy("LOCATION_WRITE", policy => policy.RequireClaim("Permission", "LOCATION_WRITE"));
    options.AddPolicy("LOCATION_DELETE", policy => policy.RequireClaim("Permission", "LOCATION_DELETE"));

    // Marques
    options.AddPolicy("BRAND_READ", policy => policy.RequireClaim("Permission", "BRAND_READ"));
    options.AddPolicy("BRAND_WRITE", policy => policy.RequireClaim("Permission", "BRAND_WRITE"));
    options.AddPolicy("BRAND_DELETE", policy => policy.RequireClaim("Permission", "BRAND_DELETE"));

    // Modèles
    options.AddPolicy("MODEL_READ", policy => policy.RequireClaim("Permission", "MODEL_READ"));
    options.AddPolicy("MODEL_WRITE", policy => policy.RequireClaim("Permission", "MODEL_WRITE"));
    options.AddPolicy("MODEL_DELETE", policy => policy.RequireClaim("Permission", "MODEL_DELETE"));

    // Pilotes
    options.AddPolicy("DRIVER_READ", policy => policy.RequireClaim("Permission", "DRIVER_READ"));
    options.AddPolicy("DRIVER_SCAN", policy => policy.RequireClaim("Permission", "DRIVER_SCAN"));

    // Utilisateurs
    options.AddPolicy("USER_READ", policy => policy.RequireClaim("Permission", "USER_READ"));
    options.AddPolicy("USER_WRITE", policy => policy.RequireClaim("Permission", "USER_WRITE"));
    options.AddPolicy("USER_DELETE", policy => policy.RequireClaim("Permission", "USER_DELETE"));

    // Rôles
    options.AddPolicy("ROLE_READ", policy => policy.RequireClaim("Permission", "ROLE_READ"));
    options.AddPolicy("ROLE_WRITE", policy => policy.RequireClaim("Permission", "ROLE_WRITE"));
    options.AddPolicy("ROLE_DELETE", policy => policy.RequireClaim("Permission", "ROLE_DELETE"));

    // Système
    options.AddPolicy("SETTINGS_MANAGE", policy => policy.RequireClaim("Permission", "SETTINGS_MANAGE"));

    // --- AJOUT : Audit (Logs) ---
    options.AddPolicy("AUDIT_READ", policy => policy.RequireClaim("Permission", "AUDIT_READ"));
});

var app = builder.Build();

// Pipeline HTTP
if (app.Environment.IsDevelopment())
{
    app.UseWebAssemblyDebugging();
    app.MapOpenApi();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/openapi/v1.json", "Autoprint API");
    });
}

app.UseHttpsRedirection();
app.UseBlazorFrameworkFiles();
app.UseStaticFiles();
app.UseCors("AllowBlazorOrigin");

app.UseAuthentication();
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

        // Logique de création de base selon le moteur
        if (currentProvider != null && currentProvider.Equals("Sqlite", StringComparison.OrdinalIgnoreCase))
        {
            // SQLite : Création simple si le fichier n'existe pas
            context.Database.EnsureCreated();
        }
        else
        {
            // SQL Server : Migration robuste
            context.Database.Migrate();
        }

        // Initialisation des données (Admin par défaut, etc.)
        DbInitializer.Initialize(context);
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "Erreur critique lors de l'initialisation de la Base de Données.");
    }
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
            admin.PasswordHash = Autoprint.Server.Helpers.SecurityHelper.ComputeSha256Hash("admin123");
            admin.ForceChangePassword = true;
            admin.IsActive = true;
            context.SaveChanges();
            Console.WriteLine("SUCCES : Mot de passe reinitialise a 'admin123'.");
        }
        else Console.WriteLine("ERREUR : Admin introuvable.");
    }
    return; // Stop l'application ici
}

app.Run();