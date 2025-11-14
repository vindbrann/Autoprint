using Autoprint.Server.Data;
using Autoprint.Server.Models;
using Autoprint.Server.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Autoprint.Server.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class SettingsController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly IEmailService _emailService;

        public SettingsController(ApplicationDbContext context, IEmailService emailService)
        {
            _context = context;
            _emailService = emailService;
        }

        public SettingsController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: api/Settings
        // Retourne toute la configuration
        [HttpGet]
        public async Task<ActionResult<IEnumerable<ServerSetting>>> GetSettings()
        {
            return await _context.ServerSettings.ToListAsync();
        }

        // PUT: api/Settings/SmtpHost
        // Modifie une valeur spécifique
        [HttpPut("{key}")]
        public async Task<IActionResult> UpdateSetting(string key, ServerSetting setting)
        {
            // Sécurité : on vérifie que la clé de l'URL correspond à l'objet envoyé
            if (key != setting.Key) return BadRequest();

            var existing = await _context.ServerSettings.FindAsync(key);
            if (existing == null) return NotFound();

            // On garde l'ancienne valeur pour le log
            string ancienneValeur = existing.Value;

            // On met à jour uniquement la valeur (on ne touche pas à la Description ou au Type)
            existing.Value = setting.Value;

            // Traçabilité : On enregistre qui a modifié quoi
            // Attention : Si c'est un mot de passe, on évite de l'écrire en clair dans les logs !
            string detailsLog = existing.Type == "PASSWORD"
                ? $"Modification du mot de passe {key}"
                : $"Modification de {key} : '{ancienneValeur}' -> '{setting.Value}'";

            _context.AuditLogs.Add(new AuditLog
            {
                Action = "CONFIG_CHANGE",
                Details = detailsLog,
                Utilisateur = "Admin", // Sera remplacé par le vrai user connecté plus tard
                Niveau = "WARNING"
            });

            await _context.SaveChangesAsync();

            return NoContent();
        }

        // POST: api/Settings/TestEmail
        [HttpPost("TestEmail")]
        public async Task<IActionResult> SendTestEmail(string emailDestinataire)
        {
            try
            {
                await _emailService.SendEmailAsync(
                    emailDestinataire,
                    "Test Autoprint",
                    "<h1>Ceci est un test</h1><p>Si vous lisez ça, la config SMTP fonctionne !</p>"
                );
                return Ok("Email envoyé avec succès.");
            }
            catch (Exception ex)
            {
                return BadRequest($"Erreur d'envoi : {ex.Message}");
            }
        }
    }
}