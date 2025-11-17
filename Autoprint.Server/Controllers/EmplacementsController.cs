using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Autoprint.Server.Data;
using Autoprint.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
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

        // GET: api/Emplacements
        [HttpGet]
        public async Task<ActionResult<IEnumerable<Emplacement>>> GetEmplacements()
        {
            return await _context.Emplacements.ToListAsync();
        }

        // GET: api/Emplacements/5
        [HttpGet("{id}")]
        public async Task<ActionResult<Emplacement>> GetEmplacement(int id)
        {
            var emplacement = await _context.Emplacements.FindAsync(id);

            if (emplacement == null)
            {
                return NotFound();
            }

            return emplacement;
        }

        // PUT: api/Emplacements/5
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        [HttpPut("{id}")]
        [Authorize(Policy = "LOCATION_WRITE")]
        public async Task<IActionResult> PutEmplacement(int id, Emplacement emplacement)
        {
            if (id != emplacement.Id)
            {
                return BadRequest();
            }

            _context.Entry(emplacement).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!EmplacementExists(id))
                {
                    return NotFound();
                }
                else
                {
                    throw;
                }
            }

            return NoContent();
        }

        // POST: api/Emplacements
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        [HttpPost]
        [Authorize(Policy = "LOCATION_WRITE")]
        public async Task<ActionResult<Emplacement>> PostEmplacement(Emplacement emplacement)
        {
            _context.Emplacements.Add(emplacement);
            await _context.SaveChangesAsync();

            return CreatedAtAction("GetEmplacement", new { id = emplacement.Id }, emplacement);
        }

        // DELETE: api/Emplacements/5
        [HttpDelete("{id}")]
        [Authorize(Policy = "LOCATION_DELETE")]
        public async Task<IActionResult> DeleteEmplacement(int id)
        {
            var emplacement = await _context.Emplacements.FindAsync(id);
            if (emplacement == null)
            {
                return NotFound();
            }

            _context.Emplacements.Remove(emplacement);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        private bool EmplacementExists(int id)
        {
            return _context.Emplacements.Any(e => e.Id == id);
        }
    }
}
