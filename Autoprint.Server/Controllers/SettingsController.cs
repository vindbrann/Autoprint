using Autoprint.Server.Data;
using Autoprint.Server.Helpers;
using Autoprint.Server.Services;
using Autoprint.Shared;
using Autoprint.Shared.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.DirectoryServices;
using System.DirectoryServices.AccountManagement;

namespace Autoprint.Server.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class SettingsController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly IEmailService _emailService;
        private readonly AuditService _auditService;
        private readonly SecretProtector _secrets;

        public SettingsController(ApplicationDbContext context, IEmailService emailService, AuditService auditService, SecretProtector secrets)
        {
            _context = context;
            _emailService = emailService;
            _auditService = auditService;
            _secrets = secrets;
        }

        [HttpGet]
        [Authorize(Policy = "SETTINGS_MANAGE")]
        public async Task<ActionResult<IEnumerable<ServerSetting>>> GetSettings()
        {
            var settings = await _context.ServerSettings.AsNoTracking().ToListAsync();
            foreach (var s in settings)
            {
                // Liste unique des secrets (SecretProtector) : tout nouveau secret est masqué automatiquement
                if (SecretProtector.IsSecret(s.Key) && !string.IsNullOrEmpty(s.Value))
                {
                    s.Value = "●●●●●●●●";
                }
            }

            return settings;
        }

        // Lecture de la clé agent en clair, à la demande et tracée dans l'audit (la liste des paramètres la masque)
        [HttpGet("agent-key")]
        [Authorize(Policy = "SETTINGS_MANAGE")]
        public async Task<IActionResult> RevealAgentKey()
        {
            var key = _secrets.Unprotect(await _context.ServerSettings.Where(s => s.Key == "AgentApiKey").Select(s => s.Value).FirstOrDefaultAsync());
            if (string.IsNullOrEmpty(key)) return NotFound("Clé agent absente.");

            _auditService.LogAction("AGENT_KEY_REVEAL", "Consultation de la clé d'API agent", User.Identity?.Name, "WARNING", resourceName: "AgentApiKey");
            await _context.SaveChangesAsync();

            return Ok(new { key });
        }

        public class AgentKeyRotationDto { public int TransitionDays { get; set; } = 30; }

        // Nouvelle clé agent (backlog B18). L'ancienne reste acceptée pendant la période de transition (0 à 90 jours),
        // le temps de redéployer la nouvelle clé sur les postes (MSI APIKEY=..., GPO, registre HKLM\SOFTWARE\Autoprint).
        [HttpPost("agent-key/rotate")]
        [Authorize(Policy = "SETTINGS_MANAGE")]
        public async Task<IActionResult> RotateAgentKey([FromBody] AgentKeyRotationDto dto)
        {
            if (dto.TransitionDays is < 0 or > 90) return BadRequest("Période de transition : de 0 à 90 jours.");

            var current = await _context.ServerSettings.FindAsync("AgentApiKey");
            if (current == null) return NotFound("Clé agent absente.");

            var newKey = Guid.NewGuid().ToString();
            if (dto.TransitionDays > 0)
            {
                await UpsertSetting("AgentApiKeyPrevious", current.Value, "PASSWORD", "Ancienne clé API agent (période de transition)");
                await UpsertSetting("AgentApiKeyPreviousExpiresAt", DateTime.UtcNow.AddDays(dto.TransitionDays).ToString("o"), "STRING", "Fin de validité de l'ancienne clé agent");
            }
            else
            {
                await UpsertSetting("AgentApiKeyPrevious", "", "PASSWORD", "Ancienne clé API agent (période de transition)");
                await UpsertSetting("AgentApiKeyPreviousExpiresAt", "", "STRING", "Fin de validité de l'ancienne clé agent");
            }
            current.Value = _secrets.Protect(newKey);

            _auditService.LogAction("AGENT_KEY_ROTATE",
                dto.TransitionDays > 0 ? $"Nouvelle clé API agent (ancienne clé acceptée encore {dto.TransitionDays} jour(s))" : "Nouvelle clé API agent (ancienne clé révoquée immédiatement)",
                User.Identity?.Name, "WARNING", resourceName: "AgentApiKey");
            await _context.SaveChangesAsync();

            return Ok(new { key = newKey, previousValidUntil = dto.TransitionDays > 0 ? DateTime.UtcNow.AddDays(dto.TransitionDays) : (DateTime?)null });
        }

        // Valeur déjà chiffrée (ou non secrète) : écrite telle quelle
        private async Task UpsertSetting(string key, string value, string type, string description)
        {
            var setting = await _context.ServerSettings.FindAsync(key);
            if (setting == null) _context.ServerSettings.Add(new ServerSetting { Key = key, Value = value, Type = type, Description = description });
            else setting.Value = value;
        }

        [HttpPost("Save")]
        [Authorize(Policy = "SETTINGS_MANAGE")]
        public async Task<IActionResult> SaveSettings([FromBody] SettingsUpdateDto dto)
        {
            if (!string.IsNullOrWhiteSpace(dto.AdLdapFilter) && LdapFilterNormalizer.Normalize(dto.AdLdapFilter) == null)
                return BadRequest(LdapFilterError);

            var currentSettings = await _context.ServerSettings.ToDictionaryAsync(s => s.Key, s => s.Value);

            var modifiedCategories = new HashSet<string>();

            var jsonBefore = new Dictionary<string, string>();
            var jsonAfter = new Dictionary<string, string>();

            void Check(string key, string newValue, string label, string category, bool isSecret = false)
            {
                string oldValue = currentSettings.ContainsKey(key) ? currentSettings[key] : "";
                if (newValue == null) newValue = "";

                if (oldValue != newValue)
                {
                    modifiedCategories.Add(category);

                    jsonBefore[label] = isSecret ? "********" : oldValue;
                    jsonAfter[label] = isSecret ? "********" : newValue;
                }
            }


            Check("LogRetentionDays", dto.LogRetentionDays.ToString(), "Rétention Logs", "Général");

            Check("SmtpHost", dto.SmtpHost, "SMTP Host", "SMTP");
            Check("SmtpPort", dto.SmtpPort.ToString(), "SMTP Port", "SMTP");
            Check("SmtpUser", dto.SmtpUser, "SMTP User", "SMTP");
            if (!string.IsNullOrEmpty(dto.SmtpPass) && dto.SmtpPass != "●●●●●●●●") Check("SmtpPass", "CHANGED", "SMTP Password", "SMTP", true);
            Check("SmtpEnableSsl", dto.SmtpEnableSsl.ToString(), "SMTP SSL", "SMTP");
            Check("SmtpFromAddress", dto.SmtpFromAddress, "SMTP From", "SMTP");
            Check("NamingTemplate", dto.NamingTemplate, "Template Nom", "Nommage");
            Check("NamingEnabled", dto.NamingEnabled.ToString(), "Nommage Auto", "Nommage");
            Check("NamingSameShare", dto.NamingSameShare.ToString(), "Nom Partage", "Nommage");
            Check("Printer_DefaultDirectModeEnabled", dto.Printer_DefaultDirectModeEnabled.ToString(), "Mode Direct par défaut", "Impression");

            Check("PasswordExpirationDays", dto.PasswordExpirationDays.ToString(), "Expiration MDP", "Sécurité");
            Check("Security_IdleTimeoutMinutes", dto.IdleTimeoutMinutes.ToString(), "Déconnexion après inactivité", "Sécurité");
            Check("AdDomain", dto.AdDomain, "AD Domaine", "Active Directory");
            Check("AdBaseDn", dto.AdBaseDn, "AD BaseDN", "Active Directory");
            Check("AdLdapFilter", dto.AdLdapFilter, "AD Filtre", "Active Directory");
            Check("AdUseServiceAccount", dto.AdUseServiceAccount.ToString(), "AD Service Account", "Active Directory");
            Check("AdServiceUser", dto.AdServiceUser, "AD User", "Active Directory");
            if (!string.IsNullOrEmpty(dto.AdServicePassword) && dto.AdServicePassword != "●●●●●●●●") Check("AdServicePassword", "CHANGED", "AD Password", "Active Directory", true);
            Check("AdAdminEmails", dto.AdAdminEmails, "Mails Alertes", "Active Directory");
            Check("Monitoring_Enabled", dto.Monitoring_Enabled.ToString(), "Supervision active", "Supervision");
            Check("Monitoring_IntervalMinutes", dto.Monitoring_IntervalMinutes.ToString(), "Intervalle de scan (min)", "Supervision");
            Check("Monitoring_StartHour", dto.Monitoring_StartHour.ToString(), "Heure de début du scan", "Supervision");
            Check("Monitoring_EndHour", dto.Monitoring_EndHour.ToString(), "Heure de fin du scan", "Supervision");
            Check("Monitoring_ScanOnWeekends", dto.Monitoring_ScanOnWeekends.ToString(), "Scan le week-end", "Supervision");
            Check("Monitoring_IncludeOffline", dto.Monitoring_IncludeOffline.ToString(), "Inclure Offline dans export API", "Monitoring");
            Check("Monitoring_IncludeWarning", dto.Monitoring_IncludeWarning.ToString(), "Inclure Warning dans export API", "Monitoring");
            Check("Monitoring_IncludeCritical", dto.Monitoring_IncludeCritical.ToString(), "Inclure Critical dans export API", "Monitoring");
            Check("Monitoring_IncludeArchived", dto.Monitoring_IncludeArchived.ToString(), "Inclure Archivées dans export API", "Monitoring");

            if (dto.LogRetentionDays > 0) await UpdateSetting("LogRetentionDays", dto.LogRetentionDays.ToString());

            await UpdateSetting("SmtpHost", dto.SmtpHost);
            await UpdateSetting("SmtpPort", dto.SmtpPort.ToString());
            await UpdateSetting("SmtpUser", dto.SmtpUser);
            if (!string.IsNullOrEmpty(dto.SmtpPass) && dto.SmtpPass != "●●●●●●●●") await UpdateSetting("SmtpPass", dto.SmtpPass);
            await UpdateSetting("SmtpEnableSsl", dto.SmtpEnableSsl.ToString());
            await UpdateSetting("SmtpFromAddress", dto.SmtpFromAddress);

            await UpdateSetting("NamingTemplate", dto.NamingTemplate);
            await UpdateSetting("NamingEnabled", dto.NamingEnabled.ToString());
            await UpdateSetting("NamingSameShare", dto.NamingSameShare.ToString());
            await UpdateSetting("Printer_DefaultDirectModeEnabled", dto.Printer_DefaultDirectModeEnabled.ToString());
            if (dto.PasswordExpirationDays >= 0) await UpdateSetting("PasswordExpirationDays", dto.PasswordExpirationDays.ToString());
            if (dto.IdleTimeoutMinutes is >= 0 and <= 1440) await UpdateSetting("Security_IdleTimeoutMinutes", dto.IdleTimeoutMinutes.ToString());

            await UpdateSetting("AdDomain", dto.AdDomain);
            await UpdateSetting("AdBaseDn", dto.AdBaseDn);
            await UpdateSetting("AdLdapFilter", dto.AdLdapFilter);
            await UpdateSetting("AdUseServiceAccount", dto.AdUseServiceAccount.ToString());
            await UpdateSetting("AdServiceUser", dto.AdServiceUser);
            if (!string.IsNullOrEmpty(dto.AdServicePassword) && dto.AdServicePassword != "●●●●●●●●") await UpdateSetting("AdServicePassword", dto.AdServicePassword);
            await UpdateSetting("AdAdminEmails", dto.AdAdminEmails);
            await UpdateSetting("Monitoring_Enabled", dto.Monitoring_Enabled.ToString());
            await UpdateSetting("Monitoring_IntervalMinutes", dto.Monitoring_IntervalMinutes.ToString());
            await UpdateSetting("Monitoring_StartHour", dto.Monitoring_StartHour.ToString());
            await UpdateSetting("Monitoring_EndHour", dto.Monitoring_EndHour.ToString());
            await UpdateSetting("Monitoring_ScanOnWeekends", dto.Monitoring_ScanOnWeekends.ToString());
            await UpdateSetting("Monitoring_IncludeOffline", dto.Monitoring_IncludeOffline.ToString());
            await UpdateSetting("Monitoring_IncludeWarning", dto.Monitoring_IncludeWarning.ToString());
            await UpdateSetting("Monitoring_IncludeCritical", dto.Monitoring_IncludeCritical.ToString());
            await UpdateSetting("Monitoring_IncludeArchived", dto.Monitoring_IncludeArchived.ToString());


            if (modifiedCategories.Any())
            {
                string summary = $"Mise à jour configuration : {string.Join(" + ", modifiedCategories)}";

                _auditService.LogCustomAudit(
                    "CONFIG_UPDATE",
                    summary,
                    User.Identity?.Name,
                    "Configuration Serveur",
                    jsonBefore,
                    jsonAfter,
                    "WARNING"
                );

                await _context.SaveChangesAsync();
            }

            return Ok(new { Message = "Configuration sauvegardée avec succès." });
        }

        private async Task UpdateSetting(string key, string value)
        {
            // Les secrets sont stockés chiffrés (audit #9)
            if (SecretProtector.IsSecret(key)) value = _secrets.Protect(value);

            var setting = await _context.ServerSettings.FindAsync(key);
            if (setting != null)
                setting.Value = value ?? "";
            else
                _context.ServerSettings.Add(new ServerSetting { Key = key, Value = value ?? "", Description = "Auto-generated" });
        }

        [HttpPost("TestEmail")]
        [Authorize(Policy = "SETTINGS_MANAGE")]
        public async Task<IActionResult> TestEmail([FromBody] TestEmailDto dto)
        {
            try
            {
                // Mot de passe laissé masqué : on réutilise celui enregistré, uniquement vers le serveur SMTP enregistré
                string password = dto.Password;
                if (IsEmptyOrMasked(password))
                {
                    var savedHost = _context.ServerSettings.Find("SmtpHost")?.Value;
                    if (!string.IsNullOrWhiteSpace(savedHost) && !string.Equals(savedHost.Trim(), dto.Host?.Trim(), StringComparison.OrdinalIgnoreCase))
                        return BadRequest("Pour tester un autre serveur SMTP que celui enregistré, saisissez son mot de passe.");
                    password = _secrets.Unprotect(_context.ServerSettings.Find("SmtpPass")?.Value);
                }
                await _emailService.SendTestEmailAsync(dto.Host, dto.Port, dto.User, password, dto.Ssl, dto.From, dto.To);
                return Ok(new { Message = "Connexion SMTP réussie !" });
            }
            catch (Exception ex) { return BadRequest("Echec du test SMTP : " + ex.Message); }
        }

        [HttpPost("TestAd")]
        [Authorize(Policy = "SETTINGS_MANAGE")]
        public IActionResult TestAd([FromBody] TestAdDto dto)
        {
            if (!OperatingSystem.IsWindows()) return BadRequest("Le serveur doit tourner sous Windows.");
            var domainError = CheckAdTestTarget(dto.Domain, dto.UseServiceAccount, dto.ServicePassword);
            if (domainError != null) return BadRequest(domainError);

            try
            {
                ContextType contextType = ContextType.Domain;
                string finalPass = dto.ServicePassword;
                if (dto.UseServiceAccount && IsEmptyOrMasked(finalPass))
                {
                    var saved = _context.ServerSettings.Find("AdServicePassword");
                    if (saved != null) finalPass = _secrets.Unprotect(saved.Value);
                }

                string finalUser = dto.ServiceUser;
                if (dto.UseServiceAccount && !string.IsNullOrEmpty(finalUser) && !finalUser.Contains("\\") && !finalUser.Contains("@"))
                {
                    finalUser = $"{finalUser}@{dto.Domain}";
                }

                var options = ContextOptions.Negotiate | ContextOptions.Sealing;

                using var context = dto.UseServiceAccount
                    ? new PrincipalContext(contextType, dto.Domain, null, options, finalUser, finalPass)
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

            var domainError = CheckAdTestTarget(dto.Domain, dto.UseServiceAccount, dto.ServicePassword);
            if (domainError != null) return Ok(new { Success = false, Message = domainError });

            string? normalizedFilter = LdapFilterNormalizer.Normalize(dto.Filter);
            if (!string.IsNullOrWhiteSpace(dto.Filter) && normalizedFilter == null)
                return Ok(new { Success = false, Message = LdapFilterError });

            // Domaine et DN de base saisis : chemin reconstruit et contrôlé (B76)
            string? ldapPath = LdapPathBuilder.Build(dto.Domain, dto.BaseDn);
            if (ldapPath == null)
                return Ok(new { Success = false, Message = "DN de base invalide : attendu « OU=…,DC=…,DC=… » (écrire \\, pour une virgule dans un nom)." });

            try
            {
                string finalPass = dto.ServicePassword;
                if (dto.UseServiceAccount && IsEmptyOrMasked(finalPass))
                {
                    var saved = _context.ServerSettings.Find("AdServicePassword");
                    if (saved != null) finalPass = _secrets.Unprotect(saved.Value);
                }

                string finalUser = dto.ServiceUser;
                if (dto.UseServiceAccount && !string.IsNullOrEmpty(finalUser) && !finalUser.Contains("\\") && !finalUser.Contains("@"))
                {
                    finalUser = $"{finalUser}@{dto.Domain}";
                }

                using DirectoryEntry entry = dto.UseServiceAccount
                    ? new DirectoryEntry(ldapPath, finalUser, finalPass, AuthenticationTypes.Secure)
                    : new DirectoryEntry(ldapPath, null, null, AuthenticationTypes.Secure);

                using DirectorySearcher searcher = new DirectorySearcher(entry);
                string configFilter = normalizedFilter ?? "(objectClass=user)";

                // Filtre final relu lui aussi : le nom testé est échappé puis l'ensemble reconstruit
                string? finalFilter = string.IsNullOrWhiteSpace(dto.TestUserQuery)
                    ? configFilter
                    : LdapFilterNormalizer.Normalize($"(&{configFilter}(sAMAccountName={SecurityHelper.EscapeLdapFilter(dto.TestUserQuery.Trim())}))");
                if (finalFilter == null) return Ok(new { Success = false, Message = "Nom à rechercher invalide." });
                searcher.Filter = finalFilter;

                searcher.SizeLimit = 1;
                var result = searcher.FindOne();

                if (result == null) return Ok(new { Success = false, Message = "Aucun objet trouvé." });

                string name = result.Properties["name"].Count > 0 ? result.Properties["name"][0].ToString()! : "Inconnu";
                return Ok(new { Success = true, Message = $"✅ SUCCÈS : Objet '{name}' trouvé !" });
            }
            catch (Exception ex) { return Ok(new { Success = false, Message = $"💥 Erreur LDAP : {ex.Message}" }); }
        }

        private static bool IsEmptyOrMasked(string? value) => string.IsNullOrEmpty(value) || value == "●●●●●●●●";

        // Sans mot de passe saisi, le test s'authentifie avec un secret du serveur (mot de passe AD enregistré ou compte machine) :
        // il est alors limité au domaine enregistré, pour qu'on ne puisse pas envoyer ce secret vers un serveur tiers.
        private string? CheckAdTestTarget(string? domain, bool useServiceAccount, string? typedPassword)
        {
            if (string.IsNullOrWhiteSpace(domain) || !System.Text.RegularExpressions.Regex.IsMatch(domain, @"^[A-Za-z0-9]([A-Za-z0-9.-]{0,252})$"))
                return "Nom de domaine invalide.";

            bool usesServerSecret = !useServiceAccount || IsEmptyOrMasked(typedPassword);
            if (!usesServerSecret) return null;

            var savedDomain = _context.ServerSettings.Find("AdDomain")?.Value;
            if (!string.IsNullOrWhiteSpace(savedDomain) && !string.Equals(savedDomain.Trim(), domain.Trim(), StringComparison.OrdinalIgnoreCase))
                return "Pour tester un autre domaine que celui enregistré, saisissez le mot de passe du compte de service.";

            return null;
        }

        private const string LdapFilterError = "Filtre LDAP invalide : vérifiez les parenthèses, les noms d'attributs et les caractères spéciaux (écrire \\28, \\29, \\2a, \\5c pour ( ) * \\ dans une valeur).";
    }

    public class SettingsUpdateDto
    {
        public int LogRetentionDays { get; set; } = 180;
        public string SmtpHost { get; set; } = "";
        public int SmtpPort { get; set; } = 25;
        public string SmtpUser { get; set; } = "";
        public string SmtpPass { get; set; } = "";
        public bool SmtpEnableSsl { get; set; } = false;
        public string SmtpFromAddress { get; set; } = "";
        public string NamingTemplate { get; set; } = "";
        public bool NamingEnabled { get; set; } = false;
        public bool NamingSameShare { get; set; } = false;
        public bool Printer_DefaultDirectModeEnabled { get; set; }
        public int PasswordExpirationDays { get; set; }
        public int IdleTimeoutMinutes { get; set; } = 30;
        public string AdDomain { get; set; } = "";
        public string AdBaseDn { get; set; } = "";
        public string AdLdapFilter { get; set; } = "";
        public bool AdUseServiceAccount { get; set; } = false;
        public string AdServiceUser { get; set; } = "";
        public string AdServicePassword { get; set; } = "";
        public string AdAdminEmails { get; set; } = "";
        public bool Monitoring_Enabled { get; set; } = true;
        public int Monitoring_IntervalMinutes { get; set; } = 60;
        public int Monitoring_StartHour { get; set; } = 8;
        public int Monitoring_EndHour { get; set; } = 18;
        public bool Monitoring_ScanOnWeekends { get; set; } = true;
        public bool Monitoring_IncludeOffline { get; set; } = true;
        public bool Monitoring_IncludeWarning { get; set; } = true;
        public bool Monitoring_IncludeCritical { get; set; } = true;
        public bool Monitoring_IncludeArchived { get; set; } = false;
    }

    public class TestEmailDto { public string Host { get; set; } = ""; public int Port { get; set; } public string User { get; set; } = ""; public string Password { get; set; } = ""; public bool Ssl { get; set; } public string From { get; set; } = ""; public string To { get; set; } = ""; }
    public class TestAdDto { public string Domain { get; set; } = ""; public bool UseServiceAccount { get; set; } public string ServiceUser { get; set; } = ""; public string ServicePassword { get; set; } = ""; }
    public class TestAdFilterDto { public string Domain { get; set; } = ""; public string BaseDn { get; set; } = ""; public string Filter { get; set; } = ""; public string TestUserQuery { get; set; } = ""; public bool UseServiceAccount { get; set; } public string ServiceUser { get; set; } = ""; public string ServicePassword { get; set; } = ""; }
}