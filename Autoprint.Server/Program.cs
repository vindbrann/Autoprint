using System.Text;
using Autoprint.Server.Data;
using Autoprint.Server.Hubs;
using Autoprint.Server.Services;
using Autoprint.Web.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

var dbProvider = builder.Configuration["Database:Provider"] ?? "SqlServer";
var connectionStrings = builder.Configuration.GetSection("Database:ConnectionStrings");

builder.Services.AddDbContext<ApplicationDbContext>(options =>
{
    if (dbProvider.Equals("Sqlite", StringComparison.OrdinalIgnoreCase))
    {
        var connectionString = connectionStrings["Sqlite"] ?? "Data Source=Autoprint.db";
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
builder.Services.AddScoped<ISyncSpoolerService, SyncSpoolerService>();
builder.Services.AddSignalR();
builder.Services.AddScoped<AuditService>();
builder.Services.AddScoped<Autoprint.Server.Services.DiscoveryService>();
builder.Services.AddHostedService<Autoprint.Server.Services.DiscoveryWorker>();

builder.Services.AddHostedService<LogCleanupWorker>();

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
});

var app = builder.Build();

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

        if (currentProvider != null && currentProvider.Equals("Sqlite", StringComparison.OrdinalIgnoreCase))
        {
            context.Database.EnsureCreated();
        }
        else
        {
            context.Database.Migrate();
        }

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
    return;
}

app.Run();