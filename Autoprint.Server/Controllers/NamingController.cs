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
    [Authorize(Policy = "SETTINGS_MANAGE")]
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

        // POST: api/Naming/ApplyToNames (Renomme uniquement le NomAffiche)
        [HttpPost("ApplyToNames")]
        public async Task<ActionResult> ApplyToNames()
        {
            var imprimantes = await _context.Imprimantes
                .Include(i => i.Emplacement)
                .Include(i => i.Modele).ThenInclude(m => m.Marque)
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

        // POST: api/Naming/ApplyToShares (Renomme uniquement le NomPartage pour celles qui sont partagées)
        [HttpPost("ApplyToShares")]
        public async Task<ActionResult> ApplyToShares()
        {
            var imprimantes = await _context.Imprimantes
                .Include(i => i.Emplacement)
                .Include(i => i.Modele).ThenInclude(m => m.Marque)
                .Where(i => i.EstPartagee) // On ne touche qu'aux partagées
                .ToListAsync();

            int count = 0;
            foreach (var imp in imprimantes)
            {
                string nouveauNom = await _namingService.GenererNomAsync(imp);

                // On applique le nouveau nom comme nom de partage
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