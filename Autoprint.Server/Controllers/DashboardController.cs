using Autoprint.Server.Data;
using Autoprint.Shared.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

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
                .GroupBy(i => i.Modele.Nom)
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

            return stats;
        }
    }
}