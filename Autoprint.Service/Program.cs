using Autoprint.Service;

var builder = Host.CreateApplicationBuilder(args);

// 1. Configuration critique pour un Service Windows
builder.Services.AddWindowsService(options =>
{
    options.ServiceName = "AutoprintService";
});

// 2. Enregistrement du Worker principal (le cœur du service)
builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();