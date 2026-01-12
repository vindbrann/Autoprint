using System.DirectoryServices.AccountManagement;
using System.DirectoryServices;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Autoprint.Server.Data;
using Autoprint.Server.Helpers;
using Autoprint.Server.Models.Security;
using Autoprint.Shared.DTOs;
using Autoprint.Shared.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

namespace Autoprint.Server.Services
{
    public interface IAuthService
    {
        Task<LoginResponse?> LoginAsync(LoginRequest request);
        Task<List<AdSearchResultDto>> SearchAdAsync(string query, AdMappingType type);
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

            var localUser = await _context.Users
                .AsSplitQuery()
                .Include(u => u.UserRoles).ThenInclude(ur => ur.Role).ThenInclude(r => r.RolePermissions).ThenInclude(rp => rp.Permission)
                .FirstOrDefaultAsync(u => u.Username == request.Username);

            if (localUser != null && !localUser.IsAdUser)
            {
                if (localUser.PasswordHash == SecurityHelper.ComputeSha256Hash(request.Password)) user = localUser;
            }
            else if (OperatingSystem.IsWindows())
            {
                var settings = await _context.ServerSettings
                    .Where(s => s.Key == "AdDomain" || s.Key == "AdServiceUser" || s.Key == "AdServicePassword")
                    .ToListAsync();

                string domain = settings.FirstOrDefault(s => s.Key == "AdDomain")?.Value ?? _configuration["Ldap:Domain"] ?? "";
                string serviceUser = settings.FirstOrDefault(s => s.Key == "AdServiceUser")?.Value ?? "";
                string servicePass = settings.FirstOrDefault(s => s.Key == "AdServicePassword")?.Value ?? "";

                if (!string.IsNullOrWhiteSpace(domain) && !string.IsNullOrWhiteSpace(serviceUser) && !string.IsNullOrWhiteSpace(servicePass))
                {
                    bool isAuthenticated = false;
                    string effectiveLogin = request.Username;

                    try
                    {
                        if (request.Username.Contains("@"))
                        {
                            string ldapPath = $"LDAP://{domain}";
                            using var searchEntry = new System.DirectoryServices.DirectoryEntry(ldapPath, serviceUser, servicePass, AuthenticationTypes.Secure);
                            using var searcher = new System.DirectoryServices.DirectorySearcher(searchEntry);

                            searcher.Filter = $"(&(objectClass=user)(|(mail={request.Username})(userPrincipalName={request.Username})))";
                            searcher.PropertiesToLoad.Add("sAMAccountName");

                            var result = searcher.FindOne();
                            if (result != null && result.Properties["sAMAccountName"].Count > 0)
                            {
                                effectiveLogin = result.Properties["sAMAccountName"][0].ToString()!;
                                Console.WriteLine($"[AUTH] Email '{request.Username}' résolu en Login '{effectiveLogin}'");
                            }
                        }

                        string effectiveServiceUser = serviceUser;
                        if (!serviceUser.Contains("\\") && !serviceUser.Contains("@"))
                        {
                            effectiveServiceUser = $"{serviceUser}@{domain}";
                        }

                        var options = ContextOptions.Negotiate | ContextOptions.Sealing;
                        using var context = new PrincipalContext(ContextType.Domain, domain, null, options, effectiveServiceUser, servicePass);

                        if (context.ValidateCredentials(effectiveLogin, request.Password, options))
                        {
                            isAuthenticated = true;
                        }
                        else if (!effectiveLogin.Contains("\\") && !effectiveLogin.Contains("@"))
                        {
                            if (context.ValidateCredentials($"{domain}\\{effectiveLogin}", request.Password))
                            {
                                isAuthenticated = true;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[AuthService] Erreur Login AD : {ex.Message}");
                    }

                    if (isAuthenticated)
                    {
                        user = await SyncAdUserAsync(effectiveLogin);
                    }
                }
            }

            if (user == null || !user.IsActive) return null;

            bool passwordExpired = false;
            if (!user.IsAdUser)
            {
                if (user.ForceChangePassword) passwordExpired = true;
                else
                {
                    int maxDays = 90;
                    var setting = await _context.ServerSettings.FirstOrDefaultAsync(s => s.Key == "PasswordExpirationDays");
                    if (setting != null && int.TryParse(setting.Value, out int days)) maxDays = days;
                    if (maxDays > 0)
                    {
                        if (user.LastPasswordChangeDate == null) passwordExpired = true;
                        else if (DateTime.UtcNow > user.LastPasswordChangeDate.Value.AddDays(maxDays)) passwordExpired = true;
                    }
                }
            }

            var permissions = await GetUserPermissionsAsync(user);

            if (!permissions.Any() && !user.UserRoles.Any())
            {
                Console.WriteLine($"[AUTH] {user.Username} : Auth OK mais aucun rôle.");
                throw new UnauthorizedAccessException("NO_ACCESS");
            }

            user.LastLogin = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            var token = GenerateJwtToken(user, permissions, passwordExpired, request.RememberMe);

            return new LoginResponse
            {
                Token = token,
                Username = user.Username,
                DisplayName = user.DisplayName,
                Permissions = permissions,
                PasswordExpired = passwordExpired
            };
        }

        [System.Runtime.Versioning.SupportedOSPlatform("windows")]
        public async Task<List<AdSearchResultDto>> SearchAdAsync(string query, AdMappingType type)
        {
            if (!OperatingSystem.IsWindows()) return new List<AdSearchResultDto>();

            var settings = await _context.ServerSettings.ToListAsync();

            string domain = settings.FirstOrDefault(s => s.Key == "AdDomain")?.Value ?? "";
            string baseDn = settings.FirstOrDefault(s => s.Key == "AdBaseDn")?.Value ?? "";
            string customFilter = settings.FirstOrDefault(s => s.Key == "AdLdapFilter")?.Value ?? "";
            string serviceUser = settings.FirstOrDefault(s => s.Key == "AdServiceUser")?.Value ?? "";
            string servicePass = settings.FirstOrDefault(s => s.Key == "AdServicePassword")?.Value ?? "";

            if (string.IsNullOrWhiteSpace(domain) || string.IsNullOrWhiteSpace(serviceUser) || string.IsNullOrWhiteSpace(servicePass))
            {
                Console.WriteLine("[AuthService] Configuration AD incomplète (Compte de service manquant).");
                return new List<AdSearchResultDto>();
            }

            return await Task.Run(() =>
            {
                var results = new List<AdSearchResultDto>();
                try
                {
                    string ldapPath = string.IsNullOrEmpty(baseDn) ? $"LDAP://{domain}" : $"LDAP://{domain}/{baseDn}";

                    using var entry = new System.DirectoryServices.DirectoryEntry(
                        ldapPath,
                        serviceUser,
                        servicePass,
                        System.DirectoryServices.AuthenticationTypes.Secure
                    );

                    using var searcher = new System.DirectoryServices.DirectorySearcher(entry);

                    string classFilter = (type == AdMappingType.Group) ? "(objectClass=group)" : "(objectClass=user)";
                    string queryFilter = $"(|(sAMAccountName=*{query}*)(name=*{query}*))";
                    string globalFilter = string.IsNullOrWhiteSpace(customFilter) ? "" : customFilter;

                    searcher.Filter = $"(&{classFilter}{queryFilter}{globalFilter})";
                    searcher.SizeLimit = 20;

                    foreach (System.DirectoryServices.SearchResult res in searcher.FindAll())
                    {
                        string name = res.Properties["name"].Count > 0 ? res.Properties["name"][0].ToString()! : "Inconnu";
                        string sam = res.Properties["sAMAccountName"].Count > 0 ? res.Properties["sAMAccountName"][0].ToString()! : "";
                        string desc = res.Properties["description"].Count > 0 ? res.Properties["description"][0].ToString()! : "";

                        if (!string.IsNullOrEmpty(sam))
                        {
                            results.Add(new AdSearchResultDto { Name = name, SamAccountName = sam, Description = desc, Type = type });
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Erreur Recherche AD: {ex.Message}");
                }
                return results;
            });
        }

        private async Task<User> SyncAdUserAsync(string username)
        {
            string cleanUsername = username.Contains("\\") ? username.Split('\\')[1] : username;
            string adDisplayName = cleanUsername;
            string? adEmail = null;

            if (OperatingSystem.IsWindows())
            {
                try
                {
                    var settings = await _context.ServerSettings.ToListAsync();
                    string domain = settings.FirstOrDefault(s => s.Key == "AdDomain")?.Value ?? _configuration["Ldap:Domain"] ?? "";
                    string baseDn = settings.FirstOrDefault(s => s.Key == "AdBaseDn")?.Value ?? "";

                    string serviceUser = settings.FirstOrDefault(s => s.Key == "AdServiceUser")?.Value ?? "";
                    string servicePass = settings.FirstOrDefault(s => s.Key == "AdServicePassword")?.Value ?? "";

                    if (!string.IsNullOrEmpty(domain) && !string.IsNullOrEmpty(serviceUser) && !string.IsNullOrEmpty(servicePass))
                    {
                        string ldapPath = string.IsNullOrEmpty(baseDn) ? $"LDAP://{domain}" : $"LDAP://{domain}/{baseDn}";

                        using DirectoryEntry entry = new DirectoryEntry(
                            ldapPath,
                            serviceUser,
                            servicePass,
                            AuthenticationTypes.Secure
                        );

                        using DirectorySearcher searcher = new DirectorySearcher(entry);
                        searcher.Filter = $"(&(objectClass=user)(sAMAccountName={cleanUsername}))";
                        searcher.PropertiesToLoad.Add("displayName");
                        searcher.PropertiesToLoad.Add("mail");
                        searcher.PropertiesToLoad.Add("givenName");
                        searcher.PropertiesToLoad.Add("sn");

                        SearchResult? result = searcher.FindOne();

                        if (result != null)
                        {
                            string firstName = result.Properties["givenName"].Count > 0 ? result.Properties["givenName"][0].ToString()! : "";
                            string lastName = result.Properties["sn"].Count > 0 ? result.Properties["sn"][0].ToString()! : "";
                            string rawDisplayName = result.Properties["displayName"].Count > 0 ? result.Properties["displayName"][0].ToString()! : "";

                            if (!string.IsNullOrEmpty(firstName) && !string.IsNullOrEmpty(lastName))
                                adDisplayName = $"{firstName} {lastName.ToUpper()}";
                            else if (!string.IsNullOrEmpty(rawDisplayName))
                                adDisplayName = rawDisplayName;

                            if (result.Properties["mail"].Count > 0)
                                adEmail = result.Properties["mail"][0].ToString();
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[SyncService] Erreur Sync AD: {ex.Message}");
                }
            }

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Username == cleanUsername);

            if (user == null)
            {
                user = new User { Username = cleanUsername, DisplayName = adDisplayName, Email = adEmail, IsAdUser = true, IsActive = true, LastLogin = DateTime.UtcNow };
                _context.Users.Add(user);
            }
            else
            {
                user.DisplayName = adDisplayName;
                user.Email = adEmail;
                user.IsAdUser = true;
                user.LastLogin = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync();

            if (OperatingSystem.IsWindows()) await ApplyAdGroupMappings(user);

            return user;
        }

        [System.Runtime.Versioning.SupportedOSPlatform("windows")]
        private async Task ApplyAdGroupMappings(User user)
        {
            try
            {
                var settings = await _context.ServerSettings.ToListAsync();
                string domain = settings.FirstOrDefault(s => s.Key == "AdDomain")?.Value ?? _configuration["Ldap:Domain"] ?? "";
                string baseDn = settings.FirstOrDefault(s => s.Key == "AdBaseDn")?.Value ?? "";

                string serviceUser = settings.FirstOrDefault(s => s.Key == "AdServiceUser")?.Value ?? "";
                string servicePass = settings.FirstOrDefault(s => s.Key == "AdServicePassword")?.Value ?? "";

                if (string.IsNullOrEmpty(domain) || string.IsNullOrEmpty(serviceUser)) return;

                string ldapPath = string.IsNullOrEmpty(baseDn) ? $"LDAP://{domain}" : $"LDAP://{domain}/{baseDn}";

                using DirectoryEntry entry = new DirectoryEntry(
                    ldapPath,
                    serviceUser,
                    servicePass,
                    AuthenticationTypes.Secure
                );

                using DirectorySearcher searcher = new DirectorySearcher(entry);
                searcher.Filter = $"(&(objectClass=user)(sAMAccountName={user.Username}))";
                searcher.PropertiesToLoad.Add("distinguishedName");

                SearchResult? userResult = searcher.FindOne();

                if (userResult != null)
                {
                    string userDn = userResult.Properties["distinguishedName"][0].ToString()!;

                    using DirectorySearcher groupSearcher = new DirectorySearcher(entry);
                    groupSearcher.Filter = $"(&(objectClass=group)(member:1.2.840.113556.1.4.1941:={userDn}))";
                    groupSearcher.PropertiesToLoad.Add("sAMAccountName");
                    groupSearcher.PageSize = 1000;

                    var adGroupNames = new List<string>();

                    foreach (SearchResult groupRes in groupSearcher.FindAll())
                    {
                        if (groupRes.Properties["sAMAccountName"].Count > 0)
                        {
                            adGroupNames.Add(groupRes.Properties["sAMAccountName"][0].ToString()!);
                        }
                    }

                    var allMappings = await _context.AdRoleMappings.ToListAsync();
                    var applicableMappings = new List<AdRoleMapping>();

                    foreach (var mapping in allMappings)
                    {
                        bool match = false;

                        if (mapping.MappingType == AdMappingType.Group)
                        {
                            if (adGroupNames.Any(g => g.Equals(mapping.AdIdentifier, StringComparison.OrdinalIgnoreCase))) match = true;
                        }
                        else if (mapping.MappingType == AdMappingType.User)
                        {
                            if (mapping.AdIdentifier.Equals(user.Username, StringComparison.OrdinalIgnoreCase)) match = true;
                        }

                        if (match) applicableMappings.Add(mapping);
                    }

                    if (applicableMappings.Any())
                    {
                        var currentRoles = await _context.UserRoles.Where(ur => ur.UserId == user.Id).ToListAsync();
                        if (currentRoles.Any()) _context.UserRoles.RemoveRange(currentRoles);

                        foreach (var map in applicableMappings)
                        {
                            if (!_context.UserRoles.Local.Any(ur => ur.UserId == user.Id && ur.RoleId == map.RoleId))
                            {
                                _context.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = map.RoleId });
                            }
                        }
                        await _context.SaveChangesAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[AD-SYNC-ERROR] {ex.Message}");
            }
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

        private string GenerateJwtToken(User user, List<string> permissions, bool passwordExpired, bool rememberMe)
        {
            var key = Encoding.ASCII.GetBytes(_configuration["Jwt:Key"]!);
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.Name, user.Username),
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim("DisplayName", user.DisplayName ?? "")
            };

            if (passwordExpired) claims.Add(new Claim("ForcePasswordChange", "true"));
            foreach (var perm in permissions) claims.Add(new Claim("Permission", perm));

            double expireMinutes = double.Parse(_configuration["Jwt:ExpireMinutes"] ?? "60");

            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(claims),
                Expires = DateTime.UtcNow.AddMinutes(expireMinutes),
                SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature),
                Issuer = _configuration["Jwt:Issuer"],
                Audience = _configuration["Jwt:Audience"]
            };
            var tokenHandler = new JwtSecurityTokenHandler();
            return tokenHandler.WriteToken(tokenHandler.CreateToken(tokenDescriptor));
        }
    }
}