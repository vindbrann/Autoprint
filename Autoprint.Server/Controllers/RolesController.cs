using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using Autoprint.Server.Data;
using Autoprint.Server.Models.Security;
using Autoprint.Shared.DTOs;

namespace Autoprint.Server.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(Policy = "ROLE_READ")]
    public class RolesController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public RolesController(ApplicationDbContext context)
        {
            _context = context;
        }

        // --- GESTION DES PERMISSIONS (Pour remplir les cases à cocher) ---

        [HttpGet("permissions")]
        public async Task<ActionResult<List<PermissionDto>>> GetAllPermissions()
        {
            return await _context.Permissions
                .Select(p => new PermissionDto
                {
                    Id = p.Id,
                    Code = p.Code,
                    Description = p.Description
                })
                .ToListAsync();
        }

        // --- GESTION DES RÔLES ---

        // GET: api/roles
        [HttpGet]
        public async Task<ActionResult<List<RoleViewDto>>> GetRoles()
        {
            return await _context.Roles
                .Include(r => r.RolePermissions)
                .ThenInclude(rp => rp.Permission)
                .Select(r => new RoleViewDto
                {
                    Id = r.Id,
                    Name = r.Name,
                    Description = r.Description,
                    PermissionCodes = r.RolePermissions.Select(rp => rp.Permission.Code).ToList()
                })
                .ToListAsync();
        }

        // GET: api/roles/5
        [HttpGet("{id}")]
        public async Task<ActionResult<RoleEditDto>> GetRoleForEdit(int id)
        {
            var role = await _context.Roles
                .Include(r => r.RolePermissions)
                .FirstOrDefaultAsync(r => r.Id == id);

            if (role == null) return NotFound();

            return new RoleEditDto
            {
                Name = role.Name,
                Description = role.Description,
                PermissionIds = role.RolePermissions.Select(rp => rp.PermissionId).ToList()
            };
        }

        // POST: api/roles
        [HttpPost]
        [Authorize(Policy = "ROLE_WRITE")]
        public async Task<IActionResult> CreateRole(RoleEditDto request)
        {
            // 1. Créer le rôle
            var newRole = new Role
            {
                Name = request.Name,
                Description = request.Description
            };
            _context.Roles.Add(newRole);
            await _context.SaveChangesAsync(); // Pour avoir l'ID

            // 2. Ajouter les permissions
            if (request.PermissionIds != null && request.PermissionIds.Any())
            {
                foreach (var permId in request.PermissionIds)
                {
                    _context.RolePermissions.Add(new RolePermission
                    {
                        RoleId = newRole.Id,
                        PermissionId = permId
                    });
                }
                await _context.SaveChangesAsync();
            }

            return Ok(newRole.Id);
        }

        // PUT: api/roles/5
        [HttpPut("{id}")]
        [Authorize(Policy = "ROLE_WRITE")]
        public async Task<IActionResult> UpdateRole(int id, RoleEditDto request)
        {
            var role = await _context.Roles.Include(r => r.RolePermissions).FirstOrDefaultAsync(r => r.Id == id);
            if (role == null) return NotFound();

            // Protection : Empêcher de casser le SuperAdmin
            if (role.Name == "SuperAdmin" && request.Name != "SuperAdmin")
            {
                return BadRequest("On ne peut pas renommer le rôle SuperAdmin.");
            }

            // 1. Update infos de base
            role.Name = request.Name;
            role.Description = request.Description;

            // 2. Update Permissions (On efface tout et on remet)
            _context.RolePermissions.RemoveRange(role.RolePermissions);

            if (request.PermissionIds != null)
            {
                foreach (var permId in request.PermissionIds)
                {
                    _context.RolePermissions.Add(new RolePermission
                    {
                        RoleId = role.Id,
                        PermissionId = permId
                    });
                }
            }

            await _context.SaveChangesAsync();
            return NoContent();
        }

        // DELETE: api/roles/5
        [HttpDelete("{id}")]
        [Authorize(Policy = "ROLE_WRITE")]
        public async Task<IActionResult> DeleteRole(int id)
        {
            var role = await _context.Roles.FindAsync(id);
            if (role == null) return NotFound();

            if (role.Name == "SuperAdmin") return BadRequest("Impossible de supprimer le SuperAdmin.");

            _context.Roles.Remove(role);
            await _context.SaveChangesAsync();
            return NoContent();
        }
    }
}