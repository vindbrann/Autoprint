using Autoprint.Server.Data;
using Autoprint.Shared;
using Microsoft.EntityFrameworkCore;

namespace Autoprint.Server.Services
{
    public class DiscoveryWorker : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<DiscoveryWorker> _logger;

        public DiscoveryWorker(IServiceProvider serviceProvider, ILogger<DiscoveryWorker> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("DiscoveryWorker démarré.");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var delay = await CalculateDelayToNextRun();
                    if (delay.TotalMinutes > 5)
                        _logger.LogInformation($"Prochain scan dans {delay.TotalHours:F1} heures.");

                    await Task.Delay(delay, stoppingToken);

                    using (var scope = _serviceProvider.CreateScope())
                    {
                        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                        var service = scope.ServiceProvider.GetRequiredService<DiscoveryService>();

                        var profiles = await db.DiscoveryProfiles.Where(p => p.IsEnabled).ToListAsync();
                        foreach (var profile in profiles)
                        {
                            if (IsProfileDue(profile))
                            {
                                await service.ExecuteScanAsync(profile.Id);
                                profile.LastRunDate = DateTime.Now;
                            }
                        }
                        await db.SaveChangesAsync();
                    }
                }
                catch (TaskCanceledException) { break; }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Erreur DiscoveryWorker.");
                    await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
                }
            }
        }

        private async Task<TimeSpan> CalculateDelayToNextRun()
        {
            using var scope = _serviceProvider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var profiles = await db.DiscoveryProfiles.Where(p => p.IsEnabled).ToListAsync();

            if (!profiles.Any()) return TimeSpan.FromHours(1);

            var now = DateTime.Now;
            DateTime? nextRunGlobal = null;

            foreach (var p in profiles)
            {
                var nextRun = GetNextOccurrence(now, p.ScheduleHour, p.ScheduleDays);
                if (nextRunGlobal == null || nextRun < nextRunGlobal) nextRunGlobal = nextRun;
            }

            if (nextRunGlobal.HasValue)
            {
                var delay = nextRunGlobal.Value - now;
                return delay.TotalMilliseconds > 0 ? delay.Add(TimeSpan.FromSeconds(5)) : TimeSpan.FromSeconds(30);
            }
            return TimeSpan.FromHours(1);
        }

        private DateTime GetNextOccurrence(DateTime now, int targetHour, ScanDays days)
        {
            for (int i = 0; i < 8; i++)
            {
                var d = now.AddDays(i);
                if (i == 0 && d.Hour >= targetHour) continue;

                if (IsDayAllowed(d.DayOfWeek, days))
                    return new DateTime(d.Year, d.Month, d.Day, targetHour, 0, 0);
            }
            return now.AddDays(1);
        }

        private bool IsProfileDue(DiscoveryProfile p)
        {
            var now = DateTime.Now;
            if (now.Hour != p.ScheduleHour) return false;
            return IsDayAllowed(now.DayOfWeek, p.ScheduleDays);
        }

        private bool IsDayAllowed(DayOfWeek d, ScanDays s)
        {
            ScanDays flag = d switch
            {
                DayOfWeek.Monday => ScanDays.Monday,
                DayOfWeek.Tuesday => ScanDays.Tuesday,
                DayOfWeek.Wednesday => ScanDays.Wednesday,
                DayOfWeek.Thursday => ScanDays.Thursday,
                DayOfWeek.Friday => ScanDays.Friday,
                DayOfWeek.Saturday => ScanDays.Saturday,
                DayOfWeek.Sunday => ScanDays.Sunday,
                _ => ScanDays.None
            };
            return (s & flag) == flag;
        }
    }
}