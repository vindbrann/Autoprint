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
            return await _context.SnmpProfiles
                .Include(p => p.Items)
                .AsNoTracking()
                .ToListAsync();
        }

        [HttpGet("{id:int}")]
        [Authorize(Policy = "SNMP_PROFILE_READ")]
        public async Task<ActionResult<SnmpProfile>> GetSnmpProfile(int id)
        {
            var profile = await _context.SnmpProfiles
                .Include(p => p.Items)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (profile == null) return NotFound();
            return profile;
        }

        [HttpPost("scan-printer")]
        [Authorize]
        public async Task<ActionResult<List<DiscoveredOidDto>>> ScanPrinterOids([FromBody] SnmpScanRequestDto request)
        {
            _logger.LogInformation("[SNMP_SCAN_API] Scan demandé pour IP={Ip}", request?.IpAddress);
            if (request == null || string.IsNullOrWhiteSpace(request.IpAddress))
            {
                return BadRequest("L'adresse IP de l'imprimante de test est obligatoire.");
            }

            try
            {
                var discoveredOids = await _snmpService.ScanPrinterOidsAsync(request);
                _logger.LogInformation("[SNMP_SCAN_API] Scan réussi pour IP={Ip}, OIDs={Count}", request.IpAddress, discoveredOids.Count);
                return Ok(discoveredOids);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[SNMP_SCAN_API] Échec scan IP={Ip}", request.IpAddress);
                return BadRequest(ex.Message);
            }
        }

        [HttpPost("test-profile")]
        [HttpPost("test")]
        [Authorize]
        public async Task<ActionResult<List<SnmpTestProfileResultDto>>> TestProfileItems([FromBody] SnmpTestProfileRequestDto request)
        {
            _logger.LogInformation("[SNMP_TEST_API] Requête reçue sur test-profile pour IP={Ip}, Items={Count}", request?.IpAddress, request?.Items?.Count ?? 0);

            if (request == null || string.IsNullOrWhiteSpace(request.IpAddress))
            {
                _logger.LogWarning("[SNMP_TEST_API] Requête invalide : IP obligatoire.");
                return BadRequest("L'adresse IP de l'imprimante de test est obligatoire.");
            }

            try
            {
                var testResults = await _snmpService.TestProfileItemsAsync(request);
                _logger.LogInformation("[SNMP_TEST_API] Succès test-profile pour IP={Ip}, Résultats={Count}", request.IpAddress, testResults.Count);
                return Ok(testResults);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[SNMP_TEST_API] Erreur sur test-profile pour IP={Ip}", request.IpAddress);
                return BadRequest(ex.Message);
            }
        }

        [HttpPut("{id:int}")]
        [Authorize(Policy = "SNMP_PROFILE_WRITE")]
        public async Task<IActionResult> PutSnmpProfile(int id, SnmpProfile profile)
        {
            if (id != profile.Id) return BadRequest();

            var existingProfile = await _context.SnmpProfiles
                .Include(p => p.Items)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (existingProfile == null) return NotFound();

            existingProfile.Name = profile.Name;
            existingProfile.IsColor = profile.IsColor;
            existingProfile.OidPageCounter = profile.OidPageCounter;
            existingProfile.OidTonerBlack = profile.OidTonerBlack;
            existingProfile.OidTonerCyan = profile.OidTonerCyan;
            existingProfile.OidTonerMagenta = profile.OidTonerMagenta;
            existingProfile.OidTonerYellow = profile.OidTonerYellow;

            // Remplacement des items
            _context.SnmpProfileItems.RemoveRange(existingProfile.Items);
            existingProfile.Items = profile.Items ?? new List<SnmpProfileItem>();

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
            _context.SnmpProfiles.Add(profile);

            _auditService.LogAction(
                "SNMP_PROFILE_CREATE",
                $"Création profil SNMP : {profile.Name}",
                User.Identity?.Name,
                resourceName: profile.Name);

            await _context.SaveChangesAsync();
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
                .FirstOrDefaultAsync(p => p.Id == id);

            if (profile == null) return NotFound();

            var exportData = new
            {
                profile.Name,
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
                OidPageCounter = importDto.OidPageCounter
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

            _context.SnmpProfiles.Add(profile);

            _auditService.LogAction(
                "SNMP_PROFILE_IMPORT",
                $"Importation profil SNMP : {profile.Name}",
                User.Identity?.Name,
                resourceName: profile.Name);

            await _context.SaveChangesAsync();
            return CreatedAtAction("GetSnmpProfile", new { id = profile.Id }, profile);
        }

        private bool SnmpProfileExists(int id)
        {
            return _context.SnmpProfiles.Any(e => e.Id == id);
        }
    }
}
