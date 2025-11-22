using System.Text;
using Autoprint.Server.Data;
using Autoprint.Server.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
// AUCUN using Microsoft.OpenApi ici !

var builder = WebApplication.CreateBuilder(args);

// 1. Base de Données
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(connectionString, sqlOptions =>
    {
        sqlOptions.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery);
    })
);

// 2. Services

if (OperatingSystem.IsWindows())
    builder.Services.AddScoped<Autoprint.Server.Services.IPrintSpoolerService, Autoprint.Server.Services.WindowsPrintSpoolerService>();
else
    builder.Services.AddScoped<Autoprint.Server.Services.IPrintSpoolerService, Autoprint.Server.Services.StubPrintSpoolerService>();

builder.Services.AddScoped<IDriverService, DriverService>();
builder.Services.AddScoped<INamingService, NamingService>();
builder.Services.AddScoped<Autoprint.Server.Services.IAuthService, Autoprint.Server.Services.AuthService>();
builder.Services.AddScoped<Autoprint.Server.Services.IEmailService, Autoprint.Server.Services.EmailService>();

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

builder.Services.AddControllersWithViews();
builder.Services.AddRazorPages();
builder.Services.AddEndpointsApiExplorer();

// 4. OpenAPI Standard (Sans configuration complexe)
builder.Services.AddOpenApi(); // Version minimale qui marche à tous les coups

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowBlazorOrigin", policy =>
    {
        policy.WithOrigins("https://localhost:7169", "http://localhost:5139")
              .AllowAnyMethod()
              .AllowAnyHeader();
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
    options.AddPolicy("DRIVER_WRITE", policy => policy.RequireClaim("Permission", "DRIVER_WRITE"));
    options.AddPolicy("DRIVER_DELETE", policy => policy.RequireClaim("Permission", "DRIVER_DELETE"));
    options.AddPolicy("DRIVER_INSTALL", policy => policy.RequireClaim("Permission", "DRIVER_INSTALL"));
    options.AddPolicy("DRIVER_UNINSTALL", policy => policy.RequireClaim("Permission", "DRIVER_UNINSTALL"));

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
});

var app = builder.Build();

// Pipeline HTTP
if (app.Environment.IsDevelopment())
{
    app.UseWebAssemblyDebugging();

    // Génération du JSON
    app.MapOpenApi();

    // Interface Swagger UI
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/openapi/v1.json", "Autoprint API");
    });
}

app.UseHttpsRedirection();

// Hébergement Blazor
app.UseBlazorFrameworkFiles();
app.UseStaticFiles();

app.UseCors("AllowBlazorOrigin");

app.UseAuthentication();
app.UseAuthorization();

app.MapRazorPages();
app.MapControllers();
app.MapFallbackToFile("index.html");

app.Run();