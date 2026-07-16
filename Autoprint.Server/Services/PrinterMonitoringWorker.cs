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
        private DateTime? _lastArchivingSweep;

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

                            // Toujours exécuter la vérification des rapports planifiés
                            await ExecuterRapportsPlanifiesAsync(scope, stoppingToken);
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

            if (_lastArchivingSweep == null || DateTime.UtcNow.Date > _lastArchivingSweep.Value.Date)
            {
                await ExecuterNettoyageAutoAsync(context, stoppingToken);
                _lastArchivingSweep = DateTime.UtcNow;
            }

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

        private async Task ExecuterNettoyageAutoAsync(ApplicationDbContext context, CancellationToken stoppingToken)
        {
            try
            {
                _logger.LogInformation("--> Exécution de la tâche d'archivage automatique...");
                var limitDate = DateTime.UtcNow.AddDays(-30);

                var printersToArchive = await context.Imprimantes
                    .Where(i => !i.IsArchived && i.LastSeen != null && i.LastSeen < limitDate)
                    .ToListAsync(stoppingToken);

                if (printersToArchive.Any())
                {
                    foreach (var printer in printersToArchive)
                    {
                        printer.IsArchived = true;
                        _logger.LogInformation($"Archivage automatique de l'imprimante : {printer.NomAffiche} (Dernière présence : {printer.LastSeen})");
                    }
                    await context.SaveChangesAsync(stoppingToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur lors de l'archivage automatique des imprimantes inactives.");
            }
        }

        private async Task ExecuterRapportsPlanifiesAsync(IServiceScope scope, CancellationToken stoppingToken)
        {
            try
            {
                var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var reportService = scope.ServiceProvider.GetRequiredService<IReportGeneratorService>();
                var emailService = scope.ServiceProvider.GetRequiredService<IEmailService>();

                var now = DateTime.UtcNow;
                var dueSchedules = await context.ReportSchedules
                    .Where(s => !s.EstSupprime && s.IsActive && (s.NextRunAt == null || s.NextRunAt <= now))
                    .ToListAsync(stoppingToken);

                if (!dueSchedules.Any()) return;

                _logger.LogInformation($"--> {dueSchedules.Count} rapport(s) planifié(s) à générer et envoyer.");

                foreach (var schedule in dueSchedules)
                {
                    if (stoppingToken.IsCancellationRequested) break;

                    try
                    {
                        _logger.LogInformation($"Génération du rapport : {schedule.ReportName} ({schedule.Format}) pour {schedule.EmailRecipients}...");

                        byte[] fileBytes;
                        string fileName;
                        string contentType;

                        if (schedule.Format.Equals("CSV", StringComparison.OrdinalIgnoreCase))
                        {
                            fileBytes = await reportService.GenerateCsvReportAsync(schedule);
                            fileName = $"Rapport_{schedule.ReportName.Replace(" ", "_")}_{DateTime.Now:yyyyMMdd}.csv";
                            contentType = "text/csv";
                        }
                        else
                        {
                            fileBytes = await reportService.GeneratePdfReportAsync(schedule);
                            fileName = $"Rapport_{schedule.ReportName.Replace(" ", "_")}_{DateTime.Now:yyyyMMdd}.pdf";
                            contentType = "application/pdf";
                        }

                        var recipients = schedule.EmailRecipients.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries);
                        foreach (var recipient in recipients)
                        {
                            var email = recipient.Trim();
                            if (string.IsNullOrEmpty(email)) continue;

                            await emailService.SendEmailWithAttachmentAsync(
                                email,
                                $"[Autoprint] Rapport planifié : {schedule.ReportName}",
                                $"<p>Bonjour,</p><p>Veuillez trouver ci-joint le rapport d'activité planifié <strong>{schedule.ReportName}</strong> généré automatiquement le {DateTime.Now:dd/MM/yyyy à HH:mm}.</p><p>Cordialement,<br/>L'équipe Autoprint</p>",
                                fileBytes,
                                fileName,
                                contentType
                            );
                        }

                        schedule.LastRunAt = now;
                        schedule.NextRunAt = CalculateNextRun(schedule, now);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, $"Erreur lors de la génération/envoi du rapport planifié '{schedule.ReportName}'");
                    }
                }

                await context.SaveChangesAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur dans la tâche de vérification des rapports planifiés.");
            }
        }

        private DateTime CalculateNextRun(ReportSchedule schedule, DateTime baseTime)
        {
            var localToday = DateTime.Today;
            var targetHour = schedule.RunHour;
            var targetMinute = schedule.RunMinute;

            if (targetHour < 0 || targetHour > 23) targetHour = 6;
            if (targetMinute < 0 || targetMinute > 59) targetMinute = 0;

            var localNow = baseTime.ToLocalTime();

            if (schedule.Frequency.Equals("Quotidien", StringComparison.OrdinalIgnoreCase))
            {
                var nextRun = localToday.AddHours(targetHour).AddMinutes(targetMinute);
                if (nextRun <= localNow)
                {
                    nextRun = nextRun.AddDays(1);
                }
                return nextRun.ToUniversalTime();
            }
            else if (schedule.Frequency.Equals("Hebdomadaire", StringComparison.OrdinalIgnoreCase))
            {
                var targetDayOfWeek = schedule.RunDayOfWeek.HasValue ? (DayOfWeek)schedule.RunDayOfWeek.Value : DayOfWeek.Monday;
                
                var nextRun = localToday.AddHours(targetHour).AddMinutes(targetMinute);
                int daysToAdd = ((int)targetDayOfWeek - (int)localToday.DayOfWeek + 7) % 7;
                nextRun = nextRun.AddDays(daysToAdd);
                
                if (nextRun <= localNow)
                {
                    nextRun = nextRun.AddDays(7);
                }
                return nextRun.ToUniversalTime();
            }
            else if (schedule.Frequency.Equals("Mensuel", StringComparison.OrdinalIgnoreCase))
            {
                var targetDayOfMonth = schedule.RunDayOfMonth.HasValue ? schedule.RunDayOfMonth.Value : 1;
                if (targetDayOfMonth < 1 || targetDayOfMonth > 31) targetDayOfMonth = 1;

                var currentYear = localToday.Year;
                var currentMonth = localToday.Month;
                
                int daysInMonth = DateTime.DaysInMonth(currentYear, currentMonth);
                int actualDay = Math.Min(targetDayOfMonth, daysInMonth);

                var nextRun = new DateTime(currentYear, currentMonth, actualDay, targetHour, targetMinute, 0);

                if (nextRun <= localNow)
                {
                    var nextMonthDate = localToday.AddMonths(1);
                    int daysInNextMonth = DateTime.DaysInMonth(nextMonthDate.Year, nextMonthDate.Month);
                    actualDay = Math.Min(targetDayOfMonth, daysInNextMonth);
                    nextRun = new DateTime(nextMonthDate.Year, nextMonthDate.Month, actualDay, targetHour, targetMinute, 0);
                }
                return nextRun.ToUniversalTime();
            }

            return baseTime.AddDays(1);
        }
    }
}
