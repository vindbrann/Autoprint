using Autoprint.Server.Data;
using Autoprint.Server.Services;
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
        private readonly IPrintSpoolerService _spoolerService;

        public ImprimantesController(ApplicationDbContext context, IPrintSpoolerService spoolerService)
        {
            _context = context;
            _spoolerService = spoolerService;
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
            // 1. Sauvegarde en BDD
            // On force le statut "En attente de création" pour le background service ou l'action manuelle
            imprimante.Status = PrinterStatus.PendingCreation;

            _context.Imprimantes.Add(imprimante);
            await _context.SaveChangesAsync();

            // 2. Tentative de création immédiate sur Windows (Optionnel, ou via bouton "Appliquer")
            // Pour l'instant, on le fait en direct pour garder le comportement Phase 1
            // Il faudra peut-être déplacer ça dans une action "Synchroniser" plus tard.
            try
            {
                // On a besoin des infos du pilote liées au modèle
                var modele = await _context.Modeles
                    .Include(m => m.Pilote)
                    .FirstOrDefaultAsync(m => m.Id == imprimante.ModeleId);

                if (modele?.Pilote != null && !string.IsNullOrEmpty(imprimante.AdresseIp))
                {
                    // A. Création du Port (Signature corrigée : juste l'IP)
                    await _spoolerService.CreerPortTcp(imprimante.AdresseIp);

                    // B. Création de l'imprimante (Signature corrigée)
                    await _spoolerService.CreerImprimante(
                        nom: imprimante.NomAffiche,
                        driverName: modele.Pilote.Nom, // On utilise le nom du pilote Windows
                        ipAddress: imprimante.AdresseIp
                    );

                    // Si succès, on passe en Vert
                    imprimante.Status = PrinterStatus.Synchronized;
                    await _context.SaveChangesAsync();
                }
            }
            catch (Exception ex)
            {
                // En cas d'erreur Windows, on ne bloque pas la création BDD mais on loggue
                Console.WriteLine($"Erreur création Windows : {ex.Message}");
                // On pourrait passer le statut en "Error" ici
            }

            return CreatedAtAction("GetImprimante", new { id = imprimante.Id }, imprimante);
        }

        // PUT: api/Imprimantes/5
        [HttpPut("{id}")]
        [Authorize(Policy = "PRINTER_WRITE")]
        public async Task<IActionResult> PutImprimante(int id, Imprimante imprimante)
        {
            if (id != imprimante.Id) return BadRequest();

            // On détecte les changements pour le statut (simplifié ici)
            imprimante.Status = PrinterStatus.PendingUpdate;

            _context.Entry(imprimante).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();

                // Mise à jour Windows (Simplifiée : on ne gère pas tout ici pour l'instant)
                // L'idéal sera le module de Synchro dédié.
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

            // 1. Suppression Windows (Signature corrigée)
            try
            {
                await _spoolerService.SupprimerImprimante(imprimante.NomAffiche);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erreur suppression Windows : {ex.Message}");
            }

            // 2. Suppression BDD
            _context.Imprimantes.Remove(imprimante);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        private bool ImprimanteExists(int id)
        {
            return _context.Imprimantes.Any(e => e.Id == id);
        }
    }
}