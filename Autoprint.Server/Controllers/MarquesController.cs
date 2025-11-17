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
    [Authorize(Policy = "BRAND_READ")]
    public class MarquesController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public MarquesController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: api/Marques
        [HttpGet]
        public async Task<ActionResult<IEnumerable<Marque>>> GetMarques()
        {
            return await _context.Marques.ToListAsync();
        }

        // GET: api/Marques/5
        [HttpGet("{id}")]
        public async Task<ActionResult<Marque>> GetMarque(int id)
        {
            var marque = await _context.Marques.FindAsync(id);

            if (marque == null)
            {
                return NotFound();
            }

            return marque;
        }

        // PUT: api/Marques/5
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        [HttpPut("{id}")]
        [Authorize(Policy = "BRAND_WRITE")]
        public async Task<IActionResult> PutMarque(int id, Marque marque)
        {
            if (id != marque.Id)
            {
                return BadRequest();
            }

            _context.Entry(marque).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!MarqueExists(id))
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

        // POST: api/Marques
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        [HttpPost]
        [Authorize(Policy = "BRAND_WRITE")]
        public async Task<ActionResult<Marque>> PostMarque(Marque marque)
        {
            _context.Marques.Add(marque);
            await _context.SaveChangesAsync();

            return CreatedAtAction("GetMarque", new { id = marque.Id }, marque);
        }

        // DELETE: api/Marques/5
        [HttpDelete("{id}")]
        [Authorize(Policy = "BRAND_DELETE")]
        public async Task<IActionResult> DeleteMarque(int id)
        {
            var marque = await _context.Marques.FindAsync(id);
            if (marque == null)
            {
                return NotFound();
            }

            _context.Marques.Remove(marque);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        private bool MarqueExists(int id)
        {
            return _context.Marques.Any(e => e.Id == id);
        }
    }
}
