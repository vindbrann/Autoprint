using Autoprint.Server.Data;
using Autoprint.Shared;
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

        public PilotesController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        [Authorize(Policy = "DRIVER_READ")]
        public async Task<ActionResult<IEnumerable<Pilote>>> GetPilotes() => await _context.Pilotes.ToListAsync();

        [HttpGet("{id}")]
        [Authorize(Policy = "DRIVER_READ")]
        public async Task<ActionResult<Pilote>> GetPilote(int id)
        {
            var pilote = await _context.Pilotes.FindAsync(id);
            return pilote == null ? NotFound() : pilote;
        }

        [HttpPut("{id}")]
        [Authorize(Policy = "DRIVER_WRITE")]
        public async Task<IActionResult> PutPilote(int id, Pilote pilote)
        {
            if (id != pilote.Id) return BadRequest();
            _context.Entry(pilote).State = EntityState.Modified;

            // LOG AUDIT
            _context.AuditLogs.Add(new AuditLog { Action = "DRIVER_UPDATE", Details = $"Modification pilote : {pilote.Nom}", Utilisateur = User.Identity?.Name ?? "System", Niveau = "INFO", DateAction = DateTime.UtcNow });

            try { await _context.SaveChangesAsync(); }
            catch (DbUpdateConcurrencyException) { if (!PiloteExists(id)) return NotFound(); else throw; }
            return NoContent();
        }

        [HttpPost]
        [Authorize(Policy = "DRIVER_WRITE")]
        public async Task<ActionResult<Pilote>> PostPilote(Pilote pilote)
        {
            _context.Pilotes.Add(pilote);

            // LOG AUDIT
            _context.AuditLogs.Add(new AuditLog { Action = "DRIVER_CREATE", Details = $"Ajout manuel pilote : {pilote.Nom}", Utilisateur = User.Identity?.Name ?? "System", Niveau = "INFO", DateAction = DateTime.UtcNow });

            await _context.SaveChangesAsync();
            return CreatedAtAction("GetPilote", new { id = pilote.Id }, pilote);
        }

        [HttpDelete("{id}")]
        [Authorize(Policy = "DRIVER_DELETE")]
        public async Task<IActionResult> DeletePilote(int id)
        {
            var pilote = await _context.Pilotes.FindAsync(id);
            if (pilote == null) return NotFound();

            // LOG AUDIT
            _context.AuditLogs.Add(new AuditLog { Action = "DRIVER_DELETE", Details = $"Suppression pilote : {pilote.Nom}", Utilisateur = User.Identity?.Name ?? "System", Niveau = "WARNING", DateAction = DateTime.UtcNow });

            _context.Pilotes.Remove(pilote);
            await _context.SaveChangesAsync();
            return NoContent();
        }

        private bool PiloteExists(int id) => _context.Pilotes.Any(e => e.Id == id);
    }
}