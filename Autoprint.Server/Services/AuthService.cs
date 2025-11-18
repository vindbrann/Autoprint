using System.DirectoryServices.AccountManagement;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Autoprint.Server.Data;
using Autoprint.Server.Helpers;
using Autoprint.Server.Models.Security;
using Autoprint.Shared;
using Azure.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

namespace Autoprint.Server.Services
{
    public interface IAuthService
    {
        Task<LoginResponse?> LoginAsync(LoginRequest request);
    }

    public class AuthService : IAuthService
    {
        private readonly ApplicationDbContext _context;
        private readonly IConfiguration _configuration;

        public AuthService(ApplicationDbContext context, IConfiguration configuration)
        {
            _context = context;
            _configuration = configuration;
        }

        public async Task<LoginResponse?> LoginAsync(LoginRequest request)
        {
            User? user = null;

            // 1. Recherche de l'utilisateur (Optimisée avec AsSplitQuery)
            var localUser = await _context.Users
                .AsSplitQuery()
                .Include(u => u.UserRoles)
                    .ThenInclude(ur => ur.Role)
                    .ThenInclude(r => r.RolePermissions)
                    .ThenInclude(rp => rp.Permission)
                .FirstOrDefaultAsync(u => u.Username == request.Username);

            // 2. Vérification Compte LOCAL
            if (localUser != null && !localUser.IsAdUser)
            {
                // Vérification du Hash via le Helper centralisé
                if (localUser.PasswordHash == SecurityHelper.ComputeSha256Hash(request.Password))
                {
                    user = localUser;
                }
            }
            // 3. Vérification ACTIVE DIRECTORY (Windows uniquement)
            else if (OperatingSystem.IsWindows())
            {
                if (CheckAdCredentials(request.Username, request.Password))
                {
                    user = await SyncAdUserAsync(request.Username);
                }
            }

            // Si user non trouvé ou inactif -> Echec
            if (user == null || !user.IsActive) return null;

            // 4. Génération du Token
            var permissions = await GetUserPermissionsAsync(user);
            var token = GenerateJwtToken(user, permissions);

            return new LoginResponse
            {
                Token = token,
                Username = user.Username,
                DisplayName = user.DisplayName,
                Permissions = permissions
            };
        }

        [System.Runtime.Versioning.SupportedOSPlatform("windows")]
        private bool CheckAdCredentials(string username, string password)
        {
            try
            {
                var domain = _configuration["Ldap:Domain"];
                ContextType contextType = string.IsNullOrEmpty(domain) ? ContextType.Machine : ContextType.Domain;
                using var context = new PrincipalContext(contextType, domain);
                return context.ValidateCredentials(username, password);
            }
            catch
            {
                return false;
            }
        }

        private async Task<User> SyncAdUserAsync(string username)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Username == username);
            if (user == null)
            {
                user = new User
                {
                    Username = username,
                    DisplayName = username,
                    IsAdUser = true,
                    IsActive = true,
                    LastLogin = DateTime.UtcNow
                };
                _context.Users.Add(user);
                await _context.SaveChangesAsync();
            }

            if (OperatingSystem.IsWindows())
            {
                await ApplyAdGroupMappings(user);
            }
            return user;
        }

        [System.Runtime.Versioning.SupportedOSPlatform("windows")]
        private async Task ApplyAdGroupMappings(User user)
        {
            try
            {
                var domain = _configuration["Ldap:Domain"];
                ContextType contextType = string.IsNullOrEmpty(domain) ? ContextType.Machine : ContextType.Domain;
                using var context = new PrincipalContext(contextType, domain);
                var userPrincipal = UserPrincipal.FindByIdentity(context, user.Username);

                if (userPrincipal != null)
                {
                    var adGroups = userPrincipal.GetAuthorizationGroups().Select(g => g.Name).ToList();
                    var mappings = await _context.AdRoleMappings
                        .Where(m => adGroups.Contains(m.AdGroupName))
                        .ToListAsync();

                    var currentRoles = await _context.UserRoles.Where(ur => ur.UserId == user.Id).ToListAsync();
                    _context.UserRoles.RemoveRange(currentRoles);

                    foreach (var map in mappings)
                    {
                        _context.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = map.RoleId });
                    }
                    await _context.SaveChangesAsync();
                }
            }
            catch { /* Ignorer les erreurs de mapping silencieusement */ }
        }

        private async Task<List<string>> GetUserPermissionsAsync(User user)
        {
            return await _context.UserRoles
                .Where(ur => ur.UserId == user.Id)
                .SelectMany(ur => ur.Role.RolePermissions)
                .Select(rp => rp.Permission.Code)
                .Distinct()
                .ToListAsync();
        }

        private string GenerateJwtToken(User user, List<string> permissions)
        {
            var key = Encoding.ASCII.GetBytes(_configuration["Jwt:Key"]!);
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.Name, user.Username),
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim("DisplayName", user.DisplayName ?? "")
            };
            foreach (var perm in permissions) claims.Add(new Claim("Permission", perm));

            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(claims),
                Expires = DateTime.UtcNow.AddMinutes(double.Parse(_configuration["Jwt:ExpireMinutes"]!)),
                SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature),
                Issuer = _configuration["Jwt:Issuer"],
                Audience = _configuration["Jwt:Audience"]
            };
            var tokenHandler = new JwtSecurityTokenHandler();
            return tokenHandler.WriteToken(tokenHandler.CreateToken(tokenDescriptor));
        }
    }
}