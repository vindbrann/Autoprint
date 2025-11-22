using Autoprint.Server.DTOs;
using Autoprint.Server.Services;
using Autoprint.Shared.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Autoprint.Server.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(Policy = "PRINTER_SYNC")] // Sécurité globale sur le contrôleur
    public class SyncController : ControllerBase
    {
        private readonly ISyncSpoolerService _syncService;

        public SyncController(ISyncSpoolerService syncService)
        {
            _syncService = syncService;
        }

        [HttpGet("preview")]
        public async Task<ActionResult<List<SyncPreviewDto>>> GetPreview()
        {
            return await _syncService.GetPendingChangesAsync();
        }

        [HttpPost("apply")]
        public async Task<ActionResult<BatchResult>> ApplyChanges([FromBody] List<int> ids)
        {
            return await _syncService.ApplyChangesAsync(ids);
        }
    }
}