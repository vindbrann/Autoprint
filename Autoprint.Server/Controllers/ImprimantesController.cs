using Autoprint.Server.Data;
using Autoprint.Shared;
using Autoprint.Shared.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Autoprint.Server.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ImprimantesController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        // On n'injecte PLUS le SpoolerService ici. Le contrôleur ne parle qu'à la BDD.
        public ImprimantesController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: api/Imprimantes
        [HttpGet]
        [Authorize(Policy = "PRINTER_READ")]
        public async Task<ActionResult<IEnumerable<Imprimante>>> GetImprimantes()
        {
            return await _context.Imprimantes
                .Include(i => i.Emplacement)
                .Include(i => i.Modele).ThenInclude(m => m.Marque)
                .Include(i => i.Modele).ThenInclude(m => m.Pilote)
                .ToListAsync();
        }

        // GET: api/Imprimantes/5
        [HttpGet("{id}")]
        [Authorize(Policy = "PRINTER_READ")]
        public async Task<ActionResult<Imprimante>> GetImprimante(int id)
        {
            var imprimante = await _context.Imprimantes
                .Include(i => i.Emplacement)
                .Include(i => i.Modele).ThenInclude(m => m.Marque)
                .Include(i => i.Modele).ThenInclude(m => m.Pilote)
                .FirstOrDefaultAsync(i => i.Id == id);

            if (imprimante == null) return NotFound();
            return imprimante;
        }

        // POST: api/Imprimantes
        [HttpPost]
        [Authorize(Policy = "PRINTER_WRITE")]
        public async Task<ActionResult<Imprimante>> PostImprimante(Imprimante imprimante)
        {
            // On force le statut : En attente de création
            imprimante.Status = PrinterStatus.PendingCreation;

            // On vide les champs techniques si l'utilisateur tente de les forcer
            imprimante.NomPartage = imprimante.NomAffiche; // Par défaut même nom

            _context.Imprimantes.Add(imprimante);
            await _context.SaveChangesAsync();

            return CreatedAtAction("GetImprimante", new { id = imprimante.Id }, imprimante);
        }

        // PUT: api/Imprimantes/5
        [HttpPut("{id}")]
        [Authorize(Policy = "PRINTER_WRITE")]
        public async Task<IActionResult> PutImprimante(int id, Imprimante imprimante)
        {
            if (id != imprimante.Id) return BadRequest();

            // On charge l'original pour ne pas écraser n'importe quoi
            var original = await _context.Imprimantes.AsNoTracking().FirstOrDefaultAsync(i => i.Id == id);
            if (original == null) return NotFound();

            // Logique de changement de statut
            if (original.Status == PrinterStatus.Synchronized)
            {
                // Si elle était synchro, elle devient "En attente de modif"
                imprimante.Status = PrinterStatus.PendingUpdate;
            }
            else if (original.Status == PrinterStatus.PendingCreation)
            {
                // Si elle n'était pas encore créée, elle reste "PendingCreation"
                imprimante.Status = PrinterStatus.PendingCreation;
            }
            // Si elle était en Error, on retente un Update
            else if (original.Status == PrinterStatus.SyncError)
            {
                imprimante.Status = PrinterStatus.PendingUpdate;
            }

            _context.Entry(imprimante).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!ImprimanteExists(id)) return NotFound();
                else throw;
            }

            return NoContent();
        }

        // DELETE: api/Imprimantes/5
        [HttpDelete("{id}")]
        [Authorize(Policy = "PRINTER_DELETE")]
        public async Task<IActionResult> DeleteImprimante(int id)
        {
            var imprimante = await _context.Imprimantes.FindAsync(id);
            if (imprimante == null) return NotFound();

            // STRATÉGIE "SOFT DELETE" (Option B)

            // Cas 1 : Elle n'a jamais été créée sur Windows (PendingCreation ou ImportedNeedsFix)
            // -> On peut la supprimer directement de la BDD, pas besoin de déranger le spouleur.
            if (imprimante.Status == PrinterStatus.PendingCreation || imprimante.Status == PrinterStatus.ImportedNeedsFix)
            {
                _context.Imprimantes.Remove(imprimante);
            }
            // Cas 2 : Elle existe (ou a existé) sur Windows -> On marque pour suppression différée
            else
            {
                imprimante.Status = PrinterStatus.PendingDelete;
                _context.Entry(imprimante).State = EntityState.Modified;
            }

            await _context.SaveChangesAsync();

            return NoContent();
        }

        private bool ImprimanteExists(int id)
        {
            return _context.Imprimantes.Any(e => e.Id == id);
        }
    }
}