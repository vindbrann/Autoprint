using Autoprint.Server.Data;
using Autoprint.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Autoprint.Server.Services;

namespace Autoprint.Server.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(Policy = "MODEL_READ")]
    public class ModelesController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly AuditService _auditService;

        public ModelesController(ApplicationDbContext context, AuditService auditService)
        {
            _context = context;
            _auditService = auditService;
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
        public async Task<IActionResult> PutModele(int id, Modele inputModele)
        {
            if (id != inputModele.Id) return BadRequest("ID incohérent.");

            if (inputModele.MarqueId == 0)
            {
                return BadRequest("Erreur : Aucune marque sélectionnée (MarqueId = 0).");
            }

            bool marqueExiste = await _context.Marques.AnyAsync(m => m.Id == inputModele.MarqueId);
            if (!marqueExiste)
            {
                return BadRequest($"Erreur : La Marque avec l'ID {inputModele.MarqueId} n'existe pas en base.");
            }

            if (inputModele.PiloteId.HasValue)
            {
                bool piloteExiste = await _context.Pilotes.AnyAsync(p => p.Id == inputModele.PiloteId.Value);
                if (!piloteExiste)
                {
                    return BadRequest($"Erreur : Le Pilote avec l'ID {inputModele.PiloteId} n'existe pas en base.");
                }
            }

            var dbModele = await _context.Modeles.FindAsync(id);
            if (dbModele == null) return NotFound();

            dbModele.Nom = inputModele.Nom;
            dbModele.Code = inputModele.Code;
            dbModele.MarqueId = inputModele.MarqueId;
            dbModele.PiloteId = inputModele.PiloteId;

            var nomMarque = await _context.Marques
                .AsNoTracking()
                .Where(m => m.Id == inputModele.MarqueId)
                .Select(m => m.Nom)
                .FirstOrDefaultAsync();

            string? nomPilote = null;
            if (inputModele.PiloteId.HasValue)
            {
                nomPilote = await _context.Pilotes
                    .AsNoTracking()
                    .Where(p => p.Id == inputModele.PiloteId)
                    .Select(p => p.Nom)
                    .FirstOrDefaultAsync();
            }

            var auditSnapshot = new Modele
            {
                Id = id,
                Nom = inputModele.Nom,
                Code = inputModele.Code,
                MarqueId = inputModele.MarqueId,
                PiloteId = inputModele.PiloteId,
                Marque = new Marque { Id = inputModele.MarqueId, Nom = nomMarque ?? "Inconnu" },
                Pilote = nomPilote != null ? new Pilote { Id = inputModele.PiloteId.Value, Nom = nomPilote } : null
            };

            try
            {
                await _auditService.LogUpdateAsync(
                    id,
                    auditSnapshot,
                    "MODEL_UPDATE",
                    User.Identity?.Name,
                    "Marque", "Pilote");

                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!ModeleExists(id)) return NotFound();
                else throw;
            }

            return NoContent();
        }

        [HttpPost]
        [Authorize(Policy = "MODEL_WRITE")]
        public async Task<ActionResult<Modele>> PostModele(Modele modele)
        {
            if (modele.MarqueId == 0) return BadRequest("Marque obligatoire.");

            if (!await _context.Marques.AnyAsync(m => m.Id == modele.MarqueId))
                return BadRequest($"Marque ID {modele.MarqueId} introuvable.");

            modele.Marque = null!;
            modele.Pilote = null;

            _context.Modeles.Add(modele);

            _auditService.LogAction(
                "MODEL_CREATE",
                $"Création modèle : {modele.Nom}",
                User.Identity?.Name,
                resourceName: modele.Nom);

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

            _auditService.LogAction(
                "MODEL_DELETE",
                $"Suppression modèle : {modele.Nom}",
                User.Identity?.Name,
                "WARNING",
                resourceName: modele.Nom);

            _context.Modeles.Remove(modele);
            await _context.SaveChangesAsync();
            return NoContent();
        }

        private bool ModeleExists(int id) => _context.Modeles.Any(e => e.Id == id);
    }
}