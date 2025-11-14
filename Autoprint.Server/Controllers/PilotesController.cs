using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Autoprint.Server.Data;
using Autoprint.Server.Models;
using Autoprint.Server.Services;

namespace Autoprint.Server.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class PilotesController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly IFileService _fileService;

        public PilotesController(ApplicationDbContext context, IFileService fileService)
        {
            _context = context;
            _fileService = fileService;
        }

        // POST: api/Pilotes/Upload
        [HttpPost("Upload")]
        public async Task<IActionResult> Upload(IFormFile file)
        {
            if (file == null || file.Length == 0)
                return BadRequest("Aucun fichier fourni.");

            try
            {
                // On appelle notre service qui fait tout le travail (Sauvegarde + Hash)
                var (chemin, checksum) = await _fileService.SaveFileAsync(file);

                // On renvoie les infos au frontend pour qu'il puisse pré-remplir le formulaire de création
                return Ok(new { CheminFichier = chemin, Checksum = checksum });
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Erreur interne : {ex.Message}");
            }
        }

        // GET: api/Pilotes
        [HttpGet]
        public async Task<ActionResult<IEnumerable<Pilote>>> GetPilotes()
        {
            return await _context.Pilotes.ToListAsync();
        }

        // GET: api/Pilotes/5
        [HttpGet("{id}")]
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
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        [HttpPut("{id}")]
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
                if (!PiloteExists(id))
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

        // POST: api/Pilotes
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        [HttpPost]
        public async Task<ActionResult<Pilote>> PostPilote(Pilote pilote)
        {
            _context.Pilotes.Add(pilote);
            await _context.SaveChangesAsync();

            return CreatedAtAction("GetPilote", new { id = pilote.Id }, pilote);
        }

        // DELETE: api/Pilotes/5
        [HttpDelete("{id}")]
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
