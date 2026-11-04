using Autoprint.Server.Data;
using Autoprint.Server.Services;
using Autoprint.Shared;
using Autoprint.Shared.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Autoprint.Server.Controllers
{
    /// <summary>
    /// Profils d'identifiants SNMP (v1 / v2c / v3). Les secrets sont chiffrés en base et ne sont jamais renvoyés :
    /// la console ne voit que leur présence. Un secret laissé vide à la modification est conservé.
    /// </summary>
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(Policy = "SNMP_PROFILE_READ")]
    public class SnmpCredentialsController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly SecretProtector _secrets;
        private readonly AuditService _auditService;

        public SnmpCredentialsController(ApplicationDbContext context, SecretProtector secrets, AuditService auditService)
        {
            _context = context;
            _secrets = secrets;
            _auditService = auditService;
        }

        [HttpGet]
        public async Task<ActionResult<List<SnmpCredentialViewDto>>> GetAll()
        {
            var profiles = await _context.SnmpCredentials.AsNoTracking().OrderBy(c => c.Name).ToListAsync();
            var modelCounts = await _context.Modeles.Where(m => m.SnmpCredentialId != null)
                .GroupBy(m => m.SnmpCredentialId!.Value).Select(g => new { g.Key, Count = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.Count);
            var printerCounts = await _context.Imprimantes.Where(i => i.SnmpCredentialId != null)
                .GroupBy(i => i.SnmpCredentialId!.Value).Select(g => new { g.Key, Count = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.Count);

            return profiles.Select(p => ToView(p, modelCounts.GetValueOrDefault(p.Id), printerCounts.GetValueOrDefault(p.Id))).ToList();
        }

        [HttpGet("{id:int}")]
        public async Task<ActionResult<SnmpCredentialViewDto>> Get(int id)
        {
            var profile = await _context.SnmpCredentials.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id);
            return profile == null ? NotFound() : ToView(profile, 0, 0);
        }

        [HttpPost]
        [Authorize(Policy = "SNMP_PROFILE_WRITE")]
        public async Task<ActionResult<SnmpCredentialViewDto>> Create(SnmpCredentialEditDto dto)
        {
            var profile = new SnmpCredential();
            var error = Apply(profile, dto, isNew: true);
            if (error != null) return BadRequest(error);

            profile.ModifiePar = User.Identity?.Name;
            _context.SnmpCredentials.Add(profile);
            _auditService.LogAction("SNMP_CREDENTIAL_CREATE", $"Création du profil d'identifiants SNMP « {profile.Name} » (v{profile.Version})", User.Identity?.Name, "WARNING", resourceName: profile.Name);
            await _context.SaveChangesAsync();
            return CreatedAtAction(nameof(Get), new { id = profile.Id }, ToView(profile, 0, 0));
        }

        [HttpPut("{id:int}")]
        [Authorize(Policy = "SNMP_PROFILE_WRITE")]
        public async Task<IActionResult> Update(int id, SnmpCredentialEditDto dto)
        {
            var profile = await _context.SnmpCredentials.FirstOrDefaultAsync(c => c.Id == id);
            if (profile == null) return NotFound();

            var error = Apply(profile, dto, isNew: false);
            if (error != null) return BadRequest(error);

            profile.ModifiePar = User.Identity?.Name;
            profile.DateModification = DateTime.UtcNow;
            _auditService.LogAction("SNMP_CREDENTIAL_UPDATE", $"Modification du profil d'identifiants SNMP « {profile.Name} »", User.Identity?.Name, "WARNING", resourceName: profile.Name);
            await _context.SaveChangesAsync();
            return NoContent();
        }

        [HttpDelete("{id:int}")]
        [Authorize(Policy = "SNMP_PROFILE_DELETE")]
        public async Task<IActionResult> Delete(int id)
        {
            var profile = await _context.SnmpCredentials.FirstOrDefaultAsync(c => c.Id == id);
            if (profile == null) return NotFound();

            // Les modèles et imprimantes qui l'utilisaient reviennent à la valeur par défaut (clé étrangère ON DELETE SET NULL)
            int models = await _context.Modeles.CountAsync(m => m.SnmpCredentialId == id);
            int printers = await _context.Imprimantes.CountAsync(i => i.SnmpCredentialId == id);

            _context.SnmpCredentials.Remove(profile);
            _auditService.LogAction("SNMP_CREDENTIAL_DELETE", $"Suppression du profil d'identifiants SNMP « {profile.Name} » ({models} modèle(s), {printers} imprimante(s) concernés)", User.Identity?.Name, "WARNING", resourceName: profile.Name);
            await _context.SaveChangesAsync();
            return Ok(new { message = $"Profil supprimé. {models} modèle(s) et {printers} imprimante(s) utilisent désormais leurs paramètres par défaut." });
        }

        private string? Apply(SnmpCredential profile, SnmpCredentialEditDto dto, bool isNew)
        {
            if (string.IsNullOrWhiteSpace(dto.Name)) return "Nom obligatoire.";
            if (dto.Version is < 1 or > 3) return "Version SNMP invalide.";

            profile.Name = dto.Name.Trim();
            profile.Description = dto.Description?.Trim();
            profile.Version = dto.Version;

            if (dto.Version is 1 or 2)
            {
                if (!string.IsNullOrEmpty(dto.Community)) profile.Community = _secrets.Protect(dto.Community.Trim());
                if (string.IsNullOrEmpty(profile.Community)) return "Communauté SNMP obligatoire.";
                // Plus de secrets v3 inutiles en base
                profile.V3AuthPassword = null;
                profile.V3PrivPassword = null;
                return null;
            }

            // SNMPv3
            if (string.IsNullOrWhiteSpace(dto.V3Username)) return "Nom d'utilisateur SNMPv3 obligatoire.";
            profile.V3Username = dto.V3Username.Trim();
            profile.V3SecurityLevel = dto.V3SecurityLevel;
            profile.V3AuthProtocol = dto.V3AuthProtocol;
            profile.V3PrivProtocol = dto.V3PrivProtocol;
            profile.Community = null;

            if (!string.IsNullOrEmpty(dto.V3AuthPassword))
            {
                if (dto.V3AuthPassword.Length < 8) return "Le mot de passe d'authentification SNMPv3 doit faire au moins 8 caractères.";
                profile.V3AuthPassword = _secrets.Protect(dto.V3AuthPassword);
            }
            if (!string.IsNullOrEmpty(dto.V3PrivPassword))
            {
                if (dto.V3PrivPassword.Length < 8) return "Le mot de passe de chiffrement SNMPv3 doit faire au moins 8 caractères.";
                profile.V3PrivPassword = _secrets.Protect(dto.V3PrivPassword);
            }

            if (dto.V3SecurityLevel == SnmpV3SecurityLevel.NoAuthNoPriv)
            {
                profile.V3AuthPassword = null;
                profile.V3PrivPassword = null;
            }
            else
            {
                if (string.IsNullOrEmpty(profile.V3AuthPassword)) return "Mot de passe d'authentification SNMPv3 obligatoire.";
                if (dto.V3SecurityLevel == SnmpV3SecurityLevel.AuthNoPriv) profile.V3PrivPassword = null;
                else if (string.IsNullOrEmpty(profile.V3PrivPassword)) return "Mot de passe de chiffrement SNMPv3 obligatoire (niveau « authentification + chiffrement »).";
            }
            return null;
        }

        private static SnmpCredentialViewDto ToView(SnmpCredential p, int models, int printers) => new()
        {
            Id = p.Id,
            Name = p.Name,
            Description = p.Description,
            Version = p.Version,
            V3Username = p.V3Username,
            V3SecurityLevel = p.V3SecurityLevel,
            V3AuthProtocol = p.V3AuthProtocol,
            V3PrivProtocol = p.V3PrivProtocol,
            HasCommunity = !string.IsNullOrEmpty(p.Community),
            HasAuthPassword = !string.IsNullOrEmpty(p.V3AuthPassword),
            HasPrivPassword = !string.IsNullOrEmpty(p.V3PrivPassword),
            ModelCount = models,
            PrinterCount = printers
        };
    }
}
