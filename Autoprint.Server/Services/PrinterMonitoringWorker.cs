using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Autoprint.Server.Data;
using Autoprint.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Autoprint.Server.Services
{
    public class PrinterMonitoringWorker : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<PrinterMonitoringWorker> _logger;

        public PrinterMonitoringWorker(IServiceProvider serviceProvider, ILogger<PrinterMonitoringWorker> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("--> Démon de Supervision des Imprimantes démarré.");

            // Pause initiale au démarrage pour laisser le serveur s'initialiser complètement
            await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                int intervalMinutes = 60;
                bool enabled = true;

                try
                {
                    using (var scope = _serviceProvider.CreateScope())
                    {
                        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                        var settings = await context.ServerSettings.AsNoTracking().ToListAsync(stoppingToken);

                        var enabledStr = settings.FirstOrDefault(s => s.Key == "Monitoring_Enabled")?.Value;
                        if (!string.IsNullOrEmpty(enabledStr)) bool.TryParse(enabledStr, out enabled);

                        var intervalStr = settings.FirstOrDefault(s => s.Key == "Monitoring_IntervalMinutes")?.Value;
                        if (!string.IsNullOrEmpty(intervalStr)) int.TryParse(intervalStr, out intervalMinutes);

                        if (intervalMinutes < 1) intervalMinutes = 1;

                        if (enabled)
                        {
                            int startHour = 8;
                            int endHour = 18;
                            bool scanOnWeekends = true;

                            var startStr = settings.FirstOrDefault(s => s.Key == "Monitoring_StartHour")?.Value;
                            if (!string.IsNullOrEmpty(startStr)) int.TryParse(startStr, out startHour);

                            var endStr = settings.FirstOrDefault(s => s.Key == "Monitoring_EndHour")?.Value;
                            if (!string.IsNullOrEmpty(endStr)) int.TryParse(endStr, out endHour);

                            var weekendStr = settings.FirstOrDefault(s => s.Key == "Monitoring_ScanOnWeekends")?.Value;
                            if (!string.IsNullOrEmpty(weekendStr)) bool.TryParse(weekendStr, out scanOnWeekends);

                            var now = DateTime.Now;
                            bool isWeekend = now.DayOfWeek == DayOfWeek.Saturday || now.DayOfWeek == DayOfWeek.Sunday;
                            bool insideTimeWindow = now.Hour >= startHour && now.Hour < endHour;

                            if (insideTimeWindow && (!isWeekend || scanOnWeekends))
                            {
                                _logger.LogInformation("--> Démarrage du scan de supervision des imprimantes...");
                                await ExecuterScanSupervisionAsync(scope, stoppingToken);
                            }
                            else
                            {
                                _logger.LogInformation("--> Scan en pause (Mode Nuit ou Hors Horaires de travail).");
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Erreur dans le cycle du démon de supervision.");
                }

                await Task.Delay(TimeSpan.FromMinutes(intervalMinutes), stoppingToken);
            }
        }

        private async Task ExecuterScanSupervisionAsync(IServiceScope scope, CancellationToken stoppingToken)
        {
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var snmpService = scope.ServiceProvider.GetRequiredService<ISnmpService>();

            var activePrinters = await context.Imprimantes
                .Include(i => i.Modele)
                .ThenInclude(m => m.SnmpProfile)
                .Where(i => !i.IsArchived)
                .ToListAsync(stoppingToken);

            foreach (var printer in activePrinters)
            {
                if (stoppingToken.IsCancellationRequested) break;

                try
                {
                    _logger.LogInformation($"Supervision: Interrogation de {printer.NomAffiche} ({printer.AdresseIp})...");

                    var diagnostic = await snmpService.GetPrinterDiagnosticAsync(
                        printer.AdresseIp,
                        printer.SnmpPort,
                        printer.SnmpCommunity ?? "public",
                        printer.SnmpVersion,
                        printer.Modele?.SnmpProfile
                    );

                    if (!diagnostic.PingSuccess)
                    {
                        printer.MonitoringStatus = "Offline";
                    }
                    else
                    {
                        printer.LastSeen = DateTime.UtcNow;

                        bool tonerLow = false;
                        if (diagnostic.Toners != null)
                        {
                            var todayUtc = DateTime.UtcNow.Date;
                            foreach (var toner in diagnostic.Toners)
                            {
                                if (toner.CurrentLevel >= 0)
                                {
                                    if (toner.CurrentLevel <= 10)
                                    {
                                        tonerLow = true;
                                    }

                                    bool alreadyLoggedToday = await context.TonerHistories
                                        .AnyAsync(h => h.ImprimanteId == printer.Id 
                                                       && h.ComponentColor == toner.Color 
                                                       && h.RecordedAt >= todayUtc, 
                                                  stoppingToken);

                                    if (!alreadyLoggedToday)
                                    {
                                        context.TonerHistories.Add(new TonerHistory
                                        {
                                            ImprimanteId = printer.Id,
                                            ComponentColor = toner.Color,
                                            LevelPercent = toner.CurrentLevel,
                                            RecordedAt = DateTime.UtcNow
                                        });
                                    }
                                }
                            }
                        }

                        if (diagnostic.Status.Contains("Alerte") || diagnostic.Status.Contains("Inconnu"))
                        {
                            printer.MonitoringStatus = "Warning";
                        }
                        else if (tonerLow)
                        {
                            printer.MonitoringStatus = "Warning";
                        }
                        else if (diagnostic.Alerts.Any(a => a.ToLower().Contains("erreur") || a.ToLower().Contains("bourrage") || a.ToLower().Contains("ouvert")))
                        {
                            printer.MonitoringStatus = "Critical";
                        }
                        else
                        {
                            printer.MonitoringStatus = "Ok";
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Erreur lors de la supervision de {printer.NomAffiche}");
                }
            }

            await context.SaveChangesAsync(stoppingToken);
            _logger.LogInformation("--> Fin du scan de supervision. Statuts sauvegardés en base.");
        }
    }
}
