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

        public ImprimantesController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: api/Imprimantes
        [HttpGet]
        [AllowAnonymous] // Modification : On ouvre pour vérifier manuellement la Clé API
        public async Task<ActionResult<IEnumerable<Imprimante>>> GetImprimantes()
        {
            // 1. Accès Admin (Utilisateur connecté)
            if (User.Identity != null && User.Identity.IsAuthenticated)
            {
                return await GetImprimantesListWithIncludes();
            }

            // 2. Accès Agent (Machine avec Clé API)
            if (Request.Headers.TryGetValue("X-Agent-Secret", out var receivedSecret))
            {
                // Vérification en BDD
                var setting = await _context.ServerSettings
                    .FirstOrDefaultAsync(s => s.Key == "AgentApiKey");

                if (setting != null && receivedSecret == setting.Value)
                {
                    return await GetImprimantesListWithIncludes();
                }
            }

            // 3. Rejet
            return Unauthorized(new { message = "Accès refusé. Authentification ou Clé API requise." });
        }

        // Méthode privée pour éviter de dupliquer la grosse requête SQL avec les Includes
        private async Task<List<Imprimante>> GetImprimantesListWithIncludes()
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
            imprimante.Status = PrinterStatus.PendingCreation;
            imprimante.NomPartage = imprimante.NomAffiche;

            _context.Imprimantes.Add(imprimante);

            // --- LOG AUDIT ---
            _context.AuditLogs.Add(new AuditLog
            {
                Action = "PRINTER_CREATE",
                Details = $"Ajout de l'imprimante '{imprimante.NomAffiche}' (IP: {imprimante.AdresseIp})",
                Utilisateur = User.Identity?.Name ?? "Inconnu",
                Niveau = "INFO",
                DateAction = DateTime.UtcNow
            });
            // -----------------

            await _context.SaveChangesAsync();

            return CreatedAtAction("GetImprimante", new { id = imprimante.Id }, imprimante);
        }

        // PUT: api/Imprimantes/5
        [HttpPut("{id}")]
        [Authorize(Policy = "PRINTER_WRITE")]
        public async Task<IActionResult> PutImprimante(int id, Imprimante imprimante)
        {
            if (id != imprimante.Id) return BadRequest();

            var original = await _context.Imprimantes.AsNoTracking().FirstOrDefaultAsync(i => i.Id == id);
            if (original == null) return NotFound();

            if (original.Status == PrinterStatus.Synchronized)
                imprimante.Status = PrinterStatus.PendingUpdate;
            else if (original.Status == PrinterStatus.PendingCreation)
                imprimante.Status = PrinterStatus.PendingCreation;
            else if (original.Status == PrinterStatus.SyncError)
                imprimante.Status = PrinterStatus.PendingUpdate;

            _context.Entry(imprimante).State = EntityState.Modified;

            // --- LOG AUDIT ---
            _context.AuditLogs.Add(new AuditLog
            {
                Action = "PRINTER_UPDATE",
                Details = $"Modification de '{imprimante.NomAffiche}' (Statut: {imprimante.Status})",
                Utilisateur = User.Identity?.Name ?? "Inconnu",
                Niveau = "INFO",
                DateAction = DateTime.UtcNow
            });
            // -----------------

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

            string logDetails;

            if (imprimante.Status == PrinterStatus.PendingCreation || imprimante.Status == PrinterStatus.ImportedNeedsFix)
            {
                _context.Imprimantes.Remove(imprimante);
                logDetails = $"Suppression immédiate (BDD) de '{imprimante.NomAffiche}'";
            }
            else
            {
                imprimante.Status = PrinterStatus.PendingDelete;
                _context.Entry(imprimante).State = EntityState.Modified;
                logDetails = $"Marquage pour suppression (Spouleur) de '{imprimante.NomAffiche}'";
            }

            // --- LOG AUDIT ---
            _context.AuditLogs.Add(new AuditLog
            {
                Action = "PRINTER_DELETE",
                Details = logDetails,
                Utilisateur = User.Identity?.Name ?? "Inconnu",
                Niveau = "WARNING", // Suppression = Warning
                DateAction = DateTime.UtcNow
            });
            // -----------------

            await _context.SaveChangesAsync();

            return NoContent();
        }

        private bool ImprimanteExists(int id)
        {
            return _context.Imprimantes.Any(e => e.Id == id);
        }
    }
}