using Autoprint.Server.Data;
using Autoprint.Server.Services;
using Autoprint.Shared;
using Autoprint.Shared.DTOs; // Assure-toi que tes DTOs sont bien là
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.DirectoryServices; // Pour le test AD avancé
using System.DirectoryServices.AccountManagement; // Pour le test AD simple

namespace Autoprint.Server.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class SettingsController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly IEmailService _emailService;

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
            // 1. On charge l'état ACTUEL pour comparer (Avant modif)
            var currentSettings = await _context.ServerSettings.ToDictionaryAsync(s => s.Key, s => s.Value);
            var changes = new List<string>();

            // Petite fonction locale pour comparer proprement
            void Check(string key, string newValue, string label, bool isSecret = false)
            {
                string oldValue = currentSettings.ContainsKey(key) ? currentSettings[key] : "";
                if (newValue == null) newValue = ""; // On évite les nulls

                if (oldValue != newValue)
                {
                    if (isSecret)
                        changes.Add($"{label} modifié (valeur masquée)");
                    else
                        changes.Add($"{label}: '{oldValue}' -> '{newValue}'");
                }
            }

            // 2. Comparaison de TOUS les champs pour les logs
            Check("LogRetentionDays", dto.LogRetentionDays.ToString(), "Rétention Logs");

            Check("SmtpHost", dto.SmtpHost, "SMTP Host");
            Check("SmtpPort", dto.SmtpPort.ToString(), "SMTP Port");
            Check("SmtpUser", dto.SmtpUser, "SMTP User");
            if (!string.IsNullOrEmpty(dto.SmtpPass)) Check("SmtpPass", "changed", "SMTP Password", true);
            Check("SmtpEnableSsl", dto.SmtpEnableSsl.ToString(), "SMTP SSL");
            Check("SmtpFromAddress", dto.SmtpFromAddress, "SMTP From");

            Check("NamingTemplate", dto.NamingTemplate, "Template Nom");
            Check("NamingEnabled", dto.NamingEnabled.ToString(), "Nommage Auto");
            Check("NamingSameShare", dto.NamingSameShare.ToString(), "Nom Partage");
            Check("PasswordExpirationDays", dto.PasswordExpirationDays.ToString(), "Expiration MDP");

            Check("AdDomain", dto.AdDomain, "AD Domaine");
            Check("AdBaseDn", dto.AdBaseDn, "AD BaseDN");
            Check("AdLdapFilter", dto.AdLdapFilter, "AD Filtre");
            Check("AdUseServiceAccount", dto.AdUseServiceAccount.ToString(), "AD Service Account");
            Check("AdServiceUser", dto.AdServiceUser, "AD User");
            if (!string.IsNullOrEmpty(dto.AdServicePassword)) Check("AdServicePassword", "changed", "AD Password", true);
            Check("AdAdminEmails", dto.AdAdminEmails, "Mails Alertes");


            // 3. Sauvegarde réelle en Base de Données

            // Rétention
            if (dto.LogRetentionDays > 0) await UpdateSetting("LogRetentionDays", dto.LogRetentionDays.ToString());

            // SMTP
            await UpdateSetting("SmtpHost", dto.SmtpHost);
            await UpdateSetting("SmtpPort", dto.SmtpPort.ToString());
            await UpdateSetting("SmtpUser", dto.SmtpUser);
            // On ne met à jour le mot de passe que s'il est fourni (pas vide)
            if (!string.IsNullOrEmpty(dto.SmtpPass)) await UpdateSetting("SmtpPass", dto.SmtpPass);
            await UpdateSetting("SmtpEnableSsl", dto.SmtpEnableSsl.ToString());
            await UpdateSetting("SmtpFromAddress", dto.SmtpFromAddress);

            // Nommage & Sécurité
            await UpdateSetting("NamingTemplate", dto.NamingTemplate);
            await UpdateSetting("NamingEnabled", dto.NamingEnabled.ToString());
            await UpdateSetting("NamingSameShare", dto.NamingSameShare.ToString());
            if (dto.PasswordExpirationDays >= 0) await UpdateSetting("PasswordExpirationDays", dto.PasswordExpirationDays.ToString());

            // Active Directory
            await UpdateSetting("AdDomain", dto.AdDomain);
            await UpdateSetting("AdBaseDn", dto.AdBaseDn);
            await UpdateSetting("AdLdapFilter", dto.AdLdapFilter);
            await UpdateSetting("AdUseServiceAccount", dto.AdUseServiceAccount.ToString());
            await UpdateSetting("AdServiceUser", dto.AdServiceUser);
            if (!string.IsNullOrEmpty(dto.AdServicePassword)) await UpdateSetting("AdServicePassword", dto.AdServicePassword);
            await UpdateSetting("AdAdminEmails", dto.AdAdminEmails);


            // 4. Enregistrement du Log d'Audit (si changements détectés)
            if (changes.Any())
            {
                _context.AuditLogs.Add(new AuditLog
                {
                    Action = "CONFIG_UPDATE",
                    Details = string.Join(" | ", changes), // On concatène les modifs
                    Utilisateur = User.Identity?.Name ?? "Unknown",
                    Niveau = "WARNING",
                    DateAction = DateTime.UtcNow
                });
                await _context.SaveChangesAsync();
            }

            return Ok(new { Message = "Configuration sauvegardée avec succès." });
        }

        // --- HELPER POUR METTRE A JOUR UNE CLE ---
        private async Task UpdateSetting(string key, string value)
        {
            var setting = await _context.ServerSettings.FindAsync(key);
            if (setting != null)
                setting.Value = value ?? "";
            else
                _context.ServerSettings.Add(new ServerSetting { Key = key, Value = value ?? "", Description = "Auto-generated" });
        }

        // --- ENDPOINTS DE TEST ---

        [HttpPost("TestEmail")]
        [Authorize(Policy = "SETTINGS_MANAGE")]
        public async Task<IActionResult> TestEmail([FromBody] TestEmailDto dto)
        {
            try
            {
                await _emailService.SendTestEmailAsync(dto.Host, dto.Port, dto.User, dto.Password, dto.Ssl, dto.From, dto.To);
                return Ok(new { Message = "Connexion SMTP réussie !" });
            }
            catch (Exception ex) { return BadRequest("Echec du test SMTP : " + ex.Message); }
        }

        [HttpPost("TestAd")]
        [Authorize(Policy = "SETTINGS_MANAGE")]
        public IActionResult TestAd([FromBody] TestAdDto dto)
        {
            if (!OperatingSystem.IsWindows())
                return BadRequest("Le serveur doit tourner sous Windows.");

            try
            {
                ContextType contextType = ContextType.Domain;
                // Gestion du mot de passe vide (récupération en BDD)
                string finalPass = dto.ServicePassword;
                if (dto.UseServiceAccount && string.IsNullOrEmpty(finalPass))
                {
                    var saved = _context.ServerSettings.Find("AdServicePassword");
                    if (saved != null) finalPass = saved.Value;
                }

                using var context = dto.UseServiceAccount
                    ? new PrincipalContext(contextType, dto.Domain, null, dto.ServiceUser, finalPass)
                    : new PrincipalContext(contextType, dto.Domain);

                var server = context.ConnectedServer;
                return Ok(new { Message = $"Succès ! Connecté au contrôleur : {server}" });
            }
            catch (Exception ex) { return BadRequest($"Échec connexion AD : {ex.Message}"); }
        }

        [HttpPost("TestAdFilter")]
        [Authorize(Policy = "SETTINGS_MANAGE")]
        public IActionResult TestAdFilter([FromBody] TestAdFilterDto dto)
        {
            if (!OperatingSystem.IsWindows()) return Ok(new { Success = false, Message = "Windows requis." });

            try
            {
                string finalPass = dto.ServicePassword;
                if (dto.UseServiceAccount && string.IsNullOrEmpty(finalPass))
                {
                    var saved = _context.ServerSettings.Find("AdServicePassword");
                    if (saved != null) finalPass = saved.Value;
                }

                string ldapPath = string.IsNullOrEmpty(dto.BaseDn) ? $"LDAP://{dto.Domain}" : $"LDAP://{dto.Domain}/{dto.BaseDn}";

                using DirectoryEntry entry = dto.UseServiceAccount
                    ? new DirectoryEntry(ldapPath, dto.ServiceUser, finalPass)
                    : new DirectoryEntry(ldapPath);

                using DirectorySearcher searcher = new DirectorySearcher(entry);

                string configFilter = string.IsNullOrWhiteSpace(dto.Filter) ? "(objectClass=user)" : dto.Filter;

                if (!string.IsNullOrWhiteSpace(dto.TestUserQuery))
                    searcher.Filter = $"(&{configFilter}(sAMAccountName={dto.TestUserQuery}))";
                else
                    searcher.Filter = configFilter;

                searcher.SizeLimit = 1;
                var result = searcher.FindOne();

                if (result == null)
                {
                    if (!string.IsNullOrWhiteSpace(dto.TestUserQuery))
                        return Ok(new { Success = false, Message = $"❌ L'utilisateur '{dto.TestUserQuery}' est INTROUVABLE avec ce filtre." });
                    else
                        return Ok(new { Success = false, Message = "⚠️ Syntaxe valide, mais aucun objet trouvé." });
                }

                string name = result.Properties["name"].Count > 0 ? result.Properties["name"][0].ToString()! : "Inconnu";
                return Ok(new { Success = true, Message = $"✅ SUCCÈS : Objet '{name}' trouvé !" });
            }
            catch (Exception ex)
            {
                return Ok(new { Success = false, Message = $"💥 Erreur LDAP : {ex.Message}" });
            }
        }
    }

    // --- DTOs (Objets de transport) ---

    public class SettingsUpdateDto
    {
        // NOUVEAU : Rétention
        public int LogRetentionDays { get; set; } = 180;

        // SMTP
        public string SmtpHost { get; set; } = "";
        public int SmtpPort { get; set; } = 25;
        public string SmtpUser { get; set; } = "";
        public string SmtpPass { get; set; } = "";
        public bool SmtpEnableSsl { get; set; } = false;
        public string SmtpFromAddress { get; set; } = "";

        // Naming
        public string NamingTemplate { get; set; } = "";
        public bool NamingEnabled { get; set; } = false;
        public bool NamingSameShare { get; set; } = false;

        // Security
        public int PasswordExpirationDays { get; set; }

        // AD
        public string AdDomain { get; set; } = "";
        public string AdBaseDn { get; set; } = "";
        public string AdLdapFilter { get; set; } = "";
        public bool AdUseServiceAccount { get; set; } = false;
        public string AdServiceUser { get; set; } = "";
        public string AdServicePassword { get; set; } = "";
        public string AdAdminEmails { get; set; } = "";
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

    public class TestAdDto
    {
        public string Domain { get; set; } = "";
        public bool UseServiceAccount { get; set; }
        public string ServiceUser { get; set; } = "";
        public string ServicePassword { get; set; } = "";
    }

    public class TestAdFilterDto
    {
        public string Domain { get; set; } = "";
        public string BaseDn { get; set; } = "";
        public string Filter { get; set; } = "";
        public string TestUserQuery { get; set; } = "";
        public bool UseServiceAccount { get; set; }
        public string ServiceUser { get; set; } = "";
        public string ServicePassword { get; set; } = "";
    }
}