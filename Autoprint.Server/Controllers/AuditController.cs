using Autoprint.Server.Data;
using Autoprint.Shared;
using Autoprint.Shared.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Linq;

namespace Autoprint.Server.Controllers
{
    [Route("api/[controller]")]
    [ApiController]

    [Authorize(Policy = "AUDIT_READ")]
    public class AuditController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public AuditController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<ActionResult<PaginatedList<AuditLogDto>>> GetLogs(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20,
            [FromQuery] string? search = null)
        {
            var query = _context.AuditLogs.AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                query = query.Where(l =>
                    (l.Utilisateur != null && l.Utilisateur.Contains(search)) ||
                    (l.Action != null && l.Action.Contains(search)) ||
                    (l.Details != null && l.Details.Contains(search)) ||
                    (l.ResourceName != null && l.ResourceName.Contains(search)));
            }

            query = query.OrderByDescending(l => l.DateAction);

            var totalItems = await query.CountAsync();

            if (page < 1) page = 1;

            var items = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(l => new AuditLogDto
                {
                    Id = l.Id,
                    DateAction = l.DateAction,
                    Utilisateur = l.Utilisateur ?? string.Empty,
                    Action = l.Action ?? string.Empty,
                    ResourceName = l.ResourceName ?? string.Empty,
                    Details = l.Details ?? string.Empty,
                    Niveau = l.Niveau ?? string.Empty,
                    OldValues = l.OldValues,
                    NewValues = l.NewValues
                })
                .ToListAsync();

            return new PaginatedList<AuditLogDto>
            {
                Items = items,
                TotalCount = totalItems,
                PageIndex = page,
                TotalPages = (int)Math.Ceiling(totalItems / (double)pageSize)
            };
        }

        [HttpGet("{id:int}")]
        public async Task<ActionResult<AuditLog>> GetLogById(int id)
        {
            var log = await _context.AuditLogs.FindAsync(id);

            if (log == null)
            {
                return NotFound();
            }

            return log;
        }

        [HttpDelete("cleanup")]

        [Authorize(Roles = "SuperAdmin")]
        public async Task<IActionResult> CleanupOldLogs()
        {
            var limitDate = DateTime.UtcNow.AddDays(-30);

            int deleted = await _context.AuditLogs
                .Where(l => l.DateAction < limitDate)
                .ExecuteDeleteAsync();

            if (deleted > 0)
            {
                _context.AuditLogs.Add(new AuditLog
                {
                    Action = "LOG_PURGE",
                    Utilisateur = User.Identity?.Name ?? "System",
                    Details = $"Nettoyage manuel de {deleted} logs anciens.",
                    Niveau = "WARNING",
                    DateAction = DateTime.UtcNow
                });
                await _context.SaveChangesAsync();
            }

            return Ok(new { Message = $"{deleted} logs purgés manuellement." });
        }
    }
}