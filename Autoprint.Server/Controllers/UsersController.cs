using System.Security.Cryptography;
using System.Text;
using Autoprint.Server.Data;
using Autoprint.Server.Helpers;
using Autoprint.Server.Models.Security;
using Autoprint.Shared.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Autoprint.Server.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    // ❌ ON ENLÈVE [Authorize(Policy = "USER_READ")] D'ICI
    // Pour éviter de bloquer l'accès au profil
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
                .Select(u => new UserViewDto // On mappe directement vers le DTO
                {
                    Id = u.Id,
                    Username = u.Username,
                    DisplayName = u.DisplayName,
                    Email = u.Email, // <-- IMPORTANT : On renvoie l'email
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
        [Authorize(Policy = "USER_READ")] // ✅ ET ICI (Admin seulement)
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
            user.IsActive = request.IsActive;

            user.ForceChangePassword = request.ForceChangePassword;
            if (!string.IsNullOrWhiteSpace(request.NewPassword))
            {

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
        [Authorize(Policy = "USER_DELETE")] // Celui-là était déjà bon
        public async Task<IActionResult> DeleteUser(int id)
        {
            // ... (Ton code DeleteUser reste identique)
            var user = await _context.Users.FindAsync(id);
            if (user == null) return NotFound();

            if (user.Username == "admin") return BadRequest("Impossible de supprimer l'administrateur racine.");

            _context.Users.Remove(user);
            await _context.SaveChangesAsync();
            return NoContent();
        }

        // --- PARTIE PROFIL (Accessible à tout utilisateur connecté) ---

        // GET: api/users/profile
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

            // --- CALCUL DE L'EXPIRATION ---
            // 1. On récupère le réglage (par défaut 90 jours)
            var setting = await _context.ServerSettings
                .FirstOrDefaultAsync(s => s.Key == "PasswordExpirationDays");

            int maxDays = setting != null && int.TryParse(setting.Value, out int d) ? d : 90;

            // 2. On calcule
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
                // Si jamais changé => Considéré comme expiré ou à changer immédiatement
                daysRemaining = 0;
                isExpired = true;
            }

            return new UserProfileDto
            {
                Username = user.Username,
                DisplayName = user.DisplayName,
                Email = user.Email,
                Role = user.UserRoles.FirstOrDefault()?.Role?.Name ?? "Aucun",
                DaysUntilExpiration = daysRemaining, // <-- Info envoyée au front
                PasswordExpired = isExpired
            };
        }

        // PUT: api/users/change-password (NOUVELLE ROUTE DÉDIÉE)
        [HttpPut("change-password")]
        [Authorize]
        public async Task<IActionResult> ChangePassword(ChangePasswordDto request)
        {
            var username = User.Identity?.Name;
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Username == username);
            if (user == null) return NotFound();

            // 1. Vérif ancien MDP
            var oldHash = SecurityHelper.ComputeSha256Hash(request.CurrentPassword);
            if (user.PasswordHash != oldHash)
                return BadRequest("Le mot de passe actuel est incorrect.");

            // 2. Application nouveau MDP
            user.PasswordHash = SecurityHelper.ComputeSha256Hash(request.NewPassword);
            user.LastPasswordChangeDate = DateTime.UtcNow; // Reset du compteur

            await _context.SaveChangesAsync();
            return NoContent();
        }
    }
}