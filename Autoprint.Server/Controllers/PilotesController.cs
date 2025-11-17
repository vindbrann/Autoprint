using Autoprint.Server.Data;
using Autoprint.Server.Services; // Pour SettingsService et DriverService
using Autoprint.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Autoprint.Server.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(Policy = "DRIVER_READ")]
    public class PilotesController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly ISettingsService _settingsService;
        private readonly IDriverService _driverService;

        public PilotesController(ApplicationDbContext context, ISettingsService settingsService, IDriverService driverService)
        {
            _context = context;
            _settingsService = settingsService;
            _driverService = driverService;
        }

        // POST: api/Pilotes/Upload
        [HttpPost("Upload")]
        [Authorize(Policy = "DRIVER_WRITE")]
        public async Task<IActionResult> Upload(IFormFile file, [FromForm] string nomPilote, [FromForm] string version)
        {
            if (file == null || file.Length == 0) return BadRequest("Fichier vide.");

            try
            {
                // 1. On demande au service : "Où est le dossier des drivers ?"
                string rootPath = await _settingsService.GetDriversPathAsync();

                // 2. On crée un chemin propre : Racine / Nom / Version
                // On remplace les caractères interdits par des "_"
                string safeName = string.Join("_", (nomPilote ?? "Inconnu").Split(Path.GetInvalidFileNameChars()));
                string safeVersion = string.Join("_", (version ?? "1.0").Split(Path.GetInvalidFileNameChars()));

                string folderPath = Path.Combine(rootPath, safeName, safeVersion);
                Directory.CreateDirectory(folderPath);

                string fullPath = Path.Combine(folderPath, file.FileName);

                // 3. On sauvegarde le fichier physiquement
                using (var stream = new FileStream(fullPath, FileMode.Create))
                {
                    await file.CopyToAsync(stream);
                }

                // 4. TODO : Calculer le vrai hash ici plus tard
                string checksum = "SHA256_PENDING";

                return Ok(new { CheminFichier = fullPath, Checksum = checksum });
            }
            catch (Exception ex)
            {
                return StatusCode(500, "Erreur upload : " + ex.Message);
            }
        }

        // POST: api/Pilotes/Install/5
        [HttpPost("Install/{id}")]
        [Authorize(Policy = "DRIVER_INSTALL")]
        public async Task<IActionResult> InstallDriver(int id)
        {
            var pilote = await _context.Pilotes.FindAsync(id);
            if (pilote == null) return NotFound();

            if (!System.IO.File.Exists(pilote.CheminFichier))
                return BadRequest($"Fichier introuvable sur le disque : {pilote.CheminFichier}");

            // On tente l'installation via PnPUtil
            bool success = await _driverService.InstallerPiloteAsync(pilote.CheminFichier);

            if (success)
            {
                pilote.EstInstalle = true;
                await _context.SaveChangesAsync();
                return Ok(new { Message = "Installation réussie." });
            }

            return StatusCode(500, "Echec de l'installation système.");
        }

        // POST: api/Pilotes/Uninstall/5
        [HttpPost("Uninstall/{id}")]
        [Authorize(Policy = "DRIVER_UNINSTALL")]
        public async Task<IActionResult> UninstallDriver(int id)
        {
            var pilote = await _context.Pilotes.FindAsync(id);
            if (pilote == null) return NotFound();

            // Sécurité : Est-ce qu'un modèle utilise ce pilote ?
            bool estUtilise = await _context.Modeles.AnyAsync(m => m.PiloteId == id);
            if (estUtilise) return BadRequest("Impossible : Ce pilote est utilisé par un modèle.");

            // Simulation désinstallation (À améliorer avec le vrai nom oemXX.inf plus tard)
            pilote.EstInstalle = false;
            await _context.SaveChangesAsync();

            return Ok(new { Message = "Pilote désinstallé." });
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
            if (pilote == null) return NotFound();
            return pilote;
        }

        // POST: api/Pilotes (Création en base après upload)
        [HttpPost]
        [Authorize(Policy = "DRIVER_WRITE")]
        public async Task<ActionResult<Pilote>> PostPilote(Pilote pilote)
        {
            _context.Pilotes.Add(pilote);
            await _context.SaveChangesAsync();
            return CreatedAtAction("GetPilote", new { id = pilote.Id }, pilote);
        }

        // PUT: api/Pilotes/5
        [HttpPut("{id}")]
        [Authorize(Policy = "DRIVER_WRITE")]
        public async Task<IActionResult> PutPilote(int id, Pilote pilote)
        {
            if (id != pilote.Id) return BadRequest();
            _context.Entry(pilote).State = EntityState.Modified;
            await _context.SaveChangesAsync();
            return NoContent();
        }

        // DELETE: api/Pilotes/5
        [HttpDelete("{id}")]
        [Authorize(Policy = "DRIVER_DELETE")]
        public async Task<IActionResult> DeletePilote(int id)
        {
            // Sécurité
            bool estUtilise = await _context.Modeles.AnyAsync(m => m.PiloteId == id);
            if (estUtilise) return BadRequest("Ce pilote est utilisé par un modèle.");

            var pilote = await _context.Pilotes.FindAsync(id);
            if (pilote == null) return NotFound();

            // Suppression physique optionnelle (à voir selon tes règles métier)
            /* if (System.IO.File.Exists(pilote.CheminFichier)) System.IO.File.Delete(pilote.CheminFichier); */

            _context.Pilotes.Remove(pilote);
            await _context.SaveChangesAsync();
            return NoContent();
        }
    }
}