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
    [Authorize(Policy = "BRAND_READ")]
    public class MarquesController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly AuditService _auditService;

        public MarquesController(ApplicationDbContext context, AuditService auditService)
        {
            _context = context;
            _auditService = auditService;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<Marque>>> GetMarques()
        {
            return await _context.Marques
                .AsNoTracking()
                .Select(m => new Marque
                {
                    Id = m.Id,
                    Nom = m.Nom,
                    PrinterCount = _context.Imprimantes.Count(i => i.Modele.MarqueId == m.Id)
                })
                .ToListAsync();
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<Marque>> GetMarque(int id)
        {
            var marque = await _context.Marques
                .AsNoTracking()
                .Where(m => m.Id == id)
                .Select(m => new Marque
                {
                    Id = m.Id,
                    Nom = m.Nom,
                    PrinterCount = _context.Imprimantes.Count(i => i.Modele.MarqueId == m.Id)
                })
                .FirstOrDefaultAsync();

            return marque == null ? NotFound() : marque;
        }

        [HttpPut("{id}")]
        [Authorize(Policy = "BRAND_WRITE")]
        public async Task<IActionResult> PutMarque(int id, Marque marque)
        {
            if (id != marque.Id) return BadRequest();

            _context.Entry(marque).State = EntityState.Modified;

            try
            {
                await _auditService.LogUpdateAsync(
                    id,
                    marque,
                    "BRAND_UPDATE",
                    User.Identity?.Name);

                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!MarqueExists(id)) return NotFound();
                else throw;
            }
            return NoContent();
        }

        [HttpPost]
        [Authorize(Policy = "BRAND_WRITE")]
        public async Task<ActionResult<Marque>> PostMarque(Marque marque)
        {
            _context.Marques.Add(marque);

            _auditService.LogAction(
                "BRAND_CREATE",
                $"Création marque : {marque.Nom}",
                User.Identity?.Name,
                resourceName: marque.Nom);

            await _context.SaveChangesAsync();
            return CreatedAtAction("GetMarque", new { id = marque.Id }, marque);
        }

        [HttpDelete("{id}")]
        [Authorize(Policy = "BRAND_DELETE")]
        public async Task<IActionResult> DeleteMarque(int id)
        {
            var marque = await _context.Marques.FindAsync(id);
            if (marque == null) return NotFound();

            _auditService.LogAction(
                "BRAND_DELETE",
                $"Suppression marque : {marque.Nom}",
                User.Identity?.Name,
                "WARNING",
                resourceName: marque.Nom);

            _context.Marques.Remove(marque);
            await _context.SaveChangesAsync();
            return NoContent();
        }

        private bool MarqueExists(int id) => _context.Marques.Any(e => e.Id == id);
    }
}