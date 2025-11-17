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
    public class SettingsController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly IEmailService _emailService;
        private readonly ISettingsService _settingsService; // AJOUTÉ : Pour gérer les fichiers

        // On injecte les 3 services nécessaires
        public SettingsController(ApplicationDbContext context, IEmailService emailService, ISettingsService settingsService)
        {
            _context = context;
            _emailService = emailService;
            _settingsService = settingsService;
        }

        // GET: api/Settings
        [HttpGet]
        public async Task<ActionResult<IEnumerable<ServerSetting>>> GetSettings()
        {
            return await _context.ServerSettings.ToListAsync();
        }

        // POST: api/Settings/Save
        // C'est cette méthode que la nouvelle page "Paramètres" va appeler
        [HttpPost("Save")]
        public async Task<IActionResult> SaveSettings([FromBody] SettingsUpdateDto dto)
        {
            // 1. GESTION DU STOCKAGE (Spéciale à cause du déplacement physique)
            var currentPath = await _settingsService.GetDriversPathAsync();

            // Si le chemin a changé, on appelle le service intelligent
            if (!string.Equals(dto.DriverPath, currentPath, StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    // Le service s'occupe de déplacer les fichiers ET de mettre à jour la clé "DriverPath"
                    await _settingsService.UpdateDriversPathAsync(dto.DriverPath, dto.MoveFiles);
                }
                catch (Exception ex)
                {
                    return StatusCode(500, "Erreur critique lors du déplacement des fichiers : " + ex.Message);
                }
            }

            // 2. GESTION DU SMTP (Mise à jour classique des valeurs)
            await UpdateSetting("SmtpHost", dto.SmtpHost);
            await UpdateSetting("SmtpPort", dto.SmtpPort.ToString());
            await UpdateSetting("SmtpUser", dto.SmtpUser);
            await UpdateSetting("SmtpPass", dto.SmtpPass);          // Ta clé
            await UpdateSetting("SmtpEnableSsl", dto.SmtpEnableSsl.ToString()); // Ta clé
            await UpdateSetting("SmtpFromAddress", dto.SmtpFromAddress); // Ta clé

            await UpdateSetting("NamingTemplate", dto.NamingTemplate);
            await UpdateSetting("NamingEnabled", dto.NamingEnabled.ToString());
            await UpdateSetting("NamingSameShare", dto.NamingSameShare.ToString());
            // On loggue l'action globale
            _context.AuditLogs.Add(new AuditLog
            {
                Action = "CONFIG_UPDATE",
                Details = "Mise à jour globale de la configuration",
                Utilisateur = "Admin",
                Niveau = "WARNING"
            });

            await _context.SaveChangesAsync();
            return Ok(new { Message = "Configuration sauvegardée avec succès." });
        }

        // POST: api/Settings/TestEmail
        // Teste la config envoyée par le formulaire (Draft)
        [HttpPost("TestEmail")]
        public async Task<IActionResult> TestEmail([FromBody] TestEmailDto dto)
        {
            try
            {
                // On utilise la méthode DE TEST de l'interface (celle qui prend les args)
                await _emailService.SendTestEmailAsync(
                    dto.Host, dto.Port, dto.User, dto.Password, dto.Ssl, dto.From, dto.To
                );
                return Ok(new { Message = "Connexion SMTP réussie !" });
            }
            catch (Exception ex)
            {
                return BadRequest("Echec du test SMTP : " + ex.Message);
            }
        }

        // Helper pour mettre à jour une clé sans planter si elle n'existe pas
        private async Task UpdateSetting(string key, string value)
        {
            var setting = await _context.ServerSettings.FindAsync(key);
            if (setting != null)
            {
                // On ne touche qu'à la valeur
                setting.Value = value ?? "";
            }
        }
    }

    // --- LES DTOs (Objets de transfert de données) ---
    // Ils doivent correspondre exactement aux champs de ta page Razor

    public class SettingsUpdateDto
    {
        public string DriverPath { get; set; } = "";
        public bool MoveFiles { get; set; } = false;

        public string SmtpHost { get; set; } = "";
        public int SmtpPort { get; set; } = 25;
        public string SmtpUser { get; set; } = "";
        public string SmtpPass { get; set; } = "";
        public bool SmtpEnableSsl { get; set; } = false;
        public string SmtpFromAddress { get; set; } = "";
        public string NamingTemplate { get; set; } = "";
        public bool NamingEnabled { get; set; } = false;
        public bool NamingSameShare { get; set; } = false;
    }

    public class TestEmailDto
    {
        public string Host { get; set; } = "";
        public int Port { get; set; }
        public string User { get; set; } = "";
        public string Password { get; set; } = "";
        public bool Ssl { get; set; }
        public string From { get; set; } = "";
        public string To { get; set; } = "";
    }
}