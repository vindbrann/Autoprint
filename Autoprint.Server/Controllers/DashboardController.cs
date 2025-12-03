using Autoprint.Server.Data;
using Autoprint.Shared.DTOs;
using Autoprint.Shared.Enums; // Nécessaire pour PrinterStatus
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.ServiceProcess; // Nécessaire pour ServiceController

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

            // 1. Compteurs (Sans Users)
            stats.TotalImprimantes = await _context.Imprimantes.CountAsync();
            stats.TotalPilotes = await _context.Pilotes.CountAsync();
            stats.TotalLieux = await _context.Emplacements.CountAsync();

            // 2. Répartition par Modèle (Top 5 + Autres)
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

            // --- NOUVEAU : Données Techniques ---

            // 3. Compteur d'erreurs de synchro
            stats.SyncErrorCount = await _context.Imprimantes
                .CountAsync(i => i.Status == PrinterStatus.SyncError);

            // 4. État du Spouleur Windows
            try
            {
                if (OperatingSystem.IsWindows())
                {
                    // Vérifie si le service "Spouleur d'impression" tourne
                    using var sc = new ServiceController("Spooler");
                    stats.IsSpoolerRunning = (sc.Status == ServiceControllerStatus.Running);
                }
                else
                {
                    // Simulation pour le développement (si pas sous Windows)
                    stats.IsSpoolerRunning = true;
                }
            }
            catch
            {
                // En cas d'erreur de droits ou d'accès au service
                stats.IsSpoolerRunning = false;
            }
            // 5. Version du Serveur (Dynamique)
            try
            {
                var version = System.Reflection.Assembly.GetEntryAssembly()?.GetName().Version;
                if (version != null)
                {
                    // Formate en v1.0.0
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