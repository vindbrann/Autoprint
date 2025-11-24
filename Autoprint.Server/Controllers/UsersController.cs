using System.Security.Cryptography;
using System.Text;
using Autoprint.Server.Data;
using Autoprint.Server.Helpers;
using Autoprint.Server.Models.Security;
using Autoprint.Shared; // Pour AuditLog
using Autoprint.Shared.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Autoprint.Server.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class UsersController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public UsersController(ApplicationDbContext context)
        {
            _context = context;
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
            {
                return BadRequest("Ce nom d'utilisateur existe déjà.");
            }

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
            {
                newUser.PasswordHash = SecurityHelper.ComputeSha256Hash(request.Password);
            }

            _context.Users.Add(newUser);

            // --- LOG AUDIT ---
            _context.AuditLogs.Add(new AuditLog
            {
                Action = "USER_CREATE",
                Details = $"Création utilisateur : {newUser.Username} ({newUser.DisplayName})",
                Utilisateur = User.Identity?.Name ?? "Inconnu",
                Niveau = "WARNING", // Créer un user est une action sensible
                DateAction = DateTime.UtcNow
            });
            // -----------------

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
            var user = await _context.Users.FindAsync(id);
            if (user == null) return NotFound();

            user.DisplayName = request.DisplayName;
            user.Email = request.Email;

            // Log changement statut
            if (user.IsActive != request.IsActive)
            {
                _context.AuditLogs.Add(new AuditLog
                {
                    Action = "USER_UPDATE",
                    Details = $"Statut utilisateur {user.Username} modifié : {(request.IsActive ? "Activé" : "Désactivé")}",
                    Utilisateur = User.Identity?.Name ?? "Inconnu",
                    Niveau = "WARNING",
                    DateAction = DateTime.UtcNow
                });
            }

            user.IsActive = request.IsActive;
            user.ForceChangePassword = request.ForceChangePassword;

            if (!string.IsNullOrWhiteSpace(request.NewPassword))
            {
                // Note: on ne logue PAS qu'on a changé le mot de passe ici pour éviter le spam log si c'est l'admin
                // Mais on pourrait le faire si on voulait être strict.
                user.PasswordHash = SecurityHelper.ComputeSha256Hash(request.NewPassword);
                user.LastPasswordChangeDate = DateTime.UtcNow;
            }

            if (request.RoleId > 0)
            {
                var existingRoles = _context.UserRoles.Where(ur => ur.UserId == id);
                _context.UserRoles.RemoveRange(existingRoles);
                _context.UserRoles.Add(new UserRole { UserId = id, RoleId = request.RoleId });
            }

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

            _context.Users.Remove(user);

            // --- LOG AUDIT ---
            _context.AuditLogs.Add(new AuditLog
            {
                Action = "USER_DELETE",
                Details = $"Suppression utilisateur : {user.Username}",
                Utilisateur = User.Identity?.Name ?? "Inconnu",
                Niveau = "ERROR", // Suppression user = critique
                DateAction = DateTime.UtcNow
            });
            // -----------------

            await _context.SaveChangesAsync();
            return NoContent();
        }

        // --- PARTIE PROFIL ---

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
            else
            {
                daysRemaining = 0;
                isExpired = true;
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

            user.PasswordHash = SecurityHelper.ComputeSha256Hash(request.NewPassword);
            user.LastPasswordChangeDate = DateTime.UtcNow;

            // --- LOG AUDIT ---
            _context.AuditLogs.Add(new AuditLog
            {
                Action = "USER_PWD_CHANGE",
                Details = "Changement de mot de passe utilisateur",
                Utilisateur = user.Username, // C'est l'utilisateur lui-même
                Niveau = "INFO",
                DateAction = DateTime.UtcNow
            });
            // -----------------

            await _context.SaveChangesAsync();
            return NoContent();
        }
    }
}