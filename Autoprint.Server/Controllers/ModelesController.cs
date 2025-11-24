using Autoprint.Server.Data;
using Autoprint.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Autoprint.Server.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(Policy = "MODEL_READ")]
    public class ModelesController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public ModelesController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<Modele>>> GetModeles()
        {
            return await _context.Modeles.Include(m => m.Marque).Include(m => m.Pilote).ToListAsync();
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<Modele>> GetModele(int id)
        {
            var modele = await _context.Modeles.Include(m => m.Marque).Include(m => m.Pilote).FirstOrDefaultAsync(m => m.Id == id);
            return modele == null ? NotFound() : modele;
        }

        [HttpPut("{id}")]
        [Authorize(Policy = "MODEL_WRITE")]
        public async Task<IActionResult> PutModele(int id, Modele modele)
        {
            if (id != modele.Id) return BadRequest();
            _context.Entry(modele).State = EntityState.Modified;

            // LOG AUDIT
            _context.AuditLogs.Add(new AuditLog { Action = "MODEL_UPDATE", Details = $"Modification modèle : {modele.Nom}", Utilisateur = User.Identity?.Name ?? "System", Niveau = "INFO", DateAction = DateTime.UtcNow });

            try { await _context.SaveChangesAsync(); }
            catch (DbUpdateConcurrencyException) { if (!ModeleExists(id)) return NotFound(); else throw; }
            return NoContent();
        }

        [HttpPost]
        [Authorize(Policy = "MODEL_WRITE")]
        public async Task<ActionResult<Modele>> PostModele(Modele modele)
        {
            _context.Modeles.Add(modele);

            // LOG AUDIT
            _context.AuditLogs.Add(new AuditLog { Action = "MODEL_CREATE", Details = $"Création modèle : {modele.Nom}", Utilisateur = User.Identity?.Name ?? "System", Niveau = "INFO", DateAction = DateTime.UtcNow });

            await _context.SaveChangesAsync();

            var newModele = await _context.Modeles.Include(m => m.Marque).Include(m => m.Pilote).FirstOrDefaultAsync(m => m.Id == modele.Id);
            return CreatedAtAction("GetModele", new { id = modele.Id }, newModele);
        }

        [HttpDelete("{id}")]
        [Authorize(Policy = "MODEL_DELETE")]
        public async Task<IActionResult> DeleteModele(int id)
        {
            var modele = await _context.Modeles.FindAsync(id);
            if (modele == null) return NotFound();

            // LOG AUDIT
            _context.AuditLogs.Add(new AuditLog { Action = "MODEL_DELETE", Details = $"Suppression modèle : {modele.Nom}", Utilisateur = User.Identity?.Name ?? "System", Niveau = "WARNING", DateAction = DateTime.UtcNow });

            _context.Modeles.Remove(modele);
            await _context.SaveChangesAsync();
            return NoContent();
        }

        private bool ModeleExists(int id) => _context.Modeles.Any(e => e.Id == id);
    }
}