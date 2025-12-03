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

        // GET: api/users
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
                    LastLogin = u.LastLogin,
                    Roles = u.UserRoles.Select(ur => ur.Role.Name).ToList()
                })
                .ToListAsync();

            return Ok(users);
        }

        // GET: api/users/5
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

        // POST: api/users
        [HttpPost]
        [Authorize(Policy = "USER_WRITE")]
        public async Task<ActionResult<User>> CreateUser(CreateUserDto request)
        {
            if (await _context.Users.AnyAsync(u => u.Username == request.Username))
                return BadRequest("Ce nom d'utilisateur existe déjà.");

            var newUser = new User
            {
                Username = request.Username,
                DisplayName = request.DisplayName,
                Email = request.Email,
                IsAdUser = false,
                IsActive = true,
                LastLogin = DateTime.MinValue
            };

            if (!string.IsNullOrEmpty(request.Password))
                newUser.PasswordHash = SecurityHelper.ComputeSha256Hash(request.Password);

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

            return CreatedAtAction(nameof(GetUser), new { id = newUser.Id }, newUser);
        }

        // PUT: api/users/5
        [HttpPut("{id}")]
        [Authorize(Policy = "USER_WRITE")]
        public async Task<IActionResult> UpdateUser(int id, UpdateUserDto request)
        {
            var user = await _context.Users
                .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
                .FirstOrDefaultAsync(u => u.Id == id);

            if (user == null) return NotFound();

            // 1. Snapshot "Avant"
            var snapshotBefore = new
            {
                user.Username,
                user.DisplayName,
                user.Email,
                user.IsActive,
                user.ForceChangePassword,
                Roles = user.UserRoles.Select(ur => ur.Role.Name).ToList()
            };

            // 2. Détection changements
            bool isPasswordReset = !string.IsNullOrWhiteSpace(request.NewPassword);
            bool isForceChangeToggled = user.ForceChangePassword != request.ForceChangePassword;
            bool isStatusChanged = user.IsActive != request.IsActive;

            // 3. Mise à jour
            user.DisplayName = request.DisplayName;
            user.Email = request.Email;
            user.IsActive = request.IsActive;
            user.ForceChangePassword = request.ForceChangePassword;

            if (isPasswordReset)
            {
                user.PasswordHash = SecurityHelper.ComputeSha256Hash(request.NewPassword);
                user.LastPasswordChangeDate = DateTime.UtcNow;
            }

            // Gestion des Rôles
            int currentRoleId = user.UserRoles.FirstOrDefault()?.RoleId ?? 0;

            if (request.RoleId > 0 && request.RoleId != currentRoleId)
            {
                var existingRoles = _context.UserRoles.Where(ur => ur.UserId == id);
                _context.UserRoles.RemoveRange(existingRoles);
                _context.UserRoles.Add(new UserRole { UserId = id, RoleId = request.RoleId });
            }

            // 4. Snapshot "Après"
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

            // 5. Log
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

        // DELETE: api/users/5
        [HttpDelete("{id}")]
        [Authorize(Policy = "USER_DELETE")]
        public async Task<IActionResult> DeleteUser(int id)
        {
            var user = await _context.Users.FindAsync(id);
            if (user == null) return NotFound();

            if (user.Username == "admin") return BadRequest("Impossible de supprimer l'administrateur racine.");

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

            var oldHash = SecurityHelper.ComputeSha256Hash(request.CurrentPassword);
            if (user.PasswordHash != oldHash)
                return BadRequest("Le mot de passe actuel est incorrect.");

            // MAJ Hash & Date
            user.PasswordHash = SecurityHelper.ComputeSha256Hash(request.NewPassword);
            user.LastPasswordChangeDate = DateTime.UtcNow;

            // CORRECTION : On retire le flag de force !
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