using Autoprint.Server.Data;
using Autoprint.Shared;
using Autoprint.Shared.DTOs;
using Autoprint.Server.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Autoprint.Server.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class PilotesController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly IDriverService _driverService;
        private readonly AuditService _auditService;

        public PilotesController(ApplicationDbContext context, IDriverService driverService, AuditService auditService)
        {
            _context = context;
            _driverService = driverService;
            _auditService = auditService;
        }

        [HttpGet]
        [Authorize(Policy = "DRIVER_READ")]
        public async Task<ActionResult<IEnumerable<Pilote>>> GetPilotes()
        {
            return await _context.Pilotes
                .OrderBy(p => p.EstInstalle ? 1 : 0)
                .ThenBy(p => p.Nom)
                .ToListAsync();
        }

        [HttpPost("sync")]
        [Authorize(Policy = "DRIVER_SCAN")]
        public async Task<ActionResult<BatchResult>> Synchroniser()
        {
            var result = await _driverService.SynchroniserPilotesAsync();
            bool aDesChangements = (result.Added > 0 || result.Updated > 0 || result.Deleted > 0);

            string messageLog = $"Scan Terminé. Résultat : " +
                                $"➕ {result.Added} Ajout(s) | " +
                                $"🔄 {result.Updated} Maj | " +
                                $"🗑️ {result.Deleted} Nettoyé(s)";

            string niveauLog = aDesChangements ? "WARNING" : "INFO";

            _auditService.LogAction(
                "DRIVER_SYNC",
                messageLog,
                User.Identity?.Name,
                niveauLog);

            await _context.SaveChangesAsync();

            return Ok(result);
        }
    }
}