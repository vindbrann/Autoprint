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
                Lieux = await _context.Emplacements.Select(x => new BackupLieuDto { Id = x.Id, Nom = x.Nom, Code = x.Code, Cidr = x.CidrIpv4 }).ToListAsync(),
                Pilotes = await _context.Pilotes.Select(x => new BackupPiloteDto { Id = x.Id, Nom = x.Nom, Version = x.Version, EstInstalle = x.EstInstalle }).ToListAsync(),
                Modeles = await _context.Modeles.Select(x => new BackupModeleDto { Id = x.Id, Nom = x.Nom, MarqueId = x.MarqueId, PiloteId = x.PiloteId }).ToListAsync(),

                Imprimantes = await _context.Imprimantes.Select(x => new BackupImprimanteDto
                {
                    Id = x.Id,
                    NomAffiche = x.NomAffiche,
                    AdresseIp = x.AdresseIp,
                    NomPartage = x.NomPartage,
                    EstPartagee = x.EstPartagee,
                    ModeleId = x.ModeleId,
                    EmplacementId = x.EmplacementId,
                    Status = x.Status
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

                await EnableIdentityInsert("Emplacements");
                _context.Emplacements.AddRange(backup.Lieux.Select(x => new Emplacement { Id = x.Id, Nom = x.Nom, Code = x.Code, CidrIpv4 = x.Cidr }));
                await _context.SaveChangesAsync();
                await DisableIdentityInsert("Emplacements");

                await EnableIdentityInsert("Modeles");
                _context.Modeles.AddRange(backup.Modeles.Select(x => new Modele { Id = x.Id, Nom = x.Nom, MarqueId = x.MarqueId, PiloteId = x.PiloteId }));
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
                    Status = x.Status
                }));
                await _context.SaveChangesAsync();
                await DisableIdentityInsert("Imprimantes");

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

                _context.AuditLogs.Add(new AuditLog { Action = "SYSTEM_RESTORE", Details = $"Restauration complète (v{backup.Version}).", Utilisateur = User.Identity?.Name ?? "System", Niveau = "WARNING", DateAction = DateTime.UtcNow });
                await _context.SaveChangesAsync();

                return Ok(new { Message = "Restauration terminée avec succès." });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return BadRequest($"Erreur Restauration : {ex.Message}");
            }
        }

        private async Task EnableIdentityInsert(string table) => await _context.Database.ExecuteSqlRawAsync($"SET IDENTITY_INSERT {table} ON");
        private async Task DisableIdentityInsert(string table) => await _context.Database.ExecuteSqlRawAsync($"SET IDENTITY_INSERT {table} OFF");
    }
}