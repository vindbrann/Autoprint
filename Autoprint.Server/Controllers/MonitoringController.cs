using Autoprint.Server.Data;
using Autoprint.Server.Filters;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Threading.Tasks;
using System;

using System.Collections.Generic;

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
            var includeOfflineStr = await _context.ServerSettings.FirstOrDefaultAsync(s => s.Key == "Monitoring_IncludeOffline");
            var includeOffline = includeOfflineStr == null || bool.Parse(includeOfflineStr.Value);

            var includeWarningStr = await _context.ServerSettings.FirstOrDefaultAsync(s => s.Key == "Monitoring_IncludeWarning");
            var includeWarning = includeWarningStr == null || bool.Parse(includeWarningStr.Value);

            var includeCriticalStr = await _context.ServerSettings.FirstOrDefaultAsync(s => s.Key == "Monitoring_IncludeCritical");
            var includeCritical = includeCriticalStr == null || bool.Parse(includeCriticalStr.Value);

            var includeArchivedStr = await _context.ServerSettings.FirstOrDefaultAsync(s => s.Key == "Monitoring_IncludeArchived");
            var includeArchived = includeArchivedStr != null && bool.Parse(includeArchivedStr.Value);

            var query = _context.Imprimantes
                .Include(i => i.Emplacement)
                .Include(i => i.Modele)
                .AsQueryable();

            if (!includeArchived)
            {
                query = query.Where(i => !i.IsArchived);
            }

            var allowedStatuses = new List<string>();
            if (includeOffline) allowedStatuses.Add("Offline");
            if (includeWarning) allowedStatuses.Add("Warning");
            if (includeCritical) allowedStatuses.Add("Critical");

            query = query.Where(i => allowedStatuses.Contains(i.MonitoringStatus));

            var alertPrinters = await query.ToListAsync();

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
