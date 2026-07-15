using Autoprint.Server.Data;
using Autoprint.Server.Filters;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Threading.Tasks;
using System;

namespace Autoprint.Server.Controllers
{
    [Route("api/v1/[controller]")]
    [ApiController]
    public class MonitoringController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public MonitoringController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpGet("export")]
        [ApiKey]
        public async Task<IActionResult> ExportAlertPrinters()
        {
            var alertPrinters = await _context.Imprimantes
                .Include(i => i.Emplacement)
                .Include(i => i.Modele)
                .Where(i => !i.IsArchived && (i.MonitoringStatus == "Offline" || i.MonitoringStatus == "Warning" || i.MonitoringStatus == "Critical"))
                .ToListAsync();

            var result = alertPrinters.Select(p => new
            {
                p.Id,
                p.NomAffiche,
                p.AdresseIp,
                Location = p.Emplacement?.Nom ?? "Non défini",
                Model = p.Modele?.Nom ?? "Générique",
                Status = p.MonitoringStatus,
                p.LastSeen
            });

            return Ok(result);
        }
    }
}
