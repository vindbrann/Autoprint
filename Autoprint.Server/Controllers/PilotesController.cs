using Autoprint.Server.Data;
using Autoprint.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Autoprint.Server.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class PilotesController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public PilotesController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: api/Pilotes
        [HttpGet]
        [Authorize(Policy = "DRIVER_READ")]
        public async Task<ActionResult<IEnumerable<Pilote>>> GetPilotes()
        {
            return await _context.Pilotes.ToListAsync();
        }

        // GET: api/Pilotes/5
        [HttpGet("{id}")]
        [Authorize(Policy = "DRIVER_READ")]
        public async Task<ActionResult<Pilote>> GetPilote(int id)
        {
            var pilote = await _context.Pilotes.FindAsync(id);

            if (pilote == null)
            {
                return NotFound();
            }

            return pilote;
        }

        // PUT: api/Pilotes/5
        [HttpPut("{id}")]
        [Authorize(Policy = "DRIVER_WRITE")]
        public async Task<IActionResult> PutPilote(int id, Pilote pilote)
        {
            if (id != pilote.Id)
            {
                return BadRequest();
            }

            _context.Entry(pilote).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!PiloteExists(id)) return NotFound();
                else throw;
            }

            return NoContent();
        }

        // POST: api/Pilotes
        [HttpPost]
        [Authorize(Policy = "DRIVER_WRITE")]
        public async Task<ActionResult<Pilote>> PostPilote(Pilote pilote)
        {
            _context.Pilotes.Add(pilote);
            await _context.SaveChangesAsync();

            return CreatedAtAction("GetPilote", new { id = pilote.Id }, pilote);
        }

        // DELETE: api/Pilotes/5
        [HttpDelete("{id}")]
        [Authorize(Policy = "DRIVER_DELETE")]
        public async Task<IActionResult> DeletePilote(int id)
        {
            var pilote = await _context.Pilotes.FindAsync(id);
            if (pilote == null)
            {
                return NotFound();
            }

            _context.Pilotes.Remove(pilote);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        private bool PiloteExists(int id)
        {
            return _context.Pilotes.Any(e => e.Id == id);
        }
    }
}