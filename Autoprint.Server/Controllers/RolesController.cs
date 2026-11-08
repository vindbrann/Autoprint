using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using Autoprint.Server.Data;
using Autoprint.Server.Models.Security;
using Autoprint.Shared.DTOs;
using Autoprint.Shared;
using Autoprint.Server.Services; 

namespace Autoprint.Server.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(Policy = "ROLE_READ")]
    public class RolesController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly IAuthService _authService;
        private readonly AuditService _auditService; 

        public RolesController(ApplicationDbContext context, IAuthService authService, AuditService auditService)
        {
            _context = context;
            _authService = authService;
            _auditService = auditService;
        }

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
            if (!await PermissionGuard.CanGrantPermissionIdsAsync(_context, User, request.PermissionIds ?? new()))
                return StatusCode(403, "Vous ne pouvez pas accorder ces droits : « Administration » est réservée aux administrateurs, et « Paramètres » ne peut être accordé que par un compte qui l'a déjà.");

            var nameError = await ValidateRoleNameAsync(request.Name, null);
            if (nameError != null) return BadRequest(nameError);

            var newRole = new Role { Name = request.Name.Trim(), Description = request.Description };
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
            var role = await _context.Roles
                .Include(r => r.RolePermissions)
                .ThenInclude(rp => rp.Permission)
                .FirstOrDefaultAsync(r => r.Id == id);

            if (role == null) return NotFound();
            if (role.Name == "SuperAdmin" && request.Name != "SuperAdmin") return BadRequest("Impossible de renommer SuperAdmin.");

            if (!await PermissionGuard.CanManageRolesAsync(_context, User, new[] { id }))
                return StatusCode(403, "Ce rôle a « Administration » ou « Paramètres » : vous ne pouvez pas le modifier.");

            if (!await PermissionGuard.CanGrantPermissionIdsAsync(_context, User, request.PermissionIds ?? new()))
                return StatusCode(403, "Vous ne pouvez pas accorder ces droits : « Administration » est réservée aux administrateurs, et « Paramètres » ne peut être accordé que par un compte qui l'a déjà.");

            // Retirer Administration d'un rôle : il doit rester un autre administrateur actif
            bool hadAdmin = role.RolePermissions.Any(rp => rp.Permission.Code == PermissionGuard.AdminPermission);
            bool keepsAdmin = request.PermissionIds != null && await _context.Permissions
                .AnyAsync(p => request.PermissionIds.Contains(p.Id) && p.Code == PermissionGuard.AdminPermission);
            if (hadAdmin && !keepsAdmin && !await PermissionGuard.HasOtherActiveAdminAsync(_context, excludeRoleId: id))
                return BadRequest("Impossible de retirer « Administration » : ce rôle porte le dernier compte administrateur actif.");

            var snapshotBefore = new
            {
                Role = role.Name,
                Description = role.Description,
                Permissions = role.RolePermissions.Select(rp => rp.Permission.Code).OrderBy(c => c).ToList()
            };

            var nameError = await ValidateRoleNameAsync(request.Name, id);
            if (nameError != null) return BadRequest(nameError);

            role.Name = request.Name.Trim();
            role.Description = request.Description;

            _context.RolePermissions.RemoveRange(role.RolePermissions);

            List<string> newPermissionCodes = new();
            if (request.PermissionIds != null && request.PermissionIds.Any())
            {
                newPermissionCodes = await _context.Permissions
                    .Where(p => request.PermissionIds.Contains(p.Id))
                    .Select(p => p.Code)
                    .OrderBy(c => c)
                    .ToListAsync();

                foreach (var permId in request.PermissionIds)
                    _context.RolePermissions.Add(new RolePermission { RoleId = role.Id, PermissionId = permId });
            }

            var snapshotAfter = new
            {
                Role = role.Name,
                Description = role.Description,
                Permissions = newPermissionCodes
            };

            // Les droits portés par les jetons des membres de ce rôle ne sont plus à jour : on coupe leurs sessions (audit #10)
            await RenewStampsOfRoleMembersAsync(role.Id);

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

            if (!await PermissionGuard.CanManageRolesAsync(_context, User, new[] { id }))
                return StatusCode(403, "Ce rôle a « Administration » ou « Paramètres » : vous ne pouvez pas le supprimer.");

            if (await PermissionGuard.RoleHasAdminAsync(_context, id) && !await PermissionGuard.HasOtherActiveAdminAsync(_context, excludeRoleId: id))
                return BadRequest("Impossible de supprimer ce rôle : il porte le dernier compte administrateur actif.");

            _auditService.LogAction("ROLE_DELETE", $"Suppression rôle : {role.Name}", User.Identity?.Name, "WARNING", role.Name);

            await RenewStampsOfRoleMembersAsync(role.Id);
            _context.Roles.Remove(role);
            await _context.SaveChangesAsync();
            return NoContent();
        }

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

            if (!await PermissionGuard.CanManageRolesAsync(_context, User, new[] { dto.RoleId }))
                return StatusCode(403, "Vous ne pouvez pas lier un groupe AD à un rôle qui a « Administration » ou « Paramètres ».");

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

            if (!await PermissionGuard.CanManageRolesAsync(_context, User, new[] { mapping.RoleId }))
                return StatusCode(403, "Ce lien AD concerne un rôle qui a « Administration » ou « Paramètres ».");

            _auditService.LogAction("ROLE_MAP_DEL", $"Suppression lien AD : {mapping.AdIdentifier}", User.Identity?.Name, "INFO", mapping.AdIdentifier);

            _context.AdRoleMappings.Remove(mapping);
            await _context.SaveChangesAsync();
            return NoContent();
        }

        // Nom obligatoire et unique (sans tenir compte de la casse) : plusieurs écrans et contrôles reconnaissent SuperAdmin
        // par son nom ; deux rôles « SuperAdmin » (ou deux « Lecteur ») ne doivent pas pouvoir coexister (B69)
        private async Task<string?> ValidateRoleNameAsync(string? name, int? currentId)
        {
            var clean = name?.Trim();
            if (string.IsNullOrEmpty(clean)) return "Nom du rôle obligatoire.";
            if (clean.Length > 100) return "Nom du rôle trop long (100 caractères au maximum).";
            var lower = clean.ToLower();
            bool exists = await _context.Roles.AnyAsync(r => r.Name.ToLower() == lower && (currentId == null || r.Id != currentId));
            return exists ? $"Un rôle nommé « {clean} » existe déjà." : null;
        }

        private async Task RenewStampsOfRoleMembersAsync(int roleId)
        {
            var members = await _context.Users.Where(u => u.UserRoles.Any(ur => ur.RoleId == roleId)).ToListAsync();
            foreach (var member in members) member.RenewSecurityStamp();
        }
    }
}