using Autoprint.Service;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddWindowsService(options =>
{
    options.ServiceName = "AutoprintService";
});

builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();