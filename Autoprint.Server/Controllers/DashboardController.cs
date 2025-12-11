using Autoprint.Server.Data;
using Autoprint.Shared.DTOs;
using Autoprint.Shared.Enums; 
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.ServiceProcess;

namespace Autoprint.Server.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class DashboardController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public DashboardController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<ActionResult<DashboardStatsDto>> GetStats()
        {
            var stats = new DashboardStatsDto();

            stats.TotalImprimantes = await _context.Imprimantes.CountAsync();
            stats.TotalPilotes = await _context.Pilotes.CountAsync();
            stats.TotalLieux = await _context.Emplacements.CountAsync();

            var rawData = await _context.Imprimantes
                .Include(i => i.Modele)
                .GroupBy(i => i.Modele != null ? i.Modele.Nom : "Inconnu")
                .Select(g => new { Modele = g.Key, Count = g.Count() })
                .OrderByDescending(x => x.Count)
                .ToListAsync();

            var top5 = rawData.Take(5)
                .Select(x => new ChartDataDto { Label = x.Modele, Value = x.Count })
                .ToList();

            var othersCount = rawData.Skip(5).Sum(x => x.Count);

            if (othersCount > 0)
            {
                top5.Add(new ChartDataDto { Label = "Autres", Value = othersCount });
            }

            stats.RepartitionModeles = top5;


            stats.SyncErrorCount = await _context.Imprimantes
                .CountAsync(i => i.Status == PrinterStatus.SyncError);

            try
            {
                if (OperatingSystem.IsWindows())
                {
                    using var sc = new ServiceController("Spooler");
                    stats.IsSpoolerRunning = (sc.Status == ServiceControllerStatus.Running);
                }
                else
                {
                    stats.IsSpoolerRunning = true;
                }
            }
            catch
            {
                stats.IsSpoolerRunning = false;
            }
            try
            {
                var version = System.Reflection.Assembly.GetEntryAssembly()?.GetName().Version;
                if (version != null)
                {
                    stats.ServerVersion = $"v{version.Major}.{version.Minor}.{version.Build}";
                }
            }
            catch
            {
                stats.ServerVersion = "v?.?.?";
            }

            return stats;
        }
    }
}