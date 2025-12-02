using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using Autoprint.Server.Data;
using Autoprint.Server.Models.Security;
using Autoprint.Shared.DTOs;
using Autoprint.Shared;
using Autoprint.Server.Services; // Injection

namespace Autoprint.Server.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(Policy = "ROLE_READ")]
    public class RolesController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly IAuthService _authService;
        private readonly AuditService _auditService; // Injection

        public RolesController(ApplicationDbContext context, IAuthService authService, AuditService auditService)
        {
            _context = context;
            _authService = authService;
            _auditService = auditService;
        }

        // ... (GetPermissions, GetRoles, GetRoleForEdit inchangés) ...
        [HttpGet("permissions")]
        public async Task<ActionResult<List<PermissionDto>>> GetAllPermissions()
        {
            return await _context.Permissions
                .Select(p => new PermissionDto { Id = p.Id, Code = p.Code, Description = p.Description })
                .ToListAsync();
        }

        [HttpGet]
        public async Task<ActionResult<List<RoleViewDto>>> GetRoles()
        {
            return await _context.Roles
                .Include(r => r.RolePermissions).ThenInclude(rp => rp.Permission)
                .Select(r => new RoleViewDto
                {
                    Id = r.Id,
                    Name = r.Name,
                    Description = r.Description,
                    PermissionCodes = r.RolePermissions.Select(rp => rp.Permission.Code).ToList()
                })
                .ToListAsync();
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<RoleEditDto>> GetRoleForEdit(int id)
        {
            var role = await _context.Roles.Include(r => r.RolePermissions).FirstOrDefaultAsync(r => r.Id == id);
            if (role == null) return NotFound();

            return new RoleEditDto
            {
                Name = role.Name,
                Description = role.Description,
                PermissionIds = role.RolePermissions.Select(rp => rp.PermissionId).ToList()
            };
        }

        [HttpPost]
        [Authorize(Policy = "ROLE_WRITE")]
        public async Task<IActionResult> CreateRole(RoleEditDto request)
        {
            var newRole = new Role { Name = request.Name, Description = request.Description };
            _context.Roles.Add(newRole);

            _auditService.LogAction("ROLE_CREATE", $"Création rôle : {newRole.Name}", User.Identity?.Name, "WARNING", newRole.Name);

            await _context.SaveChangesAsync();

            if (request.PermissionIds != null && request.PermissionIds.Any())
            {
                foreach (var permId in request.PermissionIds)
                    _context.RolePermissions.Add(new RolePermission { RoleId = newRole.Id, PermissionId = permId });
                await _context.SaveChangesAsync();
            }
            return Ok(newRole.Id);
        }

        [HttpPut("{id}")]
        [Authorize(Policy = "ROLE_WRITE")]
        public async Task<IActionResult> UpdateRole(int id, RoleEditDto request)
        {
            // 1. Charger le rôle et ses permissions actuelles (avec les Codes !)
            var role = await _context.Roles
                .Include(r => r.RolePermissions)
                .ThenInclude(rp => rp.Permission) // Important pour avoir le Code (ex: BRAND_READ)
                .FirstOrDefaultAsync(r => r.Id == id);

            if (role == null) return NotFound();
            if (role.Name == "SuperAdmin" && request.Name != "SuperAdmin") return BadRequest("Impossible de renommer SuperAdmin.");

            // 2. Snapshot "Avant" : On ne garde que les codes de permissions
            var snapshotBefore = new
            {
                Role = role.Name,
                Description = role.Description,
                Permissions = role.RolePermissions.Select(rp => rp.Permission.Code).OrderBy(c => c).ToList()
            };

            // 3. Mise à jour
            role.Name = request.Name;
            role.Description = request.Description;

            _context.RolePermissions.RemoveRange(role.RolePermissions);

            // On prépare la liste des NOUVEAUX codes pour le snapshot "Après"
            // Puisqu'on a que les IDs dans la request, on doit aller chercher les Codes correspondants en base
            List<string> newPermissionCodes = new();
            if (request.PermissionIds != null && request.PermissionIds.Any())
            {
                // Récupération des codes pour l'affichage audit
                newPermissionCodes = await _context.Permissions
                    .Where(p => request.PermissionIds.Contains(p.Id))
                    .Select(p => p.Code)
                    .OrderBy(c => c)
                    .ToListAsync();

                foreach (var permId in request.PermissionIds)
                    _context.RolePermissions.Add(new RolePermission { RoleId = role.Id, PermissionId = permId });
            }

            // 4. Snapshot "Après"
            var snapshotAfter = new
            {
                Role = role.Name,
                Description = role.Description,
                Permissions = newPermissionCodes
            };

            // 5. Audit Sur Mesure (Propre et Lisible)
            _auditService.LogCustomAudit(
                "ROLE_UPDATE",
                $"Modification rôle : {role.Name}",
                User.Identity?.Name,
                role.Name,
                snapshotBefore,
                snapshotAfter
            );

            await _context.SaveChangesAsync();
            return NoContent();
        }

        [HttpDelete("{id}")]
        [Authorize(Policy = "ROLE_WRITE")]
        public async Task<IActionResult> DeleteRole(int id)
        {
            var role = await _context.Roles.FindAsync(id);
            if (role == null) return NotFound();
            if (role.Name == "SuperAdmin") return BadRequest("Impossible de supprimer SuperAdmin.");

            _auditService.LogAction("ROLE_DELETE", $"Suppression rôle : {role.Name}", User.Identity?.Name, "WARNING", role.Name);

            _context.Roles.Remove(role);
            await _context.SaveChangesAsync();
            return NoContent();
        }

        // ... (Mapping AD : Utiliser LogAction) ...
        [HttpGet("ad/search")]
        [Authorize(Policy = "ROLE_WRITE")]
        public async Task<ActionResult<List<AdSearchResultDto>>> SearchAd([FromQuery] string q, [FromQuery] Shared.Enums.AdMappingType type)
        {
            if (!OperatingSystem.IsWindows()) return Ok(new List<AdSearchResultDto>());
            return Ok(await _authService.SearchAdAsync(q, type));
        }

        [HttpGet("{roleId}/admappings")]
        public async Task<ActionResult<List<AdRoleMappingDto>>> GetAdMappings(int roleId)
        {
            return await _context.AdRoleMappings.Where(m => m.RoleId == roleId)
                .Select(m => new AdRoleMappingDto { Id = m.Id, AdIdentifier = m.AdIdentifier, MappingType = m.MappingType, RoleId = m.RoleId })
                .ToListAsync();
        }

        [HttpPost("admappings")]
        [Authorize(Policy = "ROLE_WRITE")]
        public async Task<IActionResult> AddAdMapping(CreateAdMappingDto dto)
        {
            if (await _context.AdRoleMappings.AnyAsync(m => m.AdIdentifier == dto.AdIdentifier && m.RoleId == dto.RoleId))
                return BadRequest("Mapping existant.");

            var mapping = new AdRoleMapping { AdIdentifier = dto.AdIdentifier, MappingType = dto.MappingType, RoleId = dto.RoleId };
            _context.AdRoleMappings.Add(mapping);

            _auditService.LogAction("ROLE_MAP_ADD", $"Ajout lien AD : {dto.AdIdentifier}", User.Identity?.Name, "WARNING", dto.AdIdentifier);

            await _context.SaveChangesAsync();
            return Ok(mapping.Id);
        }

        [HttpDelete("admappings/{id}")]
        [Authorize(Policy = "ROLE_WRITE")]
        public async Task<IActionResult> DeleteAdMapping(int id)
        {
            var mapping = await _context.AdRoleMappings.FindAsync(id);
            if (mapping == null) return NotFound();

            _auditService.LogAction("ROLE_MAP_DEL", $"Suppression lien AD : {mapping.AdIdentifier}", User.Identity?.Name, "INFO", mapping.AdIdentifier);

            _context.AdRoleMappings.Remove(mapping);
            await _context.SaveChangesAsync();
            return NoContent();
        }
    }
}