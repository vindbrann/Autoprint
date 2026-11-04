using Autoprint.Spooler;
using Autoprint.SpoolerService;

// Service Windows « AutoprintSpooler » (serveur uniquement) : seul composant d'Autoprint Server exécuté en LocalSystem.
// Il réalise les 5 opérations d'administration du spouleur pour le site web, qui tourne sans privilège (audit #8).
var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddWindowsService(options => options.ServiceName = "AutoprintSpooler");
builder.Services.AddSingleton<ISpoolerAdmin, SpoolerAdmin>();
builder.Services.AddHostedService<PipeServerWorker>();

builder.Build().Run();
