using Microsoft.EntityFrameworkCore;
using Autoprint.Server.Data;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(connectionString));

builder.Services.AddScoped<Autoprint.Server.Services.IFileService, Autoprint.Server.Services.LocalFileService>();

//Service pour gérer le Spouleur Windows (Ports, Imprimantes)
// Attention : Ce service nécessitera que l'application tourne avec des droits suffisants
// Injection conditionnelle du Spouleur
if (OperatingSystem.IsWindows())
{
    // Sur Windows : On utilise le vrai service qui touche au système
    builder.Services.AddScoped<Autoprint.Server.Services.IPrintSpoolerService, Autoprint.Server.Services.WindowsPrintSpoolerService>();
}
else
{
    // Sur Linux/Mac ou autres : On utilise le bouchon pour ne pas planter
    builder.Services.AddScoped<Autoprint.Server.Services.IPrintSpoolerService, Autoprint.Server.Services.StubPrintSpoolerService>();
}

// Service d'envoi d'emails (basé sur la config BDD)
builder.Services.AddScoped<Autoprint.Server.Services.IEmailService, Autoprint.Server.Services.SmtpEmailService>();

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.UseStaticFiles();

app.MapControllers();

app.Run();
