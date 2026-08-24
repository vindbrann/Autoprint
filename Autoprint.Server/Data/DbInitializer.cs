using Autoprint.Server.Helpers;
using Autoprint.Server.Models.Security;
using Autoprint.Shared;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;

namespace Autoprint.Server.Data
{
    public static class DbInitializer
    {
        public static void Initialize(ApplicationDbContext context)
        {
            // Mise à jour de la structure (SQL Server & SQLite) lors des mises à niveau
            if (context.Database.ProviderName != null && context.Database.ProviderName.Contains("SqlServer", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    context.Database.ExecuteSqlRaw(@"
                        IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Imprimantes') AND name = 'SerialNumber')
                        BEGIN
                            ALTER TABLE Imprimantes ADD SerialNumber nvarchar(100) NULL;
                        END
                    ");
                }
                catch { }

                try
                {
                    context.Database.ExecuteSqlRaw(@"
                        IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'SnmpProfiles')
                        BEGIN
                            CREATE TABLE SnmpProfiles (
                                Id int IDENTITY(1,1) NOT NULL PRIMARY KEY,
                                Name nvarchar(100) NOT NULL,
                                OidTonerBlack nvarchar(150) NULL,
                                OidTonerCyan nvarchar(150) NULL,
                                OidTonerMagenta nvarchar(150) NULL,
                                OidTonerYellow nvarchar(150) NULL,
                                OidPageCounter nvarchar(150) NULL,
                                IsColor bit NOT NULL DEFAULT 0,
                                DateModification datetime2 NOT NULL DEFAULT (GETUTCDATE()),
                                ModifiePar nvarchar(100) NULL,
                                EstSupprime bit NOT NULL DEFAULT 0
                            );
                        END

                        IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('SnmpProfiles') AND name = 'IsColor')
                        BEGIN
                            ALTER TABLE SnmpProfiles ADD IsColor bit NOT NULL DEFAULT 0;
                        END

                        IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Modeles') AND name = 'SnmpProfileId')
                        BEGIN
                            ALTER TABLE Modeles ADD SnmpProfileId int NULL;
                        END
                    ");
                }
                catch { }

                try
                {
                    context.Database.ExecuteSqlRaw(@"
                        IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'SnmpProfileItems')
                        BEGIN
                            CREATE TABLE SnmpProfileItems (
                                Id int IDENTITY(1,1) NOT NULL PRIMARY KEY,
                                SnmpProfileId int NOT NULL,
                                Category int NOT NULL,
                                Name nvarchar(100) NOT NULL,
                                Oid nvarchar(150) NOT NULL,
                                OidMaxCapacity nvarchar(150) NULL,
                                ValueType int NOT NULL,
                                ColorHex nvarchar(20) NOT NULL,
                                SortOrder int NOT NULL,
                                DateModification datetime2 NOT NULL DEFAULT (GETUTCDATE()),
                                ModifiePar nvarchar(100) NULL,
                                EstSupprime bit NOT NULL DEFAULT 0,
                                CONSTRAINT FK_SnmpProfileItems_SnmpProfiles_SnmpProfileId FOREIGN KEY (SnmpProfileId) REFERENCES SnmpProfiles(Id) ON DELETE CASCADE
                            );
                            CREATE INDEX IX_SnmpProfileItems_SnmpProfileId ON SnmpProfileItems(SnmpProfileId);
                        END
                    ");
                }
                catch { }
            }
            else if (context.Database.ProviderName != null && context.Database.ProviderName.Contains("Sqlite", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    context.Database.ExecuteSqlRaw("ALTER TABLE ReportSchedules ADD COLUMN IsActive INTEGER NOT NULL DEFAULT 1;");
                }
                catch { }

                try
                {
                    context.Database.ExecuteSqlRaw("ALTER TABLE ReportSchedules ADD COLUMN PredictionThresholdDays INTEGER NOT NULL DEFAULT 14;");
                }
                catch { }

                try
                {
                    context.Database.ExecuteSqlRaw("ALTER TABLE ReportSchedules ADD COLUMN RunHour INTEGER NOT NULL DEFAULT 6;");
                }
                catch { }

                try
                {
                    context.Database.ExecuteSqlRaw("ALTER TABLE ReportSchedules ADD COLUMN RunMinute INTEGER NOT NULL DEFAULT 0;");
                }
                catch { }

                try
                {
                    context.Database.ExecuteSqlRaw("ALTER TABLE ReportSchedules ADD COLUMN RunDayOfWeek INTEGER NULL;");
                }
                catch { }

                try
                {
                    context.Database.ExecuteSqlRaw("ALTER TABLE ReportSchedules ADD COLUMN RunDayOfMonth INTEGER NULL;");
                }
                catch { }

                try
                {
                    context.Database.ExecuteSqlRaw("ALTER TABLE Imprimantes ADD COLUMN SerialNumber TEXT NULL;");
                }
                catch { }

                try
                {
                    context.Database.ExecuteSqlRaw(@"
                        CREATE TABLE IF NOT EXISTS SnmpProfiles (
                            Id INTEGER PRIMARY KEY AUTOINCREMENT,
                            Name TEXT NOT NULL,
                            OidTonerBlack TEXT NULL,
                            OidTonerCyan TEXT NULL,
                            OidTonerMagenta TEXT NULL,
                            OidTonerYellow TEXT NULL,
                            OidPageCounter TEXT NULL,
                            IsColor INTEGER NOT NULL DEFAULT 0,
                            DateModification TEXT NOT NULL DEFAULT (datetime('now')),
                            ModifiePar TEXT NULL,
                            EstSupprime INTEGER NOT NULL DEFAULT 0
                        );
                    ");
                }
                catch { }

                try
                {
                    context.Database.ExecuteSqlRaw("ALTER TABLE SnmpProfiles ADD COLUMN IsColor INTEGER NOT NULL DEFAULT 0;");
                }
                catch { }

                try
                {
                    context.Database.ExecuteSqlRaw("ALTER TABLE Modeles ADD COLUMN SnmpProfileId INTEGER NULL;");
                }
                catch { }

                try
                {
                    context.Database.ExecuteSqlRaw(@"
                        CREATE TABLE IF NOT EXISTS SnmpProfileItems (
                            Id INTEGER PRIMARY KEY AUTOINCREMENT,
                            SnmpProfileId INTEGER NOT NULL,
                            Category INTEGER NOT NULL,
                            Name TEXT NOT NULL,
                            Oid TEXT NOT NULL,
                            OidMaxCapacity TEXT NULL,
                            ValueType INTEGER NOT NULL,
                            ColorHex TEXT NOT NULL,
                            SortOrder INTEGER NOT NULL,
                            DateModification TEXT NOT NULL DEFAULT (datetime('now')),
                            ModifiePar TEXT NULL,
                            EstSupprime INTEGER NOT NULL DEFAULT 0,
                            FOREIGN KEY (SnmpProfileId) REFERENCES SnmpProfiles(Id) ON DELETE CASCADE
                        );
                    ");
                }
                catch { }

                try
                {
                    context.Database.ExecuteSqlRaw("CREATE INDEX IF NOT EXISTS IX_SnmpProfileItems_SnmpProfileId ON SnmpProfileItems(SnmpProfileId);");
                }
                catch { }
            }

            if (!context.ServerSettings.Any(s => s.Key == "Monitoring_IncludeOffline"))
            {
                context.ServerSettings.Add(new ServerSetting { Key = "Monitoring_IncludeOffline", Value = "true", Description = "Inclure les imprimantes hors lignes dans l'export API", Type = "BOOL" });
            }
            if (!context.ServerSettings.Any(s => s.Key == "Monitoring_IncludeWarning"))
            {
                context.ServerSettings.Add(new ServerSetting { Key = "Monitoring_IncludeWarning", Value = "true", Description = "Inclure les alertes / avertissements dans l'export API", Type = "BOOL" });
            }
            if (!context.ServerSettings.Any(s => s.Key == "Monitoring_IncludeCritical"))
            {
                context.ServerSettings.Add(new ServerSetting { Key = "Monitoring_IncludeCritical", Value = "true", Description = "Inclure les pannes critiques dans l'export API", Type = "BOOL" });
            }
            if (!context.ServerSettings.Any(s => s.Key == "Monitoring_IncludeArchived"))
            {
                context.ServerSettings.Add(new ServerSetting { Key = "Monitoring_IncludeArchived", Value = "false", Description = "Inclure les imprimantes archivées dans l'export API", Type = "BOOL" });
            }

            if (!context.ServerSettings.Any(s => s.Key == "AgentApiKey"))
            {
                context.ServerSettings.Add(new ServerSetting { Key = "AgentApiKey", Value = Guid.NewGuid().ToString(), Description = "Clé API Agent", Type = "PASSWORD" });
            }

            if (!context.ServerSettings.Any(s => s.Key == "Monitoring_IntervalMinutes"))
            {
                context.ServerSettings.Add(new ServerSetting { Key = "Monitoring_IntervalMinutes", Value = "60", Description = "Intervalle de scan en minutes", Type = "INT" });
            }
            if (!context.ServerSettings.Any(s => s.Key == "Monitoring_StartHour"))
            {
                context.ServerSettings.Add(new ServerSetting { Key = "Monitoring_StartHour", Value = "8", Description = "Heure de début du scan quotidien (0-23)", Type = "INT" });
            }
            if (!context.ServerSettings.Any(s => s.Key == "Monitoring_EndHour"))
            {
                context.ServerSettings.Add(new ServerSetting { Key = "Monitoring_EndHour", Value = "18", Description = "Heure de fin du scan quotidien (0-23)", Type = "INT" });
            }
            if (!context.ServerSettings.Any(s => s.Key == "Monitoring_ScanOnWeekends"))
            {
                context.ServerSettings.Add(new ServerSetting { Key = "Monitoring_ScanOnWeekends", Value = "true", Description = "Scanner pendant le week-end (true/false)", Type = "BOOL" });
            }
            if (!context.ServerSettings.Any(s => s.Key == "Monitoring_Enabled"))
            {
                context.ServerSettings.Add(new ServerSetting { Key = "Monitoring_Enabled", Value = "true", Description = "Activer la supervision d'arrière-plan (true/false)", Type = "BOOL" });
            }

            if (!context.Users.Any())
            {
                Console.WriteLine("--> Initialisation Admin (admin/admin123)...");
                var adminUser = new User
                {
                    Username = "admin",
                    DisplayName = "Administrateur",
                    IsAdUser = false,
                    IsActive = true,
                    PasswordHash = SecurityHelper.HashPassword("admin123"),
                    ForceChangePassword = true,
                    LastPasswordChangeDate = DateTime.UtcNow
                };
                context.Users.Add(adminUser);
                context.SaveChanges();

                context.UserRoles.Add(new UserRole { UserId = adminUser.Id, RoleId = 1 });
            }
            context.SaveChanges();

            // Programmatically seed granular permissions if they are missing (crucial for SQLite updates)
            var permissionsList = new System.Collections.Generic.List<Permission>
            {
                new Permission { Code = "SNMP_PROFILE_READ", Description = "Voir les profils SNMP" },
                new Permission { Code = "SNMP_PROFILE_WRITE", Description = "Créer/Modifier des profils SNMP" },
                new Permission { Code = "SNMP_PROFILE_DELETE", Description = "Supprimer des profils SNMP" },
                new Permission { Code = "REPORT_MANAGE", Description = "Gérer les rapports et la planification" },
                new Permission { Code = "PRINTER_ARCHIVE", Description = "Archiver/Désarchiver des imprimantes" },
                new Permission { Code = "NETWORK_SCAN", Description = "Lancer des scans réseau et gérer la découverte" }
            };

            foreach (var perm in permissionsList)
            {
                var existingPerm = context.Permissions.FirstOrDefault(p => p.Code == perm.Code);
                if (existingPerm == null)
                {
                    Console.WriteLine($"--> Seeding permission: {perm.Code}");
                    var newPerm = new Permission
                    {
                        Code = perm.Code,
                        Description = perm.Description
                    };
                    context.Permissions.Add(newPerm);
                    context.SaveChanges();

                    if (!context.RolePermissions.Any(rp => rp.RoleId == 1 && rp.PermissionId == newPerm.Id))
                    {
                        context.RolePermissions.Add(new RolePermission { RoleId = 1, PermissionId = newPerm.Id });
                    }
                }
                else
                {
                    if (!context.RolePermissions.Any(rp => rp.RoleId == 1 && rp.PermissionId == existingPerm.Id))
                    {
                        context.RolePermissions.Add(new RolePermission { RoleId = 1, PermissionId = existingPerm.Id });
                    }
                }
            }
            context.SaveChanges();
        }
    }
}