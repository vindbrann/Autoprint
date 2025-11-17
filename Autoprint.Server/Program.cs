using System.Text;
using Autoprint.Server.Data;
using Autoprint.Server.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection; // Important pour les extensions
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;


var builder = WebApplication.CreateBuilder(args);

// 1. Configurer la Base de Données
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(connectionString));

// 2. Injection des Services
builder.Services.AddScoped<Autoprint.Server.Services.IFileService, Autoprint.Server.Services.LocalFileService>();

if (OperatingSystem.IsWindows())
{
    builder.Services.AddScoped<Autoprint.Server.Services.IPrintSpoolerService, Autoprint.Server.Services.WindowsPrintSpoolerService>();
}
else
{
    builder.Services.AddScoped<Autoprint.Server.Services.IPrintSpoolerService, Autoprint.Server.Services.StubPrintSpoolerService>();
}

builder.Services.AddScoped<ISettingsService, SettingsService>();
builder.Services.AddScoped<IDriverService, DriverService>();
builder.Services.AddScoped<INamingService, NamingService>();
builder.Services.AddScoped<Autoprint.Server.Services.IAuthService, Autoprint.Server.Services.AuthService>();
builder.Services.AddScoped<Autoprint.Server.Services.IEmailService, Autoprint.Server.Services.SmtpEmailService>();

// --- SÉCURITÉ JWT ---
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

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

// --- CONFIGURATION SWAGGER V10 (CORRIGÉE) ---
builder.Services.AddSwaggerGen(options =>
{
    // 1. Info API
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Autoprint API",
        Version = "v1",
        Description = "API de gestion d'impression centralisée"
    });

    // 2. Définition de la Sécurité
    // Cela suffit pour faire apparaître le bouton "Authorize" en haut à droite !
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Description = "Entrez votre token JWT",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT"
    });

    // 3. REQUIREMENT : SUPPRIMÉ TEMPORAIREMENT
    // Le bloc qui causait l'erreur "Reference not found" a été retiré.
    // Conséquence : Tu devras cliquer sur "Authorize" manuellement dans Swagger,
    // mais tout fonctionnera techniquement.
});

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowBlazorOrigin", policy =>
    {
        policy.WithOrigins("https://localhost:7169")
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

var app = builder.Build();

// Pipeline HTTP
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseCors("AllowBlazorOrigin");

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();