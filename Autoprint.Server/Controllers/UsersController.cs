using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using System.Security.Cryptography;
using System.Text;
using Autoprint.Server.Data;
using Autoprint.Server.Models.Security;
using Autoprint.Shared; // Si tu as des DTOs ici, sinon voir classe en bas

namespace Autoprint.Server.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(Policy = "USER_READ")]
    public class UsersController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public UsersController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: api/users
        [HttpGet]
        public async Task<ActionResult<IEnumerable<object>>> GetUsers()
        {
            // On récupère les users avec leurs rôles
            // On projette dans un objet anonyme pour NE PAS renvoyer le PasswordHash au client (Sécurité)
            var users = await _context.Users
                .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
                .Select(u => new
                {
                    u.Id,
                    u.Username,
                    u.DisplayName,
                    u.IsAdUser,
                    u.IsActive,
                    u.LastLogin,
                    Roles = u.UserRoles.Select(ur => ur.Role.Name).ToList()
                })
                .ToListAsync();

            return Ok(users);
        }

        // GET: api/users/5
        [HttpGet("{id}")]
        public async Task<ActionResult<User>> GetUser(int id)
        {
            var user = await _context.Users
                .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
                .FirstOrDefaultAsync(u => u.Id == id);

            if (user == null)
            {
                return NotFound();
            }

            // Petit nettoyage de sécurité avant l'envoi
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

            // 1. Création de l'objet User
            var newUser = new User
            {
                Username = request.Username,
                DisplayName = request.DisplayName,
                IsAdUser = false, // Création locale par défaut ici
                IsActive = true,
                LastLogin = DateTime.MinValue
            };

            // 2. Hachage du mot de passe (SHA256 standard)
            if (!string.IsNullOrEmpty(request.Password))
            {
                newUser.PasswordHash = ComputeSha256Hash(request.Password);
            }

            _context.Users.Add(newUser);
            await _context.SaveChangesAsync();

            // 3. Assignation du Rôle (si fourni, sinon utilisateur par défaut ?)
            if (request.RoleId > 0)
            {
                var userRole = new UserRole
                {
                    UserId = newUser.Id,
                    RoleId = request.RoleId
                };
                _context.UserRoles.Add(userRole);
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
            if (user == null)
            {
                return NotFound();
            }

            // Mise à jour des champs simples
            user.DisplayName = request.DisplayName;
            user.IsActive = request.IsActive;

            // Mise à jour du mot de passe (seulement si un nouveau est fourni)
            if (!string.IsNullOrWhiteSpace(request.NewPassword))
            {
                user.PasswordHash = ComputeSha256Hash(request.NewPassword);
            }

            // Gestion des rôles (Simplifié : on supprime tout et on remet le nouveau pour l'instant)
            // Note : Une gestion plus fine serait nécessaire pour du multi-rôle complexe
            if (request.RoleId > 0)
            {
                // 1. Nettoyer les rôles existants
                var existingRoles = _context.UserRoles.Where(ur => ur.UserId == id);
                _context.UserRoles.RemoveRange(existingRoles);

                // 2. Ajouter le nouveau
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
            if (user == null)
            {
                return NotFound();
            }

            // Protection : On empêche de supprimer le dernier admin ou soi-même (à améliorer)
            if (user.Username == "admin")
            {
                return BadRequest("Impossible de supprimer l'administrateur racine.");
            }

            _context.Users.Remove(user);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        // --- Helper Privé pour le Hachage (Même logique que AuthController/Service) ---
        private static string ComputeSha256Hash(string rawData)
        {
            using (SHA256 sha256Hash = SHA256.Create())
            {
                // ComputeHash - returns byte array
                byte[] bytes = sha256Hash.ComputeHash(Encoding.UTF8.GetBytes(rawData));

                // Convert byte array to a string
                return Convert.ToBase64String(bytes);
            }
        }
    }

    // --- DTOs (Data Transfer Objects) ---
    // Tu peux les déplacer dans Autoprint.Shared plus tard si tu veux les utiliser côté Blazor
    public class CreateUserDto
    {
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public int RoleId { get; set; } // L'ID du rôle à donner (ex: 1 pour Admin)
    }

    public class UpdateUserDto
    {
        public string DisplayName { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public string? NewPassword { get; set; } // Optionnel
        public int RoleId { get; set; }
    }
}