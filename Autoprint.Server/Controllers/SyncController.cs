using Autoprint.Server.Data;
using Autoprint.Server.Services;
using Autoprint.Shared;
using Autoprint.Shared.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Autoprint.Server.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(Policy = "PRINTER_SYNC")]
    public class SyncController : ControllerBase
    {
        private readonly ISyncSpoolerService _syncService;
        private readonly ApplicationDbContext _context;

        public SyncController(ISyncSpoolerService syncService, ApplicationDbContext context)
        {
            _syncService = syncService;
            _context = context;
        }

        [HttpGet("preview")]
        public async Task<ActionResult<List<SyncPreviewDto>>> GetPreview()
        {
            return await _syncService.GetPendingChangesAsync();
        }

        [HttpPost("apply")]
        public async Task<ActionResult<BatchResult>> ApplyChanges([FromBody] List<int> ids)
        {
            if (ids == null || !ids.Any()) return BadRequest("Aucune imprimante sélectionnée.");

            var result = await _syncService.ApplyChangesAsync(ids);

            string details = $"Synchronisation Spouleur exécutée sur {ids.Count} imprimantes.";


            _context.AuditLogs.Add(new AuditLog
            {
                Action = "PRINTER_SYNC",
                Details = details,
                Utilisateur = User.Identity?.Name ?? "Système",
                Niveau = "WARNING", 
                DateAction = DateTime.UtcNow
            });

            await _context.SaveChangesAsync();

            return result;
        }
    }
}