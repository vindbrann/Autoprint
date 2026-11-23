using Autoprint.Server.Data;
using Autoprint.Shared;
using Autoprint.Shared.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Autoprint.Server.Services;
using System.Text.Json;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using System;

namespace Autoprint.Server.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class SnmpProfilesController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly AuditService _auditService;
        private readonly ISnmpService _snmpService;
        private readonly ILogger<SnmpProfilesController> _logger;

        public SnmpProfilesController(ApplicationDbContext context, AuditService auditService, ISnmpService snmpService, ILogger<SnmpProfilesController> logger)
        {
            _context = context;
            _auditService = auditService;
            _snmpService = snmpService;
            _logger = logger;
        }

        [HttpGet]
        [Authorize(Policy = "SNMP_PROFILE_READ")]
        public async Task<ActionResult<IEnumerable<SnmpProfile>>> GetSnmpProfiles()
        {
            var profiles = await _context.SnmpProfiles
                .Include(p => p.Items)
                .Include(p => p.Overrides)
                .AsNoTracking()
                .ToListAsync();
            foreach (var p in profiles) p.HasSnmpCommunity = !string.IsNullOrEmpty(p.SnmpCommunityChiffree);
            return profiles;
        }

        [HttpGet("{id:int}")]
        [Authorize(Policy = "SNMP_PROFILE_READ")]
        public async Task<ActionResult<SnmpProfile>> GetSnmpProfile(int id)
        {
            var profile = await _context.SnmpProfiles
                .Include(p => p.Items)
                .Include(p => p.Overrides)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (profile == null) return NotFound();
            profile.HasSnmpCommunity = !string.IsNullOrEmpty(profile.SnmpCommunityChiffree);
            return profile;
        }

        [HttpPost("scan-printer")]
        [Authorize(Policy = "SNMP_PROFILE_WRITE")]
        public async Task<ActionResult<List<DiscoveredOidDto>>> ScanPrinterOids([FromBody] SnmpScanRequestDto request)
        {
            if (request == null || !IsValidHost(request.IpAddress))
            {
                return BadRequest("Adresse IP ou nom d'hôte de l'imprimante invalide.");
            }
            _logger.LogInformation("[SNMP_SCAN_API] Scan demandé pour IP={Ip}", ForLog(request.IpAddress));

            try
            {
                var discoveredOids = await _snmpService.ScanPrinterOidsAsync(request, await ResolveRequestCredentialsAsync(request.CredentialId, request.Community, request.Version, request.ProfileId));
                _logger.LogInformation("[SNMP_SCAN_API] Scan réussi pour IP={Ip}, OIDs={Count}", ForLog(request.IpAddress), discoveredOids.Count);
                return Ok(discoveredOids);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[SNMP_SCAN_API] Échec scan IP={Ip}", ForLog(request.IpAddress));
                return BadRequest(DescribeSnmpError(ex));
            }
        }

        /// <summary>#50 : détection automatique sur une imprimante d'essai (éléments, clés, niveaux), sans les modifications du profil.</summary>
        [HttpPost("detect")]
        [Authorize(Policy = "SNMP_PROFILE_READ")]
        public async Task<ActionResult<PrinterDiagnosticResult>> DetectPrinter([FromBody] SnmpScanRequestDto request)
        {
            if (request == null || !IsValidHost(request.IpAddress))
            {
                return BadRequest("Adresse IP ou nom d'hôte de l'imprimante invalide.");
            }
            _logger.LogInformation("[SNMP_DETECT_API] Détection demandée pour IP={Ip}", ForLog(request.IpAddress));

            try
            {
                var credentials = await ResolveRequestCredentialsAsync(request.CredentialId, request.Community, request.Version, request.ProfileId);
                var threshold = await MonitoringSettings.GetLowThresholdAsync(_context);
                return Ok(await _snmpService.DetectPrinterAsync(request.IpAddress.Trim(), request.Port, credentials, threshold));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[SNMP_DETECT_API] Échec détection IP={Ip}", ForLog(request.IpAddress));
                return BadRequest(DescribeSnmpError(ex));
            }
        }

        [HttpPost("test-profile")]
        [HttpPost("test")]
        [Authorize(Policy = "SNMP_PROFILE_WRITE")]
        public async Task<ActionResult<List<SnmpTestProfileResultDto>>> TestProfileItems([FromBody] SnmpTestProfileRequestDto request)
        {
            if (request == null || !IsValidHost(request.IpAddress))
            {
                _logger.LogWarning("[SNMP_TEST_API] Requête invalide : adresse IP ou nom d'hôte incorrect.");
                return BadRequest("Adresse IP ou nom d'hôte de l'imprimante invalide.");
            }
            _logger.LogInformation("[SNMP_TEST_API] Requête reçue sur test-profile pour IP={Ip}, Items={Count}", ForLog(request.IpAddress), request.Items?.Count ?? 0);

            try
            {
                var testResults = await _snmpService.TestProfileItemsAsync(request, await ResolveRequestCredentialsAsync(request.CredentialId, request.Community, request.Version, request.ProfileId));
                _logger.LogInformation("[SNMP_TEST_API] Succès test-profile pour IP={Ip}, Résultats={Count}", ForLog(request.IpAddress), testResults.Count);
                return Ok(testResults);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[SNMP_TEST_API] Erreur sur test-profile pour IP={Ip}", ForLog(request.IpAddress));
                return BadRequest(DescribeSnmpError(ex));
            }
        }

        [HttpPut("{id:int}")]
        [Authorize(Policy = "SNMP_PROFILE_WRITE")]
        public async Task<IActionResult> PutSnmpProfile(int id, SnmpProfile profile)
        {
            if (id != profile.Id) return BadRequest();

            var existingProfile = await _context.SnmpProfiles
                .Include(p => p.Items)
                .Include(p => p.Overrides)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (existingProfile == null) return NotFound();

            existingProfile.Name = profile.Name;
            existingProfile.IsColor = profile.IsColor;
            existingProfile.OidPageCounter = profile.OidPageCounter;
            existingProfile.OidTonerBlack = profile.OidTonerBlack;
            existingProfile.OidTonerCyan = profile.OidTonerCyan;
            existingProfile.OidTonerMagenta = profile.OidTonerMagenta;
            existingProfile.OidTonerYellow = profile.OidTonerYellow;
            var connexionError = await AppliquerConnexionAsync(existingProfile, profile);
            if (connexionError != null) return BadRequest(connexionError);

            // Remplacement des items
            _context.SnmpProfileItems.RemoveRange(existingProfile.Items);
            existingProfile.Items = profile.Items ?? new List<SnmpProfileItem>();

            // #50 : mode, seuil propre et modifications des éléments détectés
            existingProfile.Mode = profile.Mode;
            existingProfile.LowThresholdPercent = CleanThreshold(profile.LowThresholdPercent);
            existingProfile.SupplyLeadTimeDays = CleanLeadTime(profile.SupplyLeadTimeDays);
            _context.SnmpProfileOverrides.RemoveRange(existingProfile.Overrides);
            existingProfile.Overrides = CleanOverrides(profile.Overrides);

            try
            {
                await _auditService.LogUpdateAsync(
                    id,
                    existingProfile,
                    "SNMP_PROFILE_UPDATE",
                    User.Identity?.Name);

                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!SnmpProfileExists(id)) return NotFound();
                else throw;
            }
            return NoContent();
        }

        [HttpPost]
        [Authorize(Policy = "SNMP_PROFILE_WRITE")]
        public async Task<ActionResult<SnmpProfile>> PostSnmpProfile(SnmpProfile profile)
        {
            var connexionError = await AppliquerConnexionAsync(profile, profile);
            if (connexionError != null) return BadRequest(connexionError);
            profile.LowThresholdPercent = CleanThreshold(profile.LowThresholdPercent);
            profile.SupplyLeadTimeDays = CleanLeadTime(profile.SupplyLeadTimeDays);
            profile.Overrides = CleanOverrides(profile.Overrides);
            _context.SnmpProfiles.Add(profile);

            _auditService.LogAction(
                "SNMP_PROFILE_CREATE",
                $"Création profil SNMP : {profile.Name}",
                User.Identity?.Name,
                resourceName: profile.Name);

            await _context.SaveChangesAsync();
            profile.HasSnmpCommunity = !string.IsNullOrEmpty(profile.SnmpCommunityChiffree);
            profile.SnmpCommunity = null; // jamais renvoyée (B32)
            return CreatedAtAction("GetSnmpProfile", new { id = profile.Id }, profile);
        }

        [HttpDelete("{id:int}")]
        [Authorize(Policy = "SNMP_PROFILE_DELETE")]
        public async Task<IActionResult> DeleteSnmpProfile(int id)
        {
            var profile = await _context.SnmpProfiles.FindAsync(id);
            if (profile == null) return NotFound();

            bool isUsed = await _context.Modeles.AnyAsync(m => m.SnmpProfileId == id);
            if (isUsed)
            {
                return BadRequest("Ce profil SNMP est utilisé par un ou plusieurs modèles d'imprimantes et ne peut pas être supprimé.");
            }

            _context.SnmpProfiles.Remove(profile);

            _auditService.LogAction(
                "SNMP_PROFILE_DELETE",
                $"Suppression profil SNMP : {profile.Name}",
                User.Identity?.Name,
                resourceName: profile.Name);

            await _context.SaveChangesAsync();
            return NoContent();
        }

        [HttpGet("{id:int}/export")]
        [Authorize(Policy = "SNMP_PROFILE_READ")]
        public async Task<IActionResult> ExportProfile(int id)
        {
            var profile = await _context.SnmpProfiles
                .Include(p => p.Items)
                .Include(p => p.Overrides)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (profile == null) return NotFound();

            var exportData = new
            {
                profile.Name,
                profile.Mode,
                profile.LowThresholdPercent,
                profile.SupplyLeadTimeDays,
                Overrides = profile.Overrides.Select(o => new { o.Key, o.DisplayName, o.Hidden, o.ColorHex, o.LowThresholdPercent }),
                profile.OidTonerBlack,
                profile.OidTonerCyan,
                profile.OidTonerMagenta,
                profile.OidTonerYellow,
                profile.OidPageCounter,
                Items = profile.Items.Select(i => new
                {
                    i.Category,
                    i.Name,
                    i.Oid,
                    i.OidMaxCapacity,
                    i.ValueType,
                    i.ColorHex,
                    i.SortOrder
                })
            };

            var json = JsonSerializer.Serialize(exportData, new JsonSerializerOptions { WriteIndented = true });
            var bytes = Encoding.UTF8.GetBytes(json);
            return File(bytes, "application/json", $"{profile.Name.Replace(" ", "_")}_snmp_profile.json");
        }

        [HttpPost("import")]
        [Authorize(Policy = "SNMP_PROFILE_WRITE")]
        public async Task<ActionResult<SnmpProfile>> ImportProfile([FromBody] SnmpProfileImportDto importDto)
        {
            if (string.IsNullOrWhiteSpace(importDto.Name))
            {
                return BadRequest("Le nom du profil est obligatoire.");
            }

            var profile = new SnmpProfile
            {
                Name = importDto.Name,
                OidTonerBlack = importDto.OidTonerBlack,
                OidTonerCyan = importDto.OidTonerCyan,
                OidTonerMagenta = importDto.OidTonerMagenta,
                OidTonerYellow = importDto.OidTonerYellow,
                OidPageCounter = importDto.OidPageCounter,
                LowThresholdPercent = CleanThreshold(importDto.LowThresholdPercent),
                SupplyLeadTimeDays = CleanLeadTime(importDto.SupplyLeadTimeDays)
            };

            if (importDto.Items != null)
            {
                foreach (var itemDto in importDto.Items)
                {
                    profile.Items.Add(new SnmpProfileItem
                    {
                        Category = itemDto.Category,
                        Name = itemDto.Name,
                        Oid = itemDto.Oid,
                        OidMaxCapacity = itemDto.OidMaxCapacity,
                        ValueType = itemDto.ValueType,
                        ColorHex = itemDto.ColorHex,
                        SortOrder = itemDto.SortOrder
                    });
                }
            }

            // #50 : export récent = mode et modifications repris ; export antérieur = conversion comme au démarrage
            if (importDto.Mode != null)
            {
                profile.Mode = importDto.Mode.Value;
                profile.Overrides = CleanOverrides((importDto.Overrides ?? new()).Select(o => new SnmpProfileOverride
                {
                    Key = o.Key, DisplayName = o.DisplayName, Hidden = o.Hidden, ColorHex = o.ColorHex, LowThresholdPercent = o.LowThresholdPercent
                }).ToList());
            }
            else
            {
                SnmpProfileConverter.Convert(profile);
            }

            _context.SnmpProfiles.Add(profile);

            _auditService.LogAction(
                "SNMP_PROFILE_IMPORT",
                $"Importation profil SNMP : {profile.Name}",
                User.Identity?.Name,
                resourceName: profile.Name);

            await _context.SaveChangesAsync();
            return CreatedAtAction("GetSnmpProfile", new { id = profile.Id }, profile);
        }

        private static int? CleanThreshold(int? value) => value is >= 1 and <= 99 ? value : null;
        private static int? CleanLeadTime(int? value) => value is >= 1 and <= 180 ? value : null;

        // Une seule modification par élément, sans les lignes vides ; longueurs bornées comme en base
        private static List<SnmpProfileOverride> CleanOverrides(IEnumerable<SnmpProfileOverride>? overrides)
        {
            return (overrides ?? Enumerable.Empty<SnmpProfileOverride>())
                .Where(o => !string.IsNullOrWhiteSpace(o.Key) && o.Key.Trim().Length <= 50)
                .GroupBy(o => o.Key.Trim(), StringComparer.OrdinalIgnoreCase)
                .Select(g => g.Last())
                .Select(o => new SnmpProfileOverride
                {
                    Key = o.Key.Trim(),
                    DisplayName = string.IsNullOrWhiteSpace(o.DisplayName) ? null : o.DisplayName.Trim()[..Math.Min(100, o.DisplayName.Trim().Length)],
                    Hidden = o.Hidden,
                    ColorHex = string.IsNullOrWhiteSpace(o.ColorHex) ? null : o.ColorHex.Trim()[..Math.Min(20, o.ColorHex.Trim().Length)],
                    LowThresholdPercent = CleanThreshold(o.LowThresholdPercent)
                })
                .Where(o => !o.IsEmpty)
                .ToList();
        }

        private bool SnmpProfileExists(int id)
        {
            return _context.SnmpProfiles.Any(e => e.Id == id);
        }

        // #40 : connexion du profil (version, communauté chiffrée, identifiant v3). Communauté vide = valeur actuelle conservée.
        private async Task<string?> AppliquerConnexionAsync(SnmpProfile target, SnmpProfile input)
        {
            int version = input.SnmpVersion is 1 or 2 or 3 ? input.SnmpVersion : 2;
            int? credentialId = version == 3 ? input.SnmpCredentialId : null;
            if (version == 3 && credentialId == null) return "Connexion SNMP v3 : choisissez l'identifiant SNMP du profil.";
            if (credentialId != null && !await _context.SnmpCredentials.AnyAsync(c => c.Id == credentialId)) return "Identifiant SNMP introuvable.";

            target.SnmpVersion = version;
            target.SnmpCredentialId = credentialId;
            if (!string.IsNullOrWhiteSpace(input.SnmpCommunity))
                target.SnmpCommunityChiffree = HttpContext.RequestServices.GetRequiredService<SecretProtector>().Protect(input.SnmpCommunity.Trim()); // B32
            target.SnmpCommunity = null;
            return null;
        }

        // Connexion de l'écran de test : identifiant enregistré (v3), communauté saisie, ou communauté enregistrée du profil
        // (jamais renvoyée au navigateur, d'où le ProfileId quand le champ est laissé vide)
        private async Task<SnmpCredentials> ResolveRequestCredentialsAsync(int? credentialId, string? community, int version, int? profileId)
        {
            var secrets = HttpContext.RequestServices.GetRequiredService<SecretProtector>();
            if (credentialId != null)
            {
                var profile = await _context.SnmpCredentials.AsNoTracking().FirstOrDefaultAsync(c => c.Id == credentialId.Value)
                              ?? throw new InvalidOperationException("Identifiant SNMP introuvable.");
                return SnmpCredentials.FromProfile(profile, secrets);
            }
            if (version == 3) throw new InvalidOperationException("Connexion SNMP v3 : choisissez un identifiant SNMP.");
            if (string.IsNullOrWhiteSpace(community) && profileId != null)
            {
                var stored = await _context.SnmpProfiles.AsNoTracking().Where(p => p.Id == profileId.Value).Select(p => p.SnmpCommunityChiffree).FirstOrDefaultAsync();
                if (!string.IsNullOrEmpty(stored)) community = secrets.Unprotect(stored);
            }
            return SnmpCredentials.Legacy(community, version);
        }

        // Retours à la ligne retirés avant journalisation (CodeQL « Log entries created from user input ») ;
        // IsValidHost les refuse déjà, mais CodeQL ne reconnaît que ce nettoyage.
        private static string ForLog(string? value) => (value ?? string.Empty).Replace("\r", string.Empty).Replace("\n", string.Empty);

        // Validation avant toute journalisation
        private static bool IsValidHost(string? host)
        {
            if (string.IsNullOrWhiteSpace(host)) return false;
            host = host.Trim();
            if (System.Net.IPAddress.TryParse(host, out _)) return true;
            return host.Length <= 253 && System.Text.RegularExpressions.Regex.IsMatch(host, @"^[A-Za-z0-9]([A-Za-z0-9-]*[A-Za-z0-9])?(\.[A-Za-z0-9]([A-Za-z0-9-]*[A-Za-z0-9])?)*$");
        }

        // Les InvalidOperationException du service SNMP portent un diagnostic réseau rédigé pour l'administrateur ; le reste reste interne
        private string DescribeSnmpError(Exception ex)
        {
            return ex is InvalidOperationException
                ? ex.Message
                : Autoprint.Server.Helpers.ApiErrors.LogAndDescribe(HttpContext, ex, "Erreur interne pendant l'interrogation SNMP.");
        }
    }
}
