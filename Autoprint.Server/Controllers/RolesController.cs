using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using Autoprint.Server.Data;
using Autoprint.Server.Models.Security;
using Autoprint.Shared.DTOs;
using Autoprint.Shared; // Pour AuditLog
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

        public RolesController(ApplicationDbContext context, IAuthService authService)
        {
            _context = context;
            _authService = authService;
        }

        // --- GESTION DES PERMISSIONS ---
        [HttpGet("permissions")]
        public async Task<ActionResult<List<PermissionDto>>> GetAllPermissions()
        {
            return await _context.Permissions
                .Select(p => new PermissionDto { Id = p.Id, Code = p.Code, Description = p.Description })
                .ToListAsync();
        }

        // --- GESTION DES RÔLES ---
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

            // LOG AUDIT
            _context.AuditLogs.Add(new AuditLog { Action = "ROLE_CREATE", Details = $"Création rôle : {newRole.Name}", Utilisateur = User.Identity?.Name ?? "Inconnu", Niveau = "WARNING", DateAction = DateTime.UtcNow });

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
            var role = await _context.Roles.Include(r => r.RolePermissions).FirstOrDefaultAsync(r => r.Id == id);
            if (role == null) return NotFound();

            if (role.Name == "SuperAdmin" && request.Name != "SuperAdmin") return BadRequest("Impossible de renommer SuperAdmin.");

            role.Name = request.Name;
            role.Description = request.Description;

            // LOG AUDIT
            _context.AuditLogs.Add(new AuditLog { Action = "ROLE_UPDATE", Details = $"Modification rôle : {role.Name}", Utilisateur = User.Identity?.Name ?? "Inconnu", Niveau = "INFO", DateAction = DateTime.UtcNow });

            _context.RolePermissions.RemoveRange(role.RolePermissions);
            if (request.PermissionIds != null)
            {
                foreach (var permId in request.PermissionIds)
                    _context.RolePermissions.Add(new RolePermission { RoleId = role.Id, PermissionId = permId });
            }

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

            // LOG AUDIT
            _context.AuditLogs.Add(new AuditLog { Action = "ROLE_DELETE", Details = $"Suppression rôle : {role.Name}", Utilisateur = User.Identity?.Name ?? "Inconnu", Niveau = "WARNING", DateAction = DateTime.UtcNow });

            _context.Roles.Remove(role);
            await _context.SaveChangesAsync();
            return NoContent();
        }

        // --- GESTION MAPPING AD ---
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

            // LOG AUDIT
            _context.AuditLogs.Add(new AuditLog { Action = "ROLE_MAP_ADD", Details = $"Ajout lien AD : {dto.AdIdentifier} -> Rôle ID {dto.RoleId}", Utilisateur = User.Identity?.Name ?? "Inconnu", Niveau = "WARNING", DateAction = DateTime.UtcNow });

            await _context.SaveChangesAsync();
            return Ok(mapping.Id);
        }

        [HttpDelete("admappings/{id}")]
        [Authorize(Policy = "ROLE_WRITE")]
        public async Task<IActionResult> DeleteAdMapping(int id)
        {
            var mapping = await _context.AdRoleMappings.FindAsync(id);
            if (mapping == null) return NotFound();

            // LOG AUDIT
            _context.AuditLogs.Add(new AuditLog { Action = "ROLE_MAP_DEL", Details = $"Suppression lien AD : {mapping.AdIdentifier}", Utilisateur = User.Identity?.Name ?? "Inconnu", Niveau = "INFO", DateAction = DateTime.UtcNow });

            _context.AdRoleMappings.Remove(mapping);
            await _context.SaveChangesAsync();
            return NoContent();
        }
    }
}