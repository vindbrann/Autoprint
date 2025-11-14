using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Autoprint.Server.Data;
using Autoprint.Server.Models;
using Autoprint.Server.Services;
using Autoprint.Server.DTOs;

namespace Autoprint.Server.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ImprimantesController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly IPrintSpoolerService _spoolerService;

        public ImprimantesController(ApplicationDbContext context, IPrintSpoolerService spoolerService)
        {
            _context = context;
            _spoolerService = spoolerService;
        }

        // GET: api/Imprimantes
        [HttpGet]
        public async Task<ActionResult<IEnumerable<Imprimante>>> GetImprimantes()
        {
            return await _context.Imprimantes
                .Include(i => i.Emplacement)
                .Include(i => i.Modele)
                .Include(i => i.Pilote)
                .ToListAsync();
        }

        // GET: api/Imprimantes/5
        [HttpGet("{id}")]
        public async Task<ActionResult<Imprimante>> GetImprimante(int id)
        {
            var imprimante = await _context.Imprimantes
                .Include(i => i.Emplacement)
                .Include(i => i.Modele)
                .Include(i => i.Pilote)
                .FirstOrDefaultAsync(i => i.Id == id);

            if (imprimante == null) return NotFound();

            return imprimante;
        }

        // POST: api/Imprimantes
        // Ajout simple en BDD uniquement (Rapide)
        [HttpPost]
        public async Task<ActionResult<Imprimante>> PostImprimante(Imprimante imprimante)
        {
            _context.Imprimantes.Add(imprimante);
            await _context.SaveChangesAsync();

            return CreatedAtAction("GetImprimante", new { id = imprimante.Id }, imprimante);
        }

        // POST: api/Imprimantes/Batch
        // Import de masse en BDD uniquement (Rapide)
        [HttpPost("Batch")]
        public async Task<ActionResult<BatchResult>> PostBatch(List<ImprimanteDto> imprimantesDtos)
        {
            var resultat = new BatchResult { TotalTraites = imprimantesDtos.Count };

            foreach (var dto in imprimantesDtos)
            {
                try
                {
                    // 1. Conversion DTO -> Entité
                    var imprimante = new Imprimante
                    {
                        NomAffiche = dto.NomAffiche,
                        AdresseIp = dto.AdresseIp,
                        EstPartagee = dto.EstPartagee,
                        NomPartage = dto.NomPartage,
                        Commentaire = dto.Commentaire,
                        EstParDefaut = dto.EstParDefaut,
                        EmplacementId = dto.EmplacementId,
                        Localisation = dto.Localisation,
                        ModeleId = dto.ModeleId,
                        PiloteId = dto.PiloteId
                    };

                    // 2. Sauvegarde BDD UNIQUEMENT (On ne touche pas à Windows ici)
                    _context.Imprimantes.Add(imprimante);
                    resultat.SuccesBdd++;
                }
                catch (Exception ex)
                {
                    resultat.Erreurs++;
                    resultat.DetailsErreurs.Add($"Erreur BDD donnée '{dto.NomAffiche}' : {ex.Message}");
                }
            }

            // Sauvegarde en un seul bloc
            await _context.SaveChangesAsync();

            return Ok(resultat);
        }

        // POST: api/Imprimantes/Synchroniser
        // C'est le bouton "Appliquer la configuration au serveur"
        [HttpPost("Synchroniser")]
        public async Task<ActionResult<BatchResult>> SynchroniserServeur()
        {
            var resultat = new BatchResult();

            // On récupère TOUT ce qu'il y a en base
            var imprimantesBdd = await _context.Imprimantes
                .Include(i => i.Pilote)
                .ToListAsync();

            resultat.TotalTraites = imprimantesBdd.Count;

            foreach (var imp in imprimantesBdd)
            {
                try
                {
                    if (imp.Pilote == null)
                    {
                        resultat.Erreurs++;
                        resultat.DetailsErreurs.Add($"Imprimante {imp.NomAffiche} ignorée : Pas de pilote.");
                        continue;
                    }

                    // A. Création Port
                    _spoolerService.CreerPortTcp(imp.AdresseIp, imp.AdresseIp);

                    // B. Création Imprimante Windows
                    string commentaireComplet = $"{imp.Localisation} - {imp.Commentaire}";

                    _spoolerService.CreerImprimante(
                        nom: imp.NomAffiche,
                        nomDriver: imp.Pilote.Nom,
                        nomPort: imp.AdresseIp,
                        commentaire: commentaireComplet,
                        nomPartage: imp.EstPartagee ? (imp.NomPartage ?? "") : ""
                    );

                    resultat.SuccesSysteme++;
                }
                catch (Exception ex)
                {
                    resultat.Erreurs++;
                    resultat.DetailsErreurs.Add($"Erreur Système '{imp.NomAffiche}' : {ex.Message}");

                    // Log audit
                    _context.AuditLogs.Add(new AuditLog
                    {
                        Action = "SYNC_ERROR",
                        Details = ex.Message,
                        Niveau = "ERROR",
                        Utilisateur = "System"
                    });
                }
            }

            await _context.SaveChangesAsync(); // Sauvegarde des logs
            return Ok(resultat);
        }

        // PUT: api/Imprimantes/5
        [HttpPut("{id}")]
        public async Task<IActionResult> PutImprimante(int id, Imprimante imprimante)
        {
            if (id != imprimante.Id) return BadRequest();

            _context.Entry(imprimante).State = EntityState.Modified;

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
        public async Task<IActionResult> DeleteImprimante(int id)
        {
            var imprimante = await _context.Imprimantes.FindAsync(id);
            if (imprimante == null) return NotFound();

            // Suppression Système Directe (Pour éviter les imprimantes fantômes)
            try
            {
                _spoolerService.SupprimerImprimante(imprimante.NomAffiche);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erreur suppression Windows : {ex.Message}");
            }

            _context.Imprimantes.Remove(imprimante);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        private bool ImprimanteExists(int id)
        {
            return _context.Imprimantes.Any(e => e.Id == id);
        }
    }
}