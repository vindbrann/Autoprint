using Autoprint.Server.Services;
using Autoprint.Server.Helpers;
using System.Text;
using System.Text.Json;
using Autoprint.Server.Data;
using Autoprint.Server.Models.Security;
using Autoprint.Shared;
using Autoprint.Shared.DTOs;
using Autoprint.Shared.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Autoprint.Server.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(Policy = "ADMIN_ACCESS")]
    public class BackupController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly SecretProtector _secrets;
        private readonly AllowedHostsService _allowedHosts;

        public BackupController(ApplicationDbContext context, SecretProtector secrets, AllowedHostsService allowedHosts)
        {
            _context = context;
            _secrets = secrets;
            _allowedHosts = allowedHosts;
        }

        // POST : le mot de passe de protection ne doit jamais apparaître dans une URL
        [HttpPost("export")]
        public async Task<IActionResult> Export([FromBody] BackupExportRequestDto? request)
        {
            var secretsPassword = request?.SecretsPassword;
            if (!string.IsNullOrEmpty(secretsPassword) && secretsPassword.Length < PasswordPolicy.MinLength)
                return BadRequest($"Le mot de passe de protection doit faire au moins {PasswordPolicy.MinLength} caractères.");

            var backup = new BackupRootDto
            {
                CreatedAt = DateTime.UtcNow,
                CreatedBy = User.Identity?.Name ?? "System",

                Marques = await _context.Marques.Select(x => new BackupMarqueDto { Id = x.Id, Nom = x.Nom }).ToListAsync(),

                Lieux = await _context.Emplacements
                    .Include(e => e.Networks)
                    .Select(x => new BackupLieuDto
                    {
                        Id = x.Id,
                        Nom = x.Nom,
                        Code = x.Code,
                        Status = x.Status,
                        Networks = x.Networks.Select(n => new BackupNetworkDto
                        {
                            Cidr = n.CidrIpv4,
                            Description = n.Description,
                            IsPrimary = n.IsPrimary
                        }).ToList()
                    }).ToListAsync(),

                Pilotes = await _context.Pilotes.Select(x => new BackupPiloteDto { Id = x.Id, Nom = x.Nom, Version = x.Version, EstInstalle = x.EstInstalle }).ToListAsync(),
                
                SnmpProfiles = await _context.SnmpProfiles
                    .Include(p => p.Items)
                    .Select(p => new BackupSnmpProfileDto
                    {
                        Id = p.Id,
                        Name = p.Name,
                        IsColor = p.IsColor,
                        OidTonerBlack = p.OidTonerBlack,
                        OidTonerCyan = p.OidTonerCyan,
                        OidTonerMagenta = p.OidTonerMagenta,
                        OidTonerYellow = p.OidTonerYellow,
                        OidPageCounter = p.OidPageCounter,
                        Items = p.Items.Select(i => new BackupSnmpProfileItemDto
                        {
                            Id = i.Id,
                            Category = i.Category,
                            Name = i.Name,
                            Oid = i.Oid,
                            OidMaxCapacity = i.OidMaxCapacity,
                            ValueType = i.ValueType,
                            ColorHex = i.ColorHex,
                            SortOrder = i.SortOrder
                        }).ToList()
                    }).ToListAsync(),

                Modeles = await _context.Modeles.Select(x => new BackupModeleDto
                {
                    Id = x.Id,
                    Nom = x.Nom,
                    MarqueId = x.MarqueId,
                    PiloteId = x.PiloteId,
                    SnmpProfileId = x.SnmpProfileId,
                    SnmpCredentialId = x.SnmpCredentialId
                }).ToListAsync(),

                Imprimantes = await _context.Imprimantes.Select(x => new BackupImprimanteDto
                {
                    Id = x.Id,
                    NomAffiche = x.NomAffiche,
                    AdresseIp = x.AdresseIp,
                    NomPartage = x.NomPartage,
                    EstPartagee = x.EstPartagee,
                    ModeleId = x.ModeleId,
                    EmplacementId = x.EmplacementId,
                    Status = x.Status,
                    Localisation = x.Localisation,
                    SerialNumber = x.SerialNumber,
                    IsDirectPrintingEnabled = x.IsDirectPrintingEnabled,
                    SnmpPort = x.SnmpPort,
                    SnmpVersion = x.SnmpVersion,
                    SnmpCommunity = x.SnmpCommunity,
                    SnmpCredentialId = x.SnmpCredentialId
                }).ToListAsync(),

                ReportSchedules = await _context.ReportSchedules
                    .Where(r => !r.EstSupprime)
                    .Select(r => new BackupReportScheduleDto
                    {
                        Id = r.Id,
                        ReportName = r.ReportName,
                        Frequency = r.Frequency,
                        SelectedMetricsJson = r.SelectedMetricsJson,
                        ScopeFilterJson = r.ScopeFilterJson,
                        EmailRecipients = r.EmailRecipients,
                        Format = r.Format,
                        IsActive = r.IsActive,
                        PredictionThresholdDays = r.PredictionThresholdDays,
                        RunHour = r.RunHour,
                        RunMinute = r.RunMinute,
                        RunDayOfWeek = r.RunDayOfWeek,
                        RunDayOfMonth = r.RunDayOfMonth
                    }).ToListAsync(),

                IntegrationTokens = await _context.IntegrationTokens.Select(t => new BackupIntegrationTokenDto
                {
                    Id = t.Id,
                    TokenHash = t.TokenHash,
                    Description = t.Description,
                    CreatedAt = t.CreatedAt,
                    ExpiresAt = t.ExpiresAt
                }).ToListAsync(),

                Roles = await _context.Roles.Include(r => r.RolePermissions).Select(x => new BackupRoleDto
                {
                    Id = x.Id,
                    Name = x.Name,
                    Description = x.Description,
                    PermissionIds = x.RolePermissions.Select(rp => rp.PermissionId).ToList()
                }).ToListAsync(),

                Users = await _context.Users.Include(u => u.UserRoles).Select(x => new BackupUserDto
                {
                    Id = x.Id,
                    Username = x.Username,
                    DisplayName = x.DisplayName,
                    Email = x.Email,
                    IsAdUser = x.IsAdUser,
                    IsActive = x.IsActive,
                    RoleIds = x.UserRoles.Select(ur => ur.RoleId).ToList(),
                    AdRoleIds = x.UserRoles.Where(ur => ur.IsFromAd).Select(ur => ur.RoleId).ToList(),
                    PasswordHash = x.PasswordHash,
                    LastPasswordChangeDate = x.LastPasswordChangeDate,
                    ForceChangePassword = x.ForceChangePassword,
                    LastLogin = x.LastLogin == DateTime.MinValue ? null : x.LastLogin
                }).ToListAsync(),

                Settings = (await _context.ServerSettings.AsNoTracking().ToListAsync()).Select(x => new BackupSettingDto
                {
                    Key = x.Key,
                    Type = x.Type,
                    // Secrets : rechiffrés avec le mot de passe de la sauvegarde, ou exclus (audit #9)
                    Value = !SecretProtector.IsSecret(x.Key) ? x.Value
                          : string.IsNullOrEmpty(secretsPassword) || string.IsNullOrEmpty(x.Value) ? ""
                          : SecretProtector.ProtectForBackup(_secrets.Unprotect(x.Value), secretsPassword)
                }).ToList(),
                SecretsProtected = !string.IsNullOrEmpty(secretsPassword),

                AdMappings = await _context.AdRoleMappings.Select(x => new BackupAdMappingDto { Identifier = x.AdIdentifier, Type = (int)x.MappingType, RoleId = x.RoleId }).ToListAsync()
            };

            // Identifiants SNMP et communautés : mêmes règles que les autres secrets (chiffrés avec le mot de passe, sinon exclus)
            string? ForBackup(string? plain) => string.IsNullOrEmpty(secretsPassword) || string.IsNullOrEmpty(plain) ? null : SecretProtector.ProtectForBackup(plain, secretsPassword);
            backup.SnmpCredentials = (await _context.SnmpCredentials.AsNoTracking().ToListAsync()).Select(c => new BackupSnmpCredentialDto
            {
                Id = c.Id,
                Name = c.Name,
                Description = c.Description,
                Version = c.Version,
                Community = ForBackup(_secrets.Unprotect(c.Community)),
                V3Username = c.V3Username,
                V3SecurityLevel = c.V3SecurityLevel,
                V3AuthProtocol = c.V3AuthProtocol,
                V3AuthPassword = ForBackup(_secrets.Unprotect(c.V3AuthPassword)),
                V3PrivProtocol = c.V3PrivProtocol,
                V3PrivPassword = ForBackup(_secrets.Unprotect(c.V3PrivPassword))
            }).ToList();
            foreach (var printer in backup.Imprimantes) printer.SnmpCommunity = ForBackup(_secrets.Unprotect(printer.SnmpCommunity)); // chiffrée en base (B32)

            var json = JsonSerializer.Serialize(backup, new JsonSerializerOptions { WriteIndented = true });
            var bytes = Encoding.UTF8.GetBytes(json);
            return File(bytes, "application/json", $"autoprint_backup_{DateTime.Now:yyyyMMdd_HHmm}.json");
        }

        [HttpPost("restore")]
        public async Task<IActionResult> Restore([FromBody] BackupRootDto backup)
        {
            // Secrets : déchiffrés AVANT toute suppression, pour qu'un mauvais mot de passe ne laisse pas la base à moitié vidée
            var currentSecrets = await _context.ServerSettings.AsNoTracking()
                .Where(s => SecretProtector.SecretKeys.Contains(s.Key))
                .ToDictionaryAsync(s => s.Key, s => s.Value, StringComparer.OrdinalIgnoreCase);
            var restoredSecrets = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var s in backup.Settings.Where(s => SecretProtector.IsSecret(s.Key)))
            {
                if (string.IsNullOrEmpty(s.Value))
                {
                    restoredSecrets[s.Key] = currentSecrets.GetValueOrDefault(s.Key) ?? ""; // absent du fichier : on garde la valeur du serveur
                }
                else if (SecretProtector.IsBackupProtected(s.Value))
                {
                    if (string.IsNullOrEmpty(backup.SecretsPassword))
                        return BadRequest("Cette sauvegarde contient des secrets protégés : saisissez son mot de passe de protection.");
                    try
                    {
                        restoredSecrets[s.Key] = _secrets.Protect(SecretProtector.UnprotectFromBackup(s.Value, backup.SecretsPassword));
                    }
                    catch (System.Security.Cryptography.CryptographicException)
                    {
                        return BadRequest("Mot de passe de protection de la sauvegarde incorrect. Aucune donnée n'a été modifiée.");
                    }
                }
                else
                {
                    restoredSecrets[s.Key] = _secrets.Protect(s.Value); // ancienne sauvegarde : secret en clair
                }
            }

            // Identifiants SNMP / communautés : déchiffrés avant suppression ; absents du fichier => valeurs actuelles conservées
            var currentCredentials = await _context.SnmpCredentials.AsNoTracking().ToDictionaryAsync(c => c.Id);
            var currentCommunities = await _context.Imprimantes.AsNoTracking().ToDictionaryAsync(i => i.Id, i => i.SnmpCommunity);
            string? RestoreSecret(string? value, string? current)
            {
                if (string.IsNullOrEmpty(value)) return current;
                if (!SecretProtector.IsBackupProtected(value)) return value; // valeur en clair (champ non secret historique)
                if (string.IsNullOrEmpty(backup.SecretsPassword))
                    throw new System.Security.Cryptography.CryptographicException("Mot de passe de protection manquant.");
                return SecretProtector.UnprotectFromBackup(value, backup.SecretsPassword);
            }
            var restoredCredentials = new List<SnmpCredential>();
            try
            {
                foreach (var c in backup.SnmpCredentials ?? new())
                {
                    var current = currentCredentials.GetValueOrDefault(c.Id);
                    string? Plain(string? v, string? cur) { var r = RestoreSecret(v, cur == null ? null : _secrets.Unprotect(cur)); return string.IsNullOrEmpty(r) ? null : _secrets.Protect(r); }
                    restoredCredentials.Add(new SnmpCredential
                    {
                        Id = c.Id, Name = c.Name, Description = c.Description, Version = c.Version,
                        Community = Plain(c.Community, current?.Community),
                        V3Username = c.V3Username, V3SecurityLevel = c.V3SecurityLevel, V3AuthProtocol = c.V3AuthProtocol,
                        V3AuthPassword = Plain(c.V3AuthPassword, current?.V3AuthPassword),
                        V3PrivProtocol = c.V3PrivProtocol,
                        V3PrivPassword = Plain(c.V3PrivPassword, current?.V3PrivPassword)
                    });
                }
                // Communautés : valeur du fichier (ou actuelle si absente), rechiffrée avec les clés de ce serveur (B32)
                foreach (var printer in backup.Imprimantes)
                    printer.SnmpCommunity = _secrets.ProtectCommunity(RestoreSecret(printer.SnmpCommunity, _secrets.Unprotect(currentCommunities.GetValueOrDefault(printer.Id))));
            }
            catch (System.Security.Cryptography.CryptographicException)
            {
                return BadRequest(string.IsNullOrEmpty(backup.SecretsPassword)
                    ? "Cette sauvegarde contient des secrets protégés : saisissez son mot de passe de protection."
                    : "Mot de passe de protection de la sauvegarde incorrect. Aucune donnée n'a été modifiée.");
            }

            // Sauvegardes antérieures sans hachage : on conserve le mot de passe actuel des comptes locaux de même nom
            var currentHashes = await _context.Users.AsNoTracking()
                .Where(u => !u.IsAdUser && u.PasswordHash != null)
                .ToDictionaryAsync(u => u.Username, u => u.PasswordHash!, StringComparer.OrdinalIgnoreCase);
            // Sauvegardes antérieures sans date de dernière connexion : on garde celle du compte actuel de même nom (B58)
            var currentLastLogins = await _context.Users.AsNoTracking()
                .ToDictionaryAsync(u => u.Username, u => u.LastLogin, StringComparer.OrdinalIgnoreCase);

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                _context.RolePermissions.RemoveRange(_context.RolePermissions);
                _context.UserRoles.RemoveRange(_context.UserRoles);
                _context.AdRoleMappings.RemoveRange(_context.AdRoleMappings);

                _context.Users.RemoveRange(_context.Users);
                _context.Roles.RemoveRange(_context.Roles);

                _context.Imprimantes.RemoveRange(_context.Imprimantes);
                _context.Modeles.RemoveRange(_context.Modeles);
                _context.Marques.RemoveRange(_context.Marques);
                _context.Pilotes.RemoveRange(_context.Pilotes);

                _context.SnmpProfileItems.RemoveRange(_context.SnmpProfileItems);
                _context.SnmpProfiles.RemoveRange(_context.SnmpProfiles);
                _context.SnmpCredentials.RemoveRange(_context.SnmpCredentials);
                _context.ReportSchedules.RemoveRange(_context.ReportSchedules);
                _context.IntegrationTokens.RemoveRange(_context.IntegrationTokens);

                _context.Emplacements.RemoveRange(_context.Emplacements);

                _context.ServerSettings.RemoveRange(_context.ServerSettings);

                await _context.SaveChangesAsync();

                await EnableIdentityInsert("Marques");
                _context.Marques.AddRange(backup.Marques.Select(x => new Marque { Id = x.Id, Nom = x.Nom }));
                await _context.SaveChangesAsync();
                await DisableIdentityInsert("Marques");

                await EnableIdentityInsert("Pilotes");
                _context.Pilotes.AddRange(backup.Pilotes.Select(x => new Pilote { Id = x.Id, Nom = x.Nom, Version = x.Version, EstInstalle = x.EstInstalle }));
                await _context.SaveChangesAsync();
                await DisableIdentityInsert("Pilotes");

                if (backup.SnmpProfiles != null && backup.SnmpProfiles.Any())
                {
                    await EnableIdentityInsert("SnmpProfiles");
                    foreach (var p in backup.SnmpProfiles)
                    {
                        var profile = new SnmpProfile
                        {
                            Id = p.Id,
                            Name = p.Name,
                            IsColor = p.IsColor,
                            OidTonerBlack = p.OidTonerBlack,
                            OidTonerCyan = p.OidTonerCyan,
                            OidTonerMagenta = p.OidTonerMagenta,
                            OidTonerYellow = p.OidTonerYellow,
                            OidPageCounter = p.OidPageCounter
                        };
                        _context.SnmpProfiles.Add(profile);
                    }
                    await _context.SaveChangesAsync();
                    await DisableIdentityInsert("SnmpProfiles");

                    await EnableIdentityInsert("SnmpProfileItems");
                    foreach (var p in backup.SnmpProfiles)
                    {
                        if (p.Items != null)
                        {
                            foreach (var item in p.Items)
                            {
                                _context.SnmpProfileItems.Add(new SnmpProfileItem
                                {
                                    Id = item.Id,
                                    SnmpProfileId = p.Id,
                                    Category = item.Category,
                                    Name = item.Name,
                                    Oid = item.Oid,
                                    OidMaxCapacity = item.OidMaxCapacity,
                                    ValueType = item.ValueType,
                                    ColorHex = item.ColorHex ?? "#212529",
                                    SortOrder = item.SortOrder
                                });
                            }
                        }
                    }
                    await _context.SaveChangesAsync();
                    await DisableIdentityInsert("SnmpProfileItems");
                }

                await EnableIdentityInsert("Emplacements");
                // Réseau principal et état du lieu (B83) : avant 26.11.11, la sauvegarde ne les contenait pas et tous les réseaux
                // revenaient « secondaires » (le client ne retrouvait plus le lieu par son réseau principal). Ancienne sauvegarde :
                // le premier réseau du lieu redevient le principal.
                var lieuxToRestore = backup.Lieux.Select(x => new Emplacement
                {
                    Id = x.Id,
                    Nom = x.Nom,
                    Code = x.Code,
                    Status = x.Status ?? LieuStatus.Active,
                    Networks = x.Networks.Select((n, i) => new EmplacementNetwork
                    {
                        CidrIpv4 = n.Cidr,
                        Description = n.Description,
                        IsPrimary = n.IsPrimary ?? (i == 0 && x.Networks.All(o => o.IsPrimary == null))
                    }).ToList()
                });
                _context.Emplacements.AddRange(lieuxToRestore);
                await _context.SaveChangesAsync();
                await DisableIdentityInsert("Emplacements");

                if (restoredCredentials.Any())
                {
                    await EnableIdentityInsert("SnmpCredentials");
                    _context.SnmpCredentials.AddRange(restoredCredentials);
                    await _context.SaveChangesAsync();
                    await DisableIdentityInsert("SnmpCredentials");
                }
                var credentialIds = restoredCredentials.Select(c => c.Id).ToHashSet();

                await EnableIdentityInsert("Modeles");
                _context.Modeles.AddRange(backup.Modeles.Select(x => new Modele
                {
                    Id = x.Id,
                    Nom = x.Nom,
                    MarqueId = x.MarqueId,
                    PiloteId = x.PiloteId,
                    SnmpProfileId = x.SnmpProfileId,
                    SnmpCredentialId = x.SnmpCredentialId != null && credentialIds.Contains(x.SnmpCredentialId.Value) ? x.SnmpCredentialId : null
                }));
                await _context.SaveChangesAsync();
                await DisableIdentityInsert("Modeles");

                await EnableIdentityInsert("Imprimantes");
                _context.Imprimantes.AddRange(backup.Imprimantes.Select(x => new Imprimante
                {
                    Id = x.Id,
                    NomAffiche = x.NomAffiche,
                    AdresseIp = x.AdresseIp,
                    NomPartage = x.NomPartage,
                    EstPartagee = x.EstPartagee,
                    ModeleId = x.ModeleId,
                    EmplacementId = x.EmplacementId,
                    Status = x.Status,
                    Localisation = x.Localisation,
                    SerialNumber = x.SerialNumber,
                    IsDirectPrintingEnabled = x.IsDirectPrintingEnabled,
                    SnmpPort = x.SnmpPort > 0 ? x.SnmpPort : 161,
                    SnmpVersion = x.SnmpVersion is 1 or 2 ? x.SnmpVersion : 2,
                    SnmpCommunity = string.IsNullOrEmpty(x.SnmpCommunity) ? _secrets.ProtectCommunity(null) : x.SnmpCommunity, // déjà chiffrée ci-dessus
                    SnmpCredentialId = x.SnmpCredentialId != null && credentialIds.Contains(x.SnmpCredentialId.Value) ? x.SnmpCredentialId : null
                }));
                await _context.SaveChangesAsync();
                await DisableIdentityInsert("Imprimantes");

                if (backup.ReportSchedules != null && backup.ReportSchedules.Any())
                {
                    await EnableIdentityInsert("ReportSchedules");
                    _context.ReportSchedules.AddRange(backup.ReportSchedules.Select(r => new ReportSchedule
                    {
                        Id = r.Id,
                        ReportName = r.ReportName,
                        Frequency = r.Frequency,
                        SelectedMetricsJson = r.SelectedMetricsJson,
                        ScopeFilterJson = r.ScopeFilterJson,
                        EmailRecipients = r.EmailRecipients,
                        Format = r.Format,
                        IsActive = r.IsActive,
                        PredictionThresholdDays = r.PredictionThresholdDays,
                        RunHour = r.RunHour,
                        RunMinute = r.RunMinute,
                        RunDayOfWeek = r.RunDayOfWeek,
                        RunDayOfMonth = r.RunDayOfMonth,
                        EstSupprime = false,
                        DateModification = DateTime.UtcNow
                    }));
                    await _context.SaveChangesAsync();
                    await DisableIdentityInsert("ReportSchedules");
                }

                if (backup.IntegrationTokens != null && backup.IntegrationTokens.Any())
                {
                    await EnableIdentityInsert("IntegrationTokens");
                    _context.IntegrationTokens.AddRange(backup.IntegrationTokens.Select(t => new IntegrationToken
                    {
                        Id = t.Id,
                        TokenHash = t.TokenHash,
                        Description = t.Description,
                        CreatedAt = t.CreatedAt,
                        ExpiresAt = t.ExpiresAt
                    }));
                    await _context.SaveChangesAsync();
                    await DisableIdentityInsert("IntegrationTokens");
                }

                await EnableIdentityInsert("Roles");
                foreach (var r in backup.Roles) _context.Roles.Add(new Role { Id = r.Id, Name = r.Name, Description = r.Description });
                await _context.SaveChangesAsync();
                await DisableIdentityInsert("Roles");

                foreach (var r in backup.Roles)
                {
                    foreach (var pid in r.PermissionIds) _context.RolePermissions.Add(new RolePermission { RoleId = r.Id, PermissionId = pid });
                }

                await EnableIdentityInsert("Users");
                foreach (var u in backup.Users)
                {
                    var restoredUser = new User
                    {
                        Id = u.Id, Username = u.Username, DisplayName = u.DisplayName, Email = u.Email, IsAdUser = u.IsAdUser, IsActive = u.IsActive,
                        PasswordHash = !string.IsNullOrEmpty(u.PasswordHash) ? u.PasswordHash : currentHashes.GetValueOrDefault(u.Username),
                        LastPasswordChangeDate = u.LastPasswordChangeDate,
                        ForceChangePassword = u.ForceChangePassword,
                        LastLogin = u.LastLogin ?? currentLastLogins.GetValueOrDefault(u.Username, DateTime.MinValue)
                    };
                    // Les sessions ouvertes avant la restauration ne sont plus valables
                    restoredUser.RenewSecurityStamp();
                    _context.Users.Add(restoredUser);
                }
                await _context.SaveChangesAsync();
                await DisableIdentityInsert("Users");

                foreach (var u in backup.Users)
                {
                    foreach (var rid in u.RoleIds) _context.UserRoles.Add(new UserRole { UserId = u.Id, RoleId = rid, IsFromAd = u.AdRoleIds.Contains(rid) });
                }

                // Noms d'hôte acceptés : propres à ce serveur, jamais repris d'une sauvegarde (B82 : une sauvegarde d'un autre
                // serveur ou d'avant un changement de nom bloquerait l'accès à la console et aux postes)
                _context.ServerSettings.Add(new ServerSetting { Key = AllowedHostsService.SettingKey, Value = string.Join(";", _allowedHosts.Hosts), Type = "STRING", Description = "Noms d'hôte acceptés par le serveur (séparés par ;)" });

                foreach (var s in backup.Settings.Where(s => !string.Equals(s.Key, AllowedHostsService.SettingKey, StringComparison.OrdinalIgnoreCase)))
                {
                    var value = SecretProtector.IsSecret(s.Key) ? restoredSecrets.GetValueOrDefault(s.Key, "") : s.Value;
                    _context.ServerSettings.Add(new ServerSetting { Key = s.Key, Value = value, Type = s.Type });
                }

                // Secret absent du fichier : on garde celui du serveur (sinon la clé agent serait régénérée et tous les postes rejetés)
                foreach (var kv in currentSecrets.Where(kv => !backup.Settings.Any(s => string.Equals(s.Key, kv.Key, StringComparison.OrdinalIgnoreCase))))
                {
                    _context.ServerSettings.Add(new ServerSetting { Key = kv.Key, Value = kv.Value, Type = "PASSWORD" });
                }

                _context.AdRoleMappings.AddRange(backup.AdMappings.Select(x => new AdRoleMapping
                {
                    AdIdentifier = x.Identifier,
                    MappingType = (AdMappingType)x.Type,
                    RoleId = x.RoleId
                }));

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                _context.AuditLogs.Add(new AuditLog { Action = "SYSTEM_RESTORE", Details = $"Restauration complète.", Utilisateur = User.Identity?.Name ?? "System", Niveau = "WARNING", DateAction = DateTime.UtcNow });
                await _context.SaveChangesAsync();

                return Ok(new { Message = "Restauration terminée avec succès." });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return BadRequest(ApiErrors.LogAndDescribe(HttpContext, ex, "Erreur pendant la restauration, aucune donnée n'a été modifiée."));
            }
        }

        // Un nom de table ne peut pas être paramétré en SQL : il est donc vérifié par liste blanche
        private static readonly HashSet<string> IdentityTables = new(StringComparer.Ordinal)
        {
            "Marques", "Pilotes", "SnmpProfiles", "SnmpProfileItems", "SnmpCredentials", "Emplacements", "EmplacementNetworks",
            "Modeles", "Imprimantes", "ReportSchedules", "IntegrationTokens", "Roles", "Users", "AdRoleMappings"
        };

        private Task EnableIdentityInsert(string table) => SetIdentityInsert(table, "ON");
        private Task DisableIdentityInsert(string table) => SetIdentityInsert(table, "OFF");

        private async Task SetIdentityInsert(string table, string state)
        {
            if (!_context.Database.IsSqlServer()) return;
            if (!IdentityTables.Contains(table)) throw new ArgumentException($"Table non autorisée : {table}");
#pragma warning disable EF1002, EF1003 // nom de table issu de la liste blanche ci-dessus
            await _context.Database.ExecuteSqlRawAsync("SET IDENTITY_INSERT [" + table + "] " + state);
#pragma warning restore EF1002, EF1003
        }
    }
}