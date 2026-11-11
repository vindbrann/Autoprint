using Autoprint.Server.Data;
using Autoprint.Server.Services;
using Autoprint.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Autoprint.Server.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class NamingController : ControllerBase
    {
        private readonly INamingService _namingService;
        private readonly ApplicationDbContext _context;

        public NamingController(INamingService namingService, ApplicationDbContext context)
        {
            _namingService = namingService;
            _context = context;
        }

        [HttpPost("Preview")]
        [Authorize(Policy = "PRINTER_WRITE")]
        public ActionResult<string> Preview([FromBody] NamingPreviewDto dto)
        {
            var fakeImp = new Imprimante
            {
                AdresseIp = dto.Ip,
                Code = dto.ImpCode,
                Emplacement = new Emplacement { Nom = dto.LieuNom, Code = dto.LieuCode },
                Modele = new Modele { Nom = dto.ModeleNom, Marque = new Marque { Nom = dto.MarqueNom } }
            };

            string resultat = _namingService.GenererNom(fakeImp, dto.Template);
            return Ok(new { GeneratedName = resultat });
        }

        /// <summary>
        /// Renommages qu'entraînerait une règle (enregistrée ou non), sans rien modifier : la console les affiche
        /// avant l'application au parc (maquette « nommage », retour de recette du 11/11).
        /// </summary>
        [HttpPost("PreviewChanges")]
        [Authorize(Policy = "SETTINGS_MANAGE")]
        public async Task<ActionResult<List<NamingChangeDto>>> PreviewChanges([FromBody] NamingChangesRequestDto dto)
        {
            var imprimantes = await _context.Imprimantes.AsNoTracking()
                .Include(i => i.Emplacement)
                .Include(i => i.Modele).ThenInclude(m => m!.Marque)
                .OrderBy(i => i.NomAffiche)
                .ToListAsync();

            var changes = new List<NamingChangeDto>();
            foreach (var imp in imprimantes)
            {
                string nouveauNom = _namingService.GenererNom(imp, dto.Template);
                bool nameChanges = !string.IsNullOrEmpty(nouveauNom) && imp.NomAffiche != nouveauNom;
                bool shareChanges = dto.SameShare && imp.EstPartagee && !string.IsNullOrEmpty(nouveauNom) && imp.NomPartage != nouveauNom;
                if (!nameChanges && !shareChanges) continue;
                changes.Add(new NamingChangeDto
                {
                    Id = imp.Id,
                    CurrentName = imp.NomAffiche,
                    NewName = nouveauNom,
                    CurrentShare = imp.EstPartagee ? imp.NomPartage : null,
                    NewShare = shareChanges ? nouveauNom : null,
                    IsArchived = imp.IsArchived
                });
            }
            return Ok(changes);
        }
        [HttpPost("ApplyToNames")]
        [Authorize(Policy = "SETTINGS_MANAGE")]
        public async Task<ActionResult> ApplyToNames()
        {
            var imprimantes = await _context.Imprimantes
                .Include(i => i.Emplacement)
                .Include(i => i.Modele).ThenInclude(m => m!.Marque)
                .ToListAsync();

            int count = 0;
            foreach (var imp in imprimantes)
            {
                string nouveauNom = await _namingService.GenererNomAsync(imp);
                if (imp.NomAffiche != nouveauNom)
                {
                    imp.NomAffiche = nouveauNom;
                    count++;
                }
            }

            await _context.SaveChangesAsync();
            return Ok(new { Message = $"{count} noms d'imprimantes mis à jour." });
        }

        [HttpPost("ApplyToShares")]
        [Authorize(Policy = "SETTINGS_MANAGE")]
        public async Task<ActionResult> ApplyToShares()
        {
            var imprimantes = await _context.Imprimantes
                .Include(i => i.Emplacement)
                .Include(i => i.Modele).ThenInclude(m => m!.Marque)
                .Where(i => i.EstPartagee)
                .ToListAsync();

            int count = 0;
            foreach (var imp in imprimantes)
            {
                string nouveauNom = await _namingService.GenererNomAsync(imp);

                if (imp.NomPartage != nouveauNom)
                {
                    imp.NomPartage = nouveauNom;
                    count++;
                }
            }

            await _context.SaveChangesAsync();
            return Ok(new { Message = $"{count} noms de partages mis à jour." });
        }
    }

    public class NamingChangesRequestDto
    {
        public string Template { get; set; } = "";
        public bool SameShare { get; set; }
    }

    public class NamingChangeDto
    {
        public int Id { get; set; }
        public string CurrentName { get; set; } = "";
        public string NewName { get; set; } = "";
        public string? CurrentShare { get; set; }
        public string? NewShare { get; set; }
        public bool IsArchived { get; set; }
    }

    public class NamingPreviewDto
    {
        public string Template { get; set; } = "";
        public string Ip { get; set; } = "";
        public string LieuNom { get; set; } = "";
        public string LieuCode { get; set; } = "";
        public string ModeleNom { get; set; } = "";
        public string MarqueNom { get; set; } = "";
        public string ImpCode { get; set; } = "";
    }
}