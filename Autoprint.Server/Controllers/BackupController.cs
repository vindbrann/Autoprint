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

        public BackupController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpGet("export")]
        public async Task<IActionResult> Export()
        {
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
                        Networks = x.Networks.Select(n => new BackupNetworkDto
                        {
                            Cidr = n.CidrIpv4,
                            Description = n.Description
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
                    SnmpProfileId = x.SnmpProfileId
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
                    SerialNumber = x.SerialNumber
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
                    RoleIds = x.UserRoles.Select(ur => ur.RoleId).ToList()
                }).ToListAsync(),

                Settings = await _context.ServerSettings.Select(x => new BackupSettingDto { Key = x.Key, Value = x.Value, Type = x.Type }).ToListAsync(),

                AdMappings = await _context.AdRoleMappings.Select(x => new BackupAdMappingDto { Identifier = x.AdIdentifier, Type = (int)x.MappingType, RoleId = x.RoleId }).ToListAsync()
            };

            var json = JsonSerializer.Serialize(backup, new JsonSerializerOptions { WriteIndented = true });
            var bytes = Encoding.UTF8.GetBytes(json);
            return File(bytes, "application/json", $"autoprint_backup_{DateTime.Now:yyyyMMdd_HHmm}.json");
        }

        [HttpPost("restore")]
        public async Task<IActionResult> Restore([FromBody] BackupRootDto backup)
        {
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
                                    ColorHex = item.ColorHex,
                                    SortOrder = item.SortOrder
                                });
                            }
                        }
                    }
                    await _context.SaveChangesAsync();
                    await DisableIdentityInsert("SnmpProfileItems");
                }

                await EnableIdentityInsert("Emplacements");
                var lieuxToRestore = backup.Lieux.Select(x => new Emplacement
                {
                    Id = x.Id,
                    Nom = x.Nom,
                    Code = x.Code,
                    Networks = x.Networks.Select(n => new EmplacementNetwork
                    {
                        CidrIpv4 = n.Cidr,
                        Description = n.Description
                    }).ToList()
                });
                _context.Emplacements.AddRange(lieuxToRestore);
                await _context.SaveChangesAsync();
                await DisableIdentityInsert("Emplacements");

                await EnableIdentityInsert("Modeles");
                _context.Modeles.AddRange(backup.Modeles.Select(x => new Modele
                {
                    Id = x.Id,
                    Nom = x.Nom,
                    MarqueId = x.MarqueId,
                    PiloteId = x.PiloteId,
                    SnmpProfileId = x.SnmpProfileId
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
                    SerialNumber = x.SerialNumber
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
                foreach (var u in backup.Users) _context.Users.Add(new User { Id = u.Id, Username = u.Username, DisplayName = u.DisplayName, Email = u.Email, IsAdUser = u.IsAdUser, IsActive = u.IsActive });
                await _context.SaveChangesAsync();
                await DisableIdentityInsert("Users");

                foreach (var u in backup.Users)
                {
                    foreach (var rid in u.RoleIds) _context.UserRoles.Add(new UserRole { UserId = u.Id, RoleId = rid });
                }

                foreach (var s in backup.Settings)
                {
                    _context.ServerSettings.Add(new ServerSetting { Key = s.Key, Value = s.Value, Type = s.Type });
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
                return BadRequest($"Erreur Restauration : {ex.Message}");
            }
        }

        private async Task EnableIdentityInsert(string table)
        {
            if (_context.Database.IsSqlServer())
            {
                await _context.Database.ExecuteSqlRawAsync($"SET IDENTITY_INSERT {table} ON");
            }
        }

        private async Task DisableIdentityInsert(string table)
        {
            if (_context.Database.IsSqlServer())
            {
                await _context.Database.ExecuteSqlRawAsync($"SET IDENTITY_INSERT {table} OFF");
            }
        }
    }
}