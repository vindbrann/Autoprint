using Autoprint.Server.Data;
using Autoprint.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Autoprint.Server.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(Policy = "LOCATION_READ")]
    public class EmplacementsController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public EmplacementsController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        [AllowAnonymous]
        public async Task<ActionResult<IEnumerable<Emplacement>>> GetEmplacements()
        {
            // 1. Admin connecté
            if (User.Identity != null && User.Identity.IsAuthenticated)
            {
                return await _context.Emplacements.ToListAsync();
            }

            // 2. Agent avec Clé API
            if (Request.Headers.TryGetValue("X-Agent-Secret", out var receivedSecret))
            {
                // A. On cherche la clé en BDD
                var setting = await _context.ServerSettings
                    .FirstOrDefaultAsync(s => s.Key == "AgentApiKey");

                // B. STRICT : Si la clé n'existe pas en base, c'est une erreur critique serveur
                if (setting == null)
                {
                    // On log l'erreur pour l'admin et on rejette
                    return StatusCode(500, new { message = "Erreur configuration serveur : AgentApiKey manquante." });
                }

                // C. Comparaison (Plus de valeur par défaut ici !)
                if (receivedSecret == setting.Value)
                {
                    return await _context.Emplacements.ToListAsync();
                }
            }

            return Unauthorized(new { message = "Accès refusé. Authentification ou Clé API requise." });
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<Emplacement>> GetEmplacement(int id)
        {
            var emplacement = await _context.Emplacements.FindAsync(id);
            return emplacement == null ? NotFound() : emplacement;
        }

        [HttpPut("{id}")]
        [Authorize(Policy = "LOCATION_WRITE")]
        public async Task<IActionResult> PutEmplacement(int id, Emplacement emplacement)
        {
            if (id != emplacement.Id) return BadRequest();
            _context.Entry(emplacement).State = EntityState.Modified;

            // LOG AUDIT
            _context.AuditLogs.Add(new AuditLog { Action = "LOCATION_UPDATE", Details = $"Modification lieu : {emplacement.Nom}", Utilisateur = User.Identity?.Name ?? "System", Niveau = "INFO", DateAction = DateTime.UtcNow });

            try { await _context.SaveChangesAsync(); }
            catch (DbUpdateConcurrencyException) { if (!EmplacementExists(id)) return NotFound(); else throw; }
            return NoContent();
        }

        [HttpPost]
        [Authorize(Policy = "LOCATION_WRITE")]
        public async Task<ActionResult<Emplacement>> PostEmplacement(Emplacement emplacement)
        {
            _context.Emplacements.Add(emplacement);

            // LOG AUDIT
            _context.AuditLogs.Add(new AuditLog { Action = "LOCATION_CREATE", Details = $"Création lieu : {emplacement.Nom} ({emplacement.Code})", Utilisateur = User.Identity?.Name ?? "System", Niveau = "INFO", DateAction = DateTime.UtcNow });

            await _context.SaveChangesAsync();
            return CreatedAtAction("GetEmplacement", new { id = emplacement.Id }, emplacement);
        }

        [HttpDelete("{id}")]
        [Authorize(Policy = "LOCATION_DELETE")]
        public async Task<IActionResult> DeleteEmplacement(int id)
        {
            var emplacement = await _context.Emplacements.FindAsync(id);
            if (emplacement == null) return NotFound();

            // LOG AUDIT
            _context.AuditLogs.Add(new AuditLog { Action = "LOCATION_DELETE", Details = $"Suppression lieu : {emplacement.Nom}", Utilisateur = User.Identity?.Name ?? "System", Niveau = "WARNING", DateAction = DateTime.UtcNow });

            _context.Emplacements.Remove(emplacement);
            await _context.SaveChangesAsync();
            return NoContent();
        }

        private bool EmplacementExists(int id) => _context.Emplacements.Any(e => e.Id == id);
    }
}