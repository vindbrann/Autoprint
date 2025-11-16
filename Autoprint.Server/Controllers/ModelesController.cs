using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Autoprint.Server.Data;
using Autoprint.Shared;

namespace Autoprint.Server.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ModelesController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public ModelesController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: api/Modeles
        [HttpGet]
        public async Task<ActionResult<IEnumerable<Modele>>> GetModeles()
        {
            // CORRECTION ICI : On ajoute les .Include pour charger les noms
            return await _context.Modeles
                .Include(m => m.Marque)
                .Include(m => m.Pilote)
                .ToListAsync();
        }

        // GET: api/Modeles/5
        [HttpGet("{id}")]
        public async Task<ActionResult<Modele>> GetModele(int id)
        {
            var modele = await _context.Modeles
                .Include(m => m.Marque)
                .Include(m => m.Pilote)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (modele == null) return NotFound();

            return modele;
        }

        // POST: api/Modeles
        [HttpPost]
        public async Task<ActionResult<Modele>> PostModele(Modele modele)
        {
            _context.Modeles.Add(modele);
            await _context.SaveChangesAsync();

            // On recharge l'objet complet pour renvoyer les noms au frontend tout de suite
            var newModele = await _context.Modeles
                .Include(m => m.Marque)
                .Include(m => m.Pilote)
                .FirstOrDefaultAsync(m => m.Id == modele.Id);

            return CreatedAtAction("GetModele", new { id = modele.Id }, newModele);
        }

        // PUT: api/Modeles/5
        [HttpPut("{id}")]
        public async Task<IActionResult> PutModele(int id, Modele modele)
        {
            if (id != modele.Id) return BadRequest();

            _context.Entry(modele).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!ModeleExists(id)) return NotFound();
                else throw;
            }

            return NoContent();
        }

        // DELETE: api/Modeles/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteModele(int id)
        {
            var modele = await _context.Modeles.FindAsync(id);
            if (modele == null) return NotFound();

            _context.Modeles.Remove(modele);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        private bool ModeleExists(int id)
        {
            return _context.Modeles.Any(e => e.Id == id);
        }
    }
}