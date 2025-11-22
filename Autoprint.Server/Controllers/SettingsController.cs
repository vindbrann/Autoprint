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
    public class SettingsController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly IEmailService _emailService;

        // On a retiré ISettingsService du constructeur
        public SettingsController(ApplicationDbContext context, IEmailService emailService)
        {
            _context = context;
            _emailService = emailService;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<ServerSetting>>> GetSettings()
        {
            return await _context.ServerSettings.ToListAsync();
        }

        [HttpPost("Save")]
        [Authorize(Policy = "SETTINGS_MANAGE")]
        public async Task<IActionResult> SaveSettings([FromBody] SettingsUpdateDto dto)
        {
            // PLUS DE GESTION DE CHEMIN (DriverPath) ICI

            // 1. GESTION DU SMTP
            await UpdateSetting("SmtpHost", dto.SmtpHost);
            await UpdateSetting("SmtpPort", dto.SmtpPort.ToString());
            await UpdateSetting("SmtpUser", dto.SmtpUser);
            await UpdateSetting("SmtpPass", dto.SmtpPass);
            await UpdateSetting("SmtpEnableSsl", dto.SmtpEnableSsl.ToString());
            await UpdateSetting("SmtpFromAddress", dto.SmtpFromAddress);

            // 2. GESTION NOMMAGE & SÉCURITÉ
            await UpdateSetting("NamingTemplate", dto.NamingTemplate);
            await UpdateSetting("NamingEnabled", dto.NamingEnabled.ToString());
            await UpdateSetting("NamingSameShare", dto.NamingSameShare.ToString());

            if (dto.PasswordExpirationDays > 0 || dto.PasswordExpirationDays == 0)
            {
                await UpdateSetting("PasswordExpirationDays", dto.PasswordExpirationDays.ToString());
            }

            _context.AuditLogs.Add(new AuditLog
            {
                Action = "CONFIG_UPDATE",
                Details = "Mise à jour globale de la configuration",
                Utilisateur = User.Identity?.Name ?? "Unknown",
                Niveau = "WARNING"
            });

            await _context.SaveChangesAsync();
            return Ok(new { Message = "Configuration sauvegardée avec succès." });
        }

        [HttpPost("TestEmail")]
        [Authorize(Policy = "SETTINGS_MANAGE")]
        public async Task<IActionResult> TestEmail([FromBody] TestEmailDto dto)
        {
            try
            {
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

        private async Task UpdateSetting(string key, string value)
        {
            var setting = await _context.ServerSettings.FindAsync(key);
            if (setting != null)
            {
                setting.Value = value ?? "";
            }
            else
            {
                _context.ServerSettings.Add(new ServerSetting { Key = key, Value = value ?? "", Description = "Auto-generated" });
            }
        }
    }

    public class SettingsUpdateDto
    {
        // DriverPath et MoveFiles SUPPRIMÉS
        public string SmtpHost { get; set; } = "";
        public int SmtpPort { get; set; } = 25;
        public string SmtpUser { get; set; } = "";
        public string SmtpPass { get; set; } = "";
        public bool SmtpEnableSsl { get; set; } = false;
        public string SmtpFromAddress { get; set; } = "";
        public string NamingTemplate { get; set; } = "";
        public bool NamingEnabled { get; set; } = false;
        public bool NamingSameShare { get; set; } = false;
        public int PasswordExpirationDays { get; set; }
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