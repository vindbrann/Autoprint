using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Autoprint.Server.Data;
using Autoprint.Server.Services;
using Autoprint.Server.DTOs;
using Autoprint.Shared;

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
                // On charge le Modèle ET le Pilote associé à ce modèle
                .Include(i => i.Modele)
                    .ThenInclude(m => m.Pilote)
                .ToListAsync();
        }

        // GET: api/Imprimantes/5
        [HttpGet("{id}")]
        public async Task<ActionResult<Imprimante>> GetImprimante(int id)
        {
            var imprimante = await _context.Imprimantes
                .Include(i => i.Emplacement)
                .Include(i => i.Modele)
                    .ThenInclude(m => m.Pilote)
                .FirstOrDefaultAsync(i => i.Id == id);

            if (imprimante == null) return NotFound();

            return imprimante;
        }

        // POST: api/Imprimantes
        [HttpPost]
        public async Task<ActionResult<Imprimante>> PostImprimante(Imprimante imprimante)
        {
            _context.Imprimantes.Add(imprimante);
            await _context.SaveChangesAsync();

            // On recharge l'objet complet pour le renvoyer au client (avec les noms de Modèle/Emplacement)
            // C'est ce qui permet d'afficher "HP" au lieu de "ID 4" juste après l'ajout
            var newImp = await _context.Imprimantes
                .Include(i => i.Modele).ThenInclude(m => m.Pilote)
                .Include(i => i.Emplacement)
                .FirstOrDefaultAsync(i => i.Id == imprimante.Id);

            return CreatedAtAction("GetImprimante", new { id = imprimante.Id }, newImp);
        }

        // POST: api/Imprimantes/Batch
        [HttpPost("Batch")]
        public async Task<ActionResult<BatchResult>> PostBatch(List<ImprimanteDto> imprimantesDtos)
        {
            var resultat = new BatchResult { TotalTraites = imprimantesDtos.Count };

            foreach (var dto in imprimantesDtos)
            {
                try
                {
                    var imprimante = new Imprimante
                    {
                        NomAffiche = dto.NomAffiche,
                        AdresseIp = dto.AdresseIp,
                        EstPartagee = dto.EstPartagee,
                        NomPartage = dto.NomPartage,
                        Commentaire = dto.Commentaire,
                        EmplacementId = dto.EmplacementId,
                        Localisation = dto.Localisation,
                        ModeleId = dto.ModeleId
                        // PiloteId supprimé ici car il est maintenant lié au Modele
                    };

                    _context.Imprimantes.Add(imprimante);
                    resultat.SuccesBdd++;
                }
                catch (Exception ex)
                {
                    resultat.Erreurs++;
                    resultat.DetailsErreurs.Add($"Erreur BDD donnée '{dto.NomAffiche}' : {ex.Message}");
                }
            }

            await _context.SaveChangesAsync();
            return Ok(resultat);
        }

        // POST: api/Imprimantes/Synchroniser
        [HttpPost("Synchroniser")]
        public async Task<ActionResult<BatchResult>> SynchroniserServeur()
        {
            var resultat = new BatchResult();

            // Récupération de l'arbre complet : Imprimante -> Modele -> Pilote
            var imprimantesBdd = await _context.Imprimantes
                .Include(i => i.Modele)
                    .ThenInclude(m => m.Pilote)
                .ToListAsync();

            resultat.TotalTraites = imprimantesBdd.Count;

            foreach (var imp in imprimantesBdd)
            {
                try
                {
                    // Vérification : Est-ce que le modèle a bien un pilote ?
                    if (imp.Modele?.Pilote == null)
                    {
                        resultat.Erreurs++;
                        resultat.DetailsErreurs.Add($"Imprimante {imp.NomAffiche} ignorée : Modèle '{imp.Modele?.Nom}' sans pilote.");
                        continue;
                    }

                    // A. Création Port TCP/IP
                    _spoolerService.CreerPortTcp(imp.AdresseIp, imp.AdresseIp);

                    // B. Création Imprimante Windows
                    string commentaireComplet = $"{imp.Localisation} - {imp.Commentaire}";

                    _spoolerService.CreerImprimante(
                        nom: imp.NomAffiche,
                        nomDriver: imp.Modele.Pilote.Nom, // On va chercher le nom via le Modèle
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

                    _context.AuditLogs.Add(new AuditLog
                    {
                        Action = "SYNC_ERROR",
                        Details = ex.Message,
                        Niveau = "ERROR",
                        Utilisateur = "System"
                    });
                }
            }

            await _context.SaveChangesAsync();
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