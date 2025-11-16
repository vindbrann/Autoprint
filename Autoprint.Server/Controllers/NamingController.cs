using Microsoft.AspNetCore.Mvc;
using Autoprint.Server.Services;
using Autoprint.Shared;
using Autoprint.Server.Data;
using Microsoft.EntityFrameworkCore;

namespace Autoprint.Server.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class NamingController : ControllerBase
    {
        private readonly INamingService _namingService;
        private readonly ApplicationDbContext _context;

        public NamingController(INamingService namingService, ApplicationDbContext context)
        {
            _namingService = namingService;
            _context = context;
        }

        // POST: api/Naming/Preview
        [HttpPost("Preview")]
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

        // POST: api/Naming/ApplyToAll
        [HttpPost("ApplyToAll")]
        public async Task<ActionResult> ApplyToAll()
        {
            // 1. On récupère le réglage "Même nom de partage" de façon robuste
            var settingShare = await _context.ServerSettings.FindAsync("NamingSameShare");
            bool sameShare = false;
            if (settingShare != null)
            {
                bool.TryParse(settingShare.Value, out sameShare);
            }

            // 2. On récupère toutes les imprimantes
            var imprimantes = await _context.Imprimantes
                .Include(i => i.Emplacement)
                .Include(i => i.Modele).ThenInclude(m => m.Marque)
                .ToListAsync();

            int count = 0;

            foreach (var imp in imprimantes)
            {
                string nouveauNom = await _namingService.GenererNomAsync(imp);
                bool aEteModifie = false;

                // A. Mise à jour du Nom
                if (imp.NomAffiche != nouveauNom)
                {
                    imp.NomAffiche = nouveauNom;
                    aEteModifie = true;
                }

                // B. Mise à jour du Partage
                if (sameShare && imp.EstPartagee)
                {
                    // On force la mise à jour si le nom de partage est différent du nouveau nom
                    if (imp.NomPartage != nouveauNom)
                    {
                        imp.NomPartage = nouveauNom;
                        aEteModifie = true;
                    }
                }

                if (aEteModifie) count++;
            }

            await _context.SaveChangesAsync();
            return Ok(new { Message = $"{count} imprimantes (noms ou partages) ont été mises à jour." });
        }
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