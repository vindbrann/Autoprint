using Autoprint.Service.Services;
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

            var serverLogger = loggerFactory.CreateLogger<NamedPipeServer>();
            var engineLogger = loggerFactory.CreateLogger<PrinterEngine>();
            var engine = new PrinterEngine(engineLogger);
            _pipeServer = new NamedPipeServer(serverLogger, engine);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("🚀 Autoprint Service démarré.");
            await _pipeServer.StartListeningAsync(stoppingToken);
        }
    }
}