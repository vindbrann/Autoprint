using Autoprint.Service.Services;
// Assure-toi d'avoir ces usings pour le logger
using Microsoft.Extensions.Logging;

namespace Autoprint.Service
{
    public class Worker : BackgroundService
    {
        private readonly ILogger<Worker> _logger;
        private readonly NamedPipeServer _pipeServer;

        public Worker(ILogger<Worker> logger, ILoggerFactory loggerFactory)
        {
            _logger = logger;

            // 1. Création des loggers spécifiques
            var serverLogger = loggerFactory.CreateLogger<NamedPipeServer>();
            var engineLogger = loggerFactory.CreateLogger<PrinterEngine>();

            // 2. Création du Moteur (Engine)
            var engine = new PrinterEngine(engineLogger);

            // 3. Création du Serveur avec le Moteur injecté
            _pipeServer = new NamedPipeServer(serverLogger, engine);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("🚀 Autoprint Service démarré.");
            await _pipeServer.StartListeningAsync(stoppingToken);
        }
    }
}