using Autoprint.Server.Data;
using Autoprint.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Autoprint.Server.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(Policy = "BRAND_READ")]
    public class MarquesController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public MarquesController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<Marque>>> GetMarques() => await _context.Marques.ToListAsync();

        [HttpGet("{id}")]
        public async Task<ActionResult<Marque>> GetMarque(int id)
        {
            var marque = await _context.Marques.FindAsync(id);
            return marque == null ? NotFound() : marque;
        }

        [HttpPut("{id}")]
        [Authorize(Policy = "BRAND_WRITE")]
        public async Task<IActionResult> PutMarque(int id, Marque marque)
        {
            if (id != marque.Id) return BadRequest();
            _context.Entry(marque).State = EntityState.Modified;

            // LOG AUDIT
            _context.AuditLogs.Add(new AuditLog { Action = "BRAND_UPDATE", Details = $"Modification marque : {marque.Nom}", Utilisateur = User.Identity?.Name ?? "System", Niveau = "INFO", DateAction = DateTime.UtcNow });

            try { await _context.SaveChangesAsync(); }
            catch (DbUpdateConcurrencyException) { if (!MarqueExists(id)) return NotFound(); else throw; }
            return NoContent();
        }

        [HttpPost]
        [Authorize(Policy = "BRAND_WRITE")]
        public async Task<ActionResult<Marque>> PostMarque(Marque marque)
        {
            _context.Marques.Add(marque);

            // LOG AUDIT
            _context.AuditLogs.Add(new AuditLog { Action = "BRAND_CREATE", Details = $"Création marque : {marque.Nom}", Utilisateur = User.Identity?.Name ?? "System", Niveau = "INFO", DateAction = DateTime.UtcNow });

            await _context.SaveChangesAsync();
            return CreatedAtAction("GetMarque", new { id = marque.Id }, marque);
        }

        [HttpDelete("{id}")]
        [Authorize(Policy = "BRAND_DELETE")]
        public async Task<IActionResult> DeleteMarque(int id)
        {
            var marque = await _context.Marques.FindAsync(id);
            if (marque == null) return NotFound();

            // LOG AUDIT
            _context.AuditLogs.Add(new AuditLog { Action = "BRAND_DELETE", Details = $"Suppression marque : {marque.Nom}", Utilisateur = User.Identity?.Name ?? "System", Niveau = "WARNING", DateAction = DateTime.UtcNow });

            _context.Marques.Remove(marque);
            await _context.SaveChangesAsync();
            return NoContent();
        }

        private bool MarqueExists(int id) => _context.Marques.Any(e => e.Id == id);
    }
}