using Autoprint.Server.Data;
using Autoprint.Server.DTOs;
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
        public async Task<ActionResult<DashboardStats>> GetStats()
        {
            var stats = new DashboardStats();

            // Compteurs simples (très rapides)
            stats.TotalImprimantes = await _context.Imprimantes.CountAsync();
            stats.TotalLieux = await _context.Emplacements.CountAsync();
            stats.TotalPilotes = await _context.Pilotes.CountAsync();

            // Compter les erreurs critiques dans les logs depuis 24h
            var hier = DateTime.UtcNow.AddHours(-24);
            stats.ImprimantesEnErreur = await _context.AuditLogs
                .Where(l => l.Niveau == "ERROR" && l.DateAction > hier)
                .CountAsync();

            return stats;
        }
    }
}