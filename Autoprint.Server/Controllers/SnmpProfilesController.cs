using Autoprint.Server.Data;
using Autoprint.Shared;
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
    [Authorize(Policy = "SETTINGS_MANAGE")]
    public class SnmpProfilesController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly AuditService _auditService;

        public SnmpProfilesController(ApplicationDbContext context, AuditService auditService)
        {
            _context = context;
            _auditService = auditService;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<SnmpProfile>>> GetSnmpProfiles()
        {
            return await _context.SnmpProfiles
                .AsNoTracking()
                .ToListAsync();
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<SnmpProfile>> GetSnmpProfile(int id)
        {
            var profile = await _context.SnmpProfiles.FindAsync(id);
            if (profile == null) return NotFound();
            return profile;
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> PutSnmpProfile(int id, SnmpProfile profile)
        {
            if (id != profile.Id) return BadRequest();

            _context.Entry(profile).State = EntityState.Modified;

            try
            {
                await _auditService.LogUpdateAsync(
                    id,
                    profile,
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

        [HttpDelete("{id}")]
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

        [HttpGet("{id}/export")]
        public async Task<IActionResult> ExportProfile(int id)
        {
            var profile = await _context.SnmpProfiles.FindAsync(id);
            if (profile == null) return NotFound();

            var exportData = new
            {
                profile.Name,
                profile.OidTonerBlack,
                profile.OidTonerCyan,
                profile.OidTonerMagenta,
                profile.OidTonerYellow,
                profile.OidPageCounter
            };

            var json = JsonSerializer.Serialize(exportData, new JsonSerializerOptions { WriteIndented = true });
            var bytes = Encoding.UTF8.GetBytes(json);
            return File(bytes, "application/json", $"{profile.Name.Replace(" ", "_")}_snmp_profile.json");
        }

        [HttpPost("import")]
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
