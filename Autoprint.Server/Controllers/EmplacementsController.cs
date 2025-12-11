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
    [Authorize(Policy = "LOCATION_READ")]
    public class EmplacementsController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly AuditService _auditService;

        public EmplacementsController(ApplicationDbContext context, AuditService auditService)
        {
            _context = context;
            _auditService = auditService;
        }

        [HttpGet]
        [AllowAnonymous]
        public async Task<ActionResult<IEnumerable<Emplacement>>> GetEmplacements()
        {
            if (User.Identity != null && User.Identity.IsAuthenticated)
            {
                return await _context.Emplacements.ToListAsync();
            }

            if (Request.Headers.TryGetValue("X-Agent-Secret", out var receivedSecret))
            {
                var setting = await _context.ServerSettings
                    .FirstOrDefaultAsync(s => s.Key == "AgentApiKey");

                if (setting == null)
                {
                    return StatusCode(500, new { message = "Erreur configuration serveur : AgentApiKey manquante." });
                }

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

            try
            {
                await _auditService.LogUpdateAsync(
                    id,
                    emplacement,
                    "LOCATION_UPDATE",
                    User.Identity?.Name);

                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!EmplacementExists(id)) return NotFound();
                else throw;
            }
            return NoContent();
        }

        [HttpPost]
        [Authorize(Policy = "LOCATION_WRITE")]
        public async Task<ActionResult<Emplacement>> PostEmplacement(Emplacement emplacement)
        {
            _context.Emplacements.Add(emplacement);

            _auditService.LogAction(
                "LOCATION_CREATE",
                $"Création lieu : {emplacement.Nom} ({emplacement.Code})",
                User.Identity?.Name,
                resourceName: emplacement.Nom
            );

            await _context.SaveChangesAsync();
            return CreatedAtAction("GetEmplacement", new { id = emplacement.Id }, emplacement);
        }

        [HttpDelete("{id}")]
        [Authorize(Policy = "LOCATION_DELETE")]
        public async Task<IActionResult> DeleteEmplacement(int id)
        {
            var emplacement = await _context.Emplacements.FindAsync(id);
            if (emplacement == null) return NotFound();

            _auditService.LogAction(
                "LOCATION_DELETE",
                $"Suppression lieu : {emplacement.Nom}",
                User.Identity?.Name,
                "WARNING",
                resourceName: emplacement.Nom
            );

            _context.Emplacements.Remove(emplacement);
            await _context.SaveChangesAsync();
            return NoContent();
        }

        private bool EmplacementExists(int id) => _context.Emplacements.Any(e => e.Id == id);
    }
}