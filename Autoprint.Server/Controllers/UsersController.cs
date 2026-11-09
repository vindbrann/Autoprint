using System.Security.Cryptography;
using System.Text;
using Autoprint.Server.Data;
using Autoprint.Server.Helpers;
using Autoprint.Server.Models.Security;
using Autoprint.Shared;
using Autoprint.Shared.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Autoprint.Server.Services;

namespace Autoprint.Server.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class UsersController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly AuditService _auditService;

        public UsersController(ApplicationDbContext context, AuditService auditService)
        {
            _context = context;
            _auditService = auditService;
        }

        [HttpGet]
        [Authorize(Policy = "USER_READ")]
        public async Task<ActionResult<IEnumerable<object>>> GetUsers()
        {
            var users = await _context.Users
                .Include(u => u.UserRoles).ThenInclude(ur => ur.Role)
                .Select(u => new UserViewDto
                {
                    Id = u.Id,
                    Username = u.Username,
                    DisplayName = u.DisplayName,
                    Email = u.Email,
                    IsAdUser = u.IsAdUser,
                    IsActive = u.IsActive,
                    ForceChangePassword = u.ForceChangePassword,
                    LastLogin = u.LastLogin,
                    Roles = u.UserRoles.Select(ur => ur.Role.Name).ToList()
                })
                .ToListAsync();

            return Ok(users);
        }

        [HttpGet("{id}")]
        [Authorize(Policy = "USER_READ")]
        public async Task<ActionResult<User>> GetUser(int id)
        {
            var user = await _context.Users
                .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
                .FirstOrDefaultAsync(u => u.Id == id);

            if (user == null) return NotFound();

            user.PasswordHash = null;
            return user;
        }

        [HttpPost]
        [Authorize(Policy = "USER_WRITE")]
        public async Task<ActionResult<User>> CreateUser(CreateUserDto request)
        {
            if (await _context.Users.AnyAsync(u => u.Username == request.Username))
                return BadRequest("Ce nom d'utilisateur existe déjà.");

            if (request.RoleId > 0 && !await PermissionGuard.CanManageRolesAsync(_context, User, new[] { request.RoleId }))
                return StatusCode(403, "Vous ne pouvez pas attribuer un rôle qui a « Administration » ou « Paramètres ».");

            if (string.Equals(request.Password, request.Username, StringComparison.OrdinalIgnoreCase))
                return BadRequest("Le mot de passe ne peut pas être identique au nom d'utilisateur.");

            var newUser = new User
            {
                Username = request.Username,
                DisplayName = request.DisplayName,
                Email = request.Email,
                IsAdUser = false,
                IsActive = true,
                LastLogin = DateTime.MinValue,
                // Choix explicite de l'administrateur (avant : changement toujours imposé, faute de date de dernier changement)
                ForceChangePassword = request.ForceChangePassword,
                LastPasswordChangeDate = DateTime.UtcNow
            };

            if (!string.IsNullOrEmpty(request.Password))
                newUser.PasswordHash = SecurityHelper.HashPassword(request.Password);

            _context.Users.Add(newUser);

            _auditService.LogAction(
                "USER_CREATE",
                $"Création utilisateur : {newUser.Username} ({newUser.DisplayName})",
                User.Identity?.Name,
                "WARNING",
                resourceName: newUser.Username);

            await _context.SaveChangesAsync();

            if (request.RoleId > 0)
            {
                _context.UserRoles.Add(new UserRole { UserId = newUser.Id, RoleId = request.RoleId });
                await _context.SaveChangesAsync();
            }

            // On ne renvoie jamais le hachage du mot de passe
            return CreatedAtAction(nameof(GetUser), new { id = newUser.Id }, new { newUser.Id, newUser.Username, newUser.DisplayName, newUser.Email, newUser.IsActive });
        }

        [HttpPut("{id}")]
        [Authorize(Policy = "USER_WRITE")]
        public async Task<IActionResult> UpdateUser(int id, UpdateUserDto request)
        {
            var user = await _context.Users
                .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
                .FirstOrDefaultAsync(u => u.Id == id);

            if (user == null) return NotFound();

            if (!await PermissionGuard.CanManageUserAsync(_context, User, id))
                return StatusCode(403, "Ce compte a « Administration » ou « Paramètres » : vous ne pouvez pas le modifier.");

            if (request.RoleId > 0 && !await PermissionGuard.CanManageRolesAsync(_context, User, new[] { request.RoleId }))
                return StatusCode(403, "Vous ne pouvez pas attribuer un rôle qui a « Administration » ou « Paramètres ».");

            var snapshotBefore = new
            {
                user.Username,
                user.DisplayName,
                user.Email,
                user.IsActive,
                user.ForceChangePassword,
                Roles = user.UserRoles.Select(ur => ur.Role.Name).ToList()
            };

            // Désactiver le dernier administrateur, ou lui retirer son rôle d'administrateur, rendrait l'application ingérable
            int previousRoleId = user.UserRoles.FirstOrDefault()?.RoleId ?? 0;
            bool losesAdmin = (user.IsActive && !request.IsActive)
                || (request.RoleId > 0 && request.RoleId != previousRoleId && !await PermissionGuard.RoleHasAdminAsync(_context, request.RoleId));
            if (losesAdmin && await PermissionGuard.UserIsAdminAsync(_context, id) && !await PermissionGuard.HasOtherActiveAdminAsync(_context, excludeUserId: id))
                return BadRequest("Impossible : ce compte est le dernier administrateur actif.");

            bool isPasswordReset = !string.IsNullOrWhiteSpace(request.NewPassword);
            if (isPasswordReset && string.Equals(request.NewPassword, user.Username, StringComparison.OrdinalIgnoreCase))
                return BadRequest("Le mot de passe ne peut pas être identique au nom d'utilisateur.");
            bool isForceChangeToggled = user.ForceChangePassword != request.ForceChangePassword;
            bool isStatusChanged = user.IsActive != request.IsActive;

            user.DisplayName = request.DisplayName;
            user.Email = request.Email;
            user.IsActive = request.IsActive;
            user.ForceChangePassword = request.ForceChangePassword;

            if (isPasswordReset)
            {
                user.PasswordHash = SecurityHelper.HashPassword(request.NewPassword!);
                user.LastPasswordChangeDate = DateTime.UtcNow;
            }

            int currentRoleId = user.UserRoles.FirstOrDefault()?.RoleId ?? 0;
            bool isRoleChanged = request.RoleId > 0 && request.RoleId != currentRoleId;

            // Toute modification sensible coupe les sessions ouvertes de ce compte (audit #10)
            if (isPasswordReset || isStatusChanged || isRoleChanged || (isForceChangeToggled && request.ForceChangePassword))
            {
                user.RenewSecurityStamp();
            }

            if (request.RoleId > 0 && request.RoleId != currentRoleId)
            {
                var existingRoles = _context.UserRoles.Where(ur => ur.UserId == id);
                _context.UserRoles.RemoveRange(existingRoles);
                _context.UserRoles.Add(new UserRole { UserId = id, RoleId = request.RoleId });
            }

            string roleNameAfter;
            if (request.RoleId > 0 && request.RoleId != currentRoleId)
            {
                roleNameAfter = await _context.Roles
                   .Where(r => r.Id == request.RoleId)
                   .Select(r => r.Name)
                   .FirstOrDefaultAsync() ?? "Inconnu";
            }
            else
            {
                roleNameAfter = snapshotBefore.Roles.FirstOrDefault() ?? "Aucun";
            }

            var snapshotAfter = new
            {
                user.Username,
                user.DisplayName,
                user.Email,
                user.IsActive,
                user.ForceChangePassword,
                Roles = new List<string> { roleNameAfter }
            };

            string actionCode = "USER_UPDATE";
            List<string> detailsParts = new();

            if (isPasswordReset)
            {
                actionCode = "USER_PASSWORD_RESET";
                detailsParts.Add("Réinitialisation du mot de passe (Admin)");
            }
            if (isForceChangeToggled) detailsParts.Add($"Obligation changement MDP : {(request.ForceChangePassword ? "OUI" : "NON")}");
            if (isStatusChanged) detailsParts.Add($"Statut : {(request.IsActive ? "Actif" : "Inactif")}");

            if (detailsParts.Count == 0) detailsParts.Add("Mise à jour informations");

            _auditService.LogCustomAudit(
                actionCode,
                string.Join(" + ", detailsParts),
                User.Identity?.Name,
                user.Username,
                snapshotBefore,
                snapshotAfter,
                "WARNING"
            );

            await _context.SaveChangesAsync();
            return NoContent();
        }

        [HttpDelete("{id}")]
        [Authorize(Policy = "USER_DELETE")]
        public async Task<IActionResult> DeleteUser(int id)
        {
            var user = await _context.Users.FindAsync(id);
            if (user == null) return NotFound();

            if (user.Username == "admin") return BadRequest("Impossible de supprimer l'administrateur racine.");

            if (!await PermissionGuard.CanManageUserAsync(_context, User, id))
                return StatusCode(403, "Ce compte a « Administration » ou « Paramètres » : vous ne pouvez pas le supprimer.");

            if (await PermissionGuard.UserIsAdminAsync(_context, id) && !await PermissionGuard.HasOtherActiveAdminAsync(_context, excludeUserId: id))
                return BadRequest("Impossible : ce compte est le dernier administrateur actif.");

            _auditService.LogAction(
                "USER_DELETE",
                $"Suppression utilisateur : {user.Username}",
                User.Identity?.Name,
                "ERROR",
                resourceName: user.Username);

            _context.Users.Remove(user);
            await _context.SaveChangesAsync();
            return NoContent();
        }

        [HttpGet("profile")]
        [Authorize]
        public async Task<ActionResult<UserProfileDto>> GetMyProfile()
        {
            var username = User.Identity?.Name;
            if (string.IsNullOrEmpty(username)) return Unauthorized();

            var user = await _context.Users
                .Include(u => u.UserRoles).ThenInclude(ur => ur.Role)
                .FirstOrDefaultAsync(u => u.Username == username);

            if (user == null) return NotFound();

            var setting = await _context.ServerSettings.FirstOrDefaultAsync(s => s.Key == "PasswordExpirationDays");
            int maxDays = setting != null && int.TryParse(setting.Value, out int d) ? d : 90;

            int daysRemaining = 0;
            bool isExpired = false;

            if (user.LastPasswordChangeDate.HasValue)
            {
                var expirationDate = user.LastPasswordChangeDate.Value.AddDays(maxDays);
                daysRemaining = (int)(expirationDate - DateTime.UtcNow).TotalDays;
                if (daysRemaining < 0) isExpired = true;
            }

            return new UserProfileDto
            {
                Username = user.Username,
                DisplayName = user.DisplayName,
                Email = user.Email,
                Role = user.UserRoles.FirstOrDefault()?.Role?.Name ?? "Aucun",
                IsAdUser = user.IsAdUser,
                DaysUntilExpiration = daysRemaining,
                PasswordExpired = isExpired
            };
        }

        [HttpPut("change-password")]
        [Authorize]
        public async Task<IActionResult> ChangePassword(ChangePasswordDto request)
        {
            var username = User.Identity?.Name;
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Username == username);
            if (user == null) return NotFound();

            if (!SecurityHelper.VerifyPassword(user.PasswordHash ?? "", request.CurrentPassword))
                return BadRequest("Le mot de passe actuel est incorrect.");

            if (request.NewPassword == request.CurrentPassword)
                return BadRequest("Le nouveau mot de passe doit être différent de l'actuel.");

            if (string.Equals(request.NewPassword, user.Username, StringComparison.OrdinalIgnoreCase))
                return BadRequest("Le mot de passe ne peut pas être identique au nom d'utilisateur.");

            user.PasswordHash = SecurityHelper.HashPassword(request.NewPassword);
            user.LastPasswordChangeDate = DateTime.UtcNow;
            user.RenewSecurityStamp();

            user.ForceChangePassword = false;

            _auditService.LogAction(
                "USER_PWD_CHANGE",
                "Changement de mot de passe utilisateur",
                user.Username,
                "INFO",
                resourceName: user.Username);

            await _context.SaveChangesAsync();
            return NoContent();
        }
    }
}