using Autoprint.Spooler;
using Autoprint.SpoolerService;

// Service Windows « AutoprintSpooler » (serveur uniquement) : seul composant d'Autoprint Server exécuté en LocalSystem.
// Il réalise les 5 opérations d'administration du spouleur pour le site web, qui tourne sans privilège (audit #8).
var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddWindowsService(options => options.ServiceName = "AutoprintSpooler");
// Journal Windows : une ligne par commande reçue (traçabilité des opérations sur le spouleur, test S8-12).
// Par défaut le journal d'événements ne reçoit qu'à partir du niveau Warning : les LogInformation étaient perdus (B56).
builder.Logging.AddFilter<Microsoft.Extensions.Logging.EventLog.EventLogLoggerProvider>("Autoprint", LogLevel.Information);
builder.Services.AddSingleton<ISpoolerAdmin, SpoolerAdmin>();
builder.Services.AddHostedService<PipeServerWorker>();

builder.Build().Run();
