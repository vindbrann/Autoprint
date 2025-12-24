using Autoprint.Server.Data;
using Microsoft.EntityFrameworkCore;

namespace Autoprint.Server.Services
{
    public class LogCleanupWorker : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<LogCleanupWorker> _logger;

        public LogCleanupWorker(IServiceProvider serviceProvider, ILogger<LogCleanupWorker> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("LogCleanupWorker démarré.");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using (var scope = _serviceProvider.CreateScope())
                    {
                        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

                        int retentionDays = 180; 
                        bool purgeEnabled = true;

                        var setting = await context.ServerSettings.FindAsync("LogRetentionDays");

                        if (setting != null && int.TryParse(setting.Value, out int days))
                        {
                            if (days == 0)
                            {
                                purgeEnabled = false;
                            }
                            else if (days > 0)
                            {
                                retentionDays = days;
                            }
                        }

                        if (!purgeEnabled)
                        {
                            _logger.LogInformation("Nettoyage des logs : DÉSACTIVÉ (Configuration = 0).");
                        }
                        else
                        {
                            var limitDate = DateTime.UtcNow.AddDays(-retentionDays);

                            int deletedCount = await context.AuditLogs
                                .Where(l => l.DateAction < limitDate)
                                .ExecuteDeleteAsync(stoppingToken);

                            if (deletedCount > 0)
                            {
                                _logger.LogInformation($"Nettoyage Logs : {deletedCount} entrées supprimées (> {retentionDays} jours).");
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Erreur lors du nettoyage automatique des logs.");
                }

                await Task.Delay(TimeSpan.FromHours(24), stoppingToken);
            }
        }
    }
}