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
        // SQLite n'a pas de « ADD COLUMN IF NOT EXISTS » : on vérifie d'abord, pour ne pas remplir les journaux d'erreurs attendues.
        // Renvoie true si la colonne vient d'être ajoutée.
        private static bool SqliteAddColumn(ApplicationDbContext context, string table, string column, string definition)
        {
            var connection = context.Database.GetDbConnection();
            bool wasClosed = connection.State != System.Data.ConnectionState.Open;
            if (wasClosed) connection.Open();
            try
            {
                using (var cmd = connection.CreateCommand())
                {
                    cmd.CommandText = $"SELECT COUNT(*) FROM pragma_table_info('{table}') WHERE name = '{column}'";
                    if (Convert.ToInt32(cmd.ExecuteScalar()) > 0) return false;
                }
#pragma warning disable EF1002 // noms de table / colonne en dur dans ce fichier, jamais issus d'une saisie
                context.Database.ExecuteSqlRaw($"ALTER TABLE {table} ADD COLUMN {column} {definition};");
#pragma warning restore EF1002
                return true;
            }
            finally
            {
                if (wasClosed) connection.Close();
            }
        }

        // Mises à niveau du schéma (backlog B3) : une colonne / table / index déjà présent est normal (SQLite n'a pas de
        // « IF NOT EXISTS » pour ADD COLUMN) ; toute autre erreur est affichée au lieu d'être ignorée.
        private static void LogSchemaError(Exception ex)
        {
            var message = ex.InnerException?.Message ?? ex.Message;
            if (message.Contains("duplicate column", StringComparison.OrdinalIgnoreCase) ||
                message.Contains("already exists", StringComparison.OrdinalIgnoreCase))
                return;
            Console.WriteLine($"[DbInitializer] Mise à niveau du schéma en échec : {message}");
        }

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
                catch (Exception ex) { LogSchemaError(ex); }

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
                catch (Exception ex) { LogSchemaError(ex); }

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
                catch (Exception ex) { LogSchemaError(ex); }

                // Profils d'identifiants SNMP (SNMPv3, phase B)
                try
                {
                    context.Database.ExecuteSqlRaw(@"
                        IF OBJECT_ID('SnmpCredentials') IS NULL
                        BEGIN
                            CREATE TABLE SnmpCredentials (
                                Id int IDENTITY(1,1) NOT NULL CONSTRAINT PK_SnmpCredentials PRIMARY KEY,
                                Name nvarchar(100) NOT NULL,
                                Description nvarchar(255) NULL,
                                Version int NOT NULL CONSTRAINT DF_SnmpCredentials_Version DEFAULT 2,
                                Community nvarchar(max) NULL,
                                V3Username nvarchar(100) NULL,
                                V3SecurityLevel int NOT NULL CONSTRAINT DF_SnmpCredentials_Level DEFAULT 2,
                                V3AuthProtocol int NOT NULL CONSTRAINT DF_SnmpCredentials_Auth DEFAULT 1,
                                V3AuthPassword nvarchar(max) NULL,
                                V3PrivProtocol int NOT NULL CONSTRAINT DF_SnmpCredentials_Priv DEFAULT 0,
                                V3PrivPassword nvarchar(max) NULL,
                                DateModification datetime2 NOT NULL CONSTRAINT DF_SnmpCredentials_Date DEFAULT (GETUTCDATE()),
                                ModifiePar nvarchar(100) NULL,
                                EstSupprime bit NOT NULL CONSTRAINT DF_SnmpCredentials_Suppr DEFAULT 0
                            );
                        END

                        IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Modeles') AND name = 'SnmpCredentialId')
                        BEGIN
                            ALTER TABLE Modeles ADD SnmpCredentialId int NULL
                                CONSTRAINT FK_Modeles_SnmpCredentials_SnmpCredentialId REFERENCES SnmpCredentials(Id) ON DELETE SET NULL;
                        END

                        IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Imprimantes') AND name = 'SnmpCredentialId')
                        BEGIN
                            ALTER TABLE Imprimantes ADD SnmpCredentialId int NULL
                                CONSTRAINT FK_Imprimantes_SnmpCredentials_SnmpCredentialId REFERENCES SnmpCredentials(Id) ON DELETE SET NULL;
                        END

                        IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_Modeles_SnmpCredentialId')
                            EXEC('CREATE INDEX IX_Modeles_SnmpCredentialId ON Modeles(SnmpCredentialId)');
                        IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_Imprimantes_SnmpCredentialId')
                            EXEC('CREATE INDEX IX_Imprimantes_SnmpCredentialId ON Imprimantes(SnmpCredentialId)');
                    ");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[DbInitializer] Création des identifiants SNMP (SQL Server) en échec : {ex.Message}");
                }

                // Colonnes des migrations du 16/07/2026 (sans attribut [Migration], jamais appliquées par EF sur SQL Server)
                // et colonnes de SnmpProfileItems absentes de la migration du 21/08/2026 (backlog B22). Scripts idempotents.
                try
                {
                    context.Database.ExecuteSqlRaw(@"
                        IF OBJECT_ID('ReportSchedules') IS NOT NULL
                        BEGIN
                            IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('ReportSchedules') AND name = 'IsActive')
                                ALTER TABLE ReportSchedules ADD IsActive bit NOT NULL CONSTRAINT DF_ReportSchedules_IsActive DEFAULT 1;
                            IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('ReportSchedules') AND name = 'PredictionThresholdDays')
                                ALTER TABLE ReportSchedules ADD PredictionThresholdDays int NOT NULL CONSTRAINT DF_ReportSchedules_PredictionThresholdDays DEFAULT 14;
                            IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('ReportSchedules') AND name = 'RunHour')
                                ALTER TABLE ReportSchedules ADD RunHour int NOT NULL CONSTRAINT DF_ReportSchedules_RunHour DEFAULT 6;
                            IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('ReportSchedules') AND name = 'RunMinute')
                                ALTER TABLE ReportSchedules ADD RunMinute int NOT NULL CONSTRAINT DF_ReportSchedules_RunMinute DEFAULT 0;
                            IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('ReportSchedules') AND name = 'RunDayOfWeek')
                                ALTER TABLE ReportSchedules ADD RunDayOfWeek int NULL;
                            IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('ReportSchedules') AND name = 'RunDayOfMonth')
                                ALTER TABLE ReportSchedules ADD RunDayOfMonth int NULL;
                        END

                        IF OBJECT_ID('SnmpProfileItems') IS NOT NULL
                        BEGIN
                            IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('SnmpProfileItems') AND name = 'ModifiePar')
                                ALTER TABLE SnmpProfileItems ADD ModifiePar nvarchar(100) NULL;
                            IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('SnmpProfileItems') AND name = 'EstSupprime')
                                ALTER TABLE SnmpProfileItems ADD EstSupprime bit NOT NULL CONSTRAINT DF_SnmpProfileItems_EstSupprime DEFAULT 0;
                        END
                    ");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[DbInitializer] Mise à niveau du schéma SQL Server (ReportSchedules / SnmpProfileItems) en échec : {ex.Message}");
                }

                try
                {
                    context.Database.ExecuteSqlRaw(@"
                        IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Users') AND name = 'SecurityStamp')
                        BEGIN
                            ALTER TABLE Users ADD SecurityStamp nvarchar(64) NULL;
                        END
                    ");
                }
                catch (Exception ex) { LogSchemaError(ex); }

                // Origine des rôles (AD / manuel). À la création de la colonne, les rôles des utilisateurs AD
                // correspondant à un mapping de groupe sont considérés comme issus de l'AD.
                try
                {
                    context.Database.ExecuteSqlRaw(@"
                        IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('UserRoles') AND name = 'IsFromAd')
                        BEGIN
                            ALTER TABLE UserRoles ADD IsFromAd bit NOT NULL DEFAULT 0;
                            EXEC('UPDATE UserRoles SET IsFromAd = 1
                                  WHERE UserId IN (SELECT Id FROM Users WHERE IsAdUser = 1)
                                    AND RoleId IN (SELECT RoleId FROM AdRoleMappings)');
                        END
                    ");
                }
                catch (Exception ex) { LogSchemaError(ex); }
            }
            else if (context.Database.ProviderName != null && context.Database.ProviderName.Contains("Sqlite", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    SqliteAddColumn(context, "ReportSchedules", "IsActive", "INTEGER NOT NULL DEFAULT 1");
                }
                catch (Exception ex) { LogSchemaError(ex); }

                try
                {
                    SqliteAddColumn(context, "ReportSchedules", "PredictionThresholdDays", "INTEGER NOT NULL DEFAULT 14");
                }
                catch (Exception ex) { LogSchemaError(ex); }

                try
                {
                    SqliteAddColumn(context, "ReportSchedules", "RunHour", "INTEGER NOT NULL DEFAULT 6");
                }
                catch (Exception ex) { LogSchemaError(ex); }

                try
                {
                    SqliteAddColumn(context, "ReportSchedules", "RunMinute", "INTEGER NOT NULL DEFAULT 0");
                }
                catch (Exception ex) { LogSchemaError(ex); }

                try
                {
                    SqliteAddColumn(context, "ReportSchedules", "RunDayOfWeek", "INTEGER NULL");
                }
                catch (Exception ex) { LogSchemaError(ex); }

                try
                {
                    SqliteAddColumn(context, "ReportSchedules", "RunDayOfMonth", "INTEGER NULL");
                }
                catch (Exception ex) { LogSchemaError(ex); }

                try
                {
                    SqliteAddColumn(context, "Imprimantes", "SerialNumber", "TEXT NULL");
                }
                catch (Exception ex) { LogSchemaError(ex); }

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
                catch (Exception ex) { LogSchemaError(ex); }

                try
                {
                    SqliteAddColumn(context, "SnmpProfiles", "IsColor", "INTEGER NOT NULL DEFAULT 0");
                }
                catch (Exception ex) { LogSchemaError(ex); }

                try
                {
                    SqliteAddColumn(context, "Modeles", "SnmpProfileId", "INTEGER NULL");
                }
                catch (Exception ex) { LogSchemaError(ex); }

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
                catch (Exception ex) { LogSchemaError(ex); }

                try
                {
                    context.Database.ExecuteSqlRaw("CREATE INDEX IF NOT EXISTS IX_SnmpProfileItems_SnmpProfileId ON SnmpProfileItems(SnmpProfileId);");
                }
                catch (Exception ex) { LogSchemaError(ex); }

                try
                {
                    SqliteAddColumn(context, "Users", "SecurityStamp", "TEXT NULL");
                }
                catch (Exception ex) { LogSchemaError(ex); }


                // Profils d'identifiants SNMP (SNMPv3, phase B)
                try
                {
                    context.Database.ExecuteSqlRaw(@"
                        CREATE TABLE IF NOT EXISTS SnmpCredentials (
                            Id INTEGER PRIMARY KEY AUTOINCREMENT,
                            Name TEXT NOT NULL,
                            Description TEXT NULL,
                            Version INTEGER NOT NULL DEFAULT 2,
                            Community TEXT NULL,
                            V3Username TEXT NULL,
                            V3SecurityLevel INTEGER NOT NULL DEFAULT 2,
                            V3AuthProtocol INTEGER NOT NULL DEFAULT 1,
                            V3AuthPassword TEXT NULL,
                            V3PrivProtocol INTEGER NOT NULL DEFAULT 0,
                            V3PrivPassword TEXT NULL,
                            DateModification TEXT NOT NULL DEFAULT (datetime('now')),
                            ModifiePar TEXT NULL,
                            EstSupprime INTEGER NOT NULL DEFAULT 0
                        );");
                }
                catch (Exception ex) { LogSchemaError(ex); }
                try { SqliteAddColumn(context, "Modeles", "SnmpCredentialId", "INTEGER NULL REFERENCES SnmpCredentials(Id) ON DELETE SET NULL"); } catch (Exception ex) { LogSchemaError(ex); }
                try { SqliteAddColumn(context, "Imprimantes", "SnmpCredentialId", "INTEGER NULL REFERENCES SnmpCredentials(Id) ON DELETE SET NULL"); } catch (Exception ex) { LogSchemaError(ex); }
                try { context.Database.ExecuteSqlRaw("CREATE INDEX IF NOT EXISTS IX_Modeles_SnmpCredentialId ON Modeles(SnmpCredentialId);"); } catch (Exception ex) { LogSchemaError(ex); }
                try { context.Database.ExecuteSqlRaw("CREATE INDEX IF NOT EXISTS IX_Imprimantes_SnmpCredentialId ON Imprimantes(SnmpCredentialId);"); } catch (Exception ex) { LogSchemaError(ex); }

                // Origine des rôles (AD / manuel) : la mise à jour ne s'exécute que si l'ajout de colonne réussit (première fois)
                try
                {
                    if (SqliteAddColumn(context, "UserRoles", "IsFromAd", "INTEGER NOT NULL DEFAULT 0"))
                        context.Database.ExecuteSqlRaw(@"
                        UPDATE UserRoles SET IsFromAd = 1
                        WHERE UserId IN (SELECT Id FROM Users WHERE IsAdUser = 1)
                          AND RoleId IN (SELECT RoleId FROM AdRoleMappings);");
                }
                catch (Exception ex) { LogSchemaError(ex); }
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
                // Installation neuve : TLS activé par défaut pour le SMTP (audit #14). Les installations existantes ne sont pas modifiées.
                var smtpSsl = context.ServerSettings.FirstOrDefault(s => s.Key == "SmtpEnableSsl");
                if (smtpSsl != null) smtpSsl.Value = "true";

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

            ApplyPermissionModelChanges(context);
        }

        /// <summary>
        /// Novembre 2026 : « Rôles - suppression » fusionnée dans « Rôles - écriture » (même effet, un seul droit à gérer).
        /// La migration SQL Server fait la même chose ; ce code couvre les bases SQLite (pas de migrations).
        /// </summary>
        private static void ApplyPermissionModelChanges(ApplicationDbContext context)
        {
            var obsolete = context.Permissions.FirstOrDefault(p => p.Code == "ROLE_DELETE");
            if (obsolete != null)
            {
                Console.WriteLine("--> Fusion de la permission ROLE_DELETE dans ROLE_WRITE");
                context.RolePermissions.RemoveRange(context.RolePermissions.Where(rp => rp.PermissionId == obsolete.Id));
                context.Permissions.Remove(obsolete);
            }

            var descriptions = new System.Collections.Generic.Dictionary<string, string>
            {
                ["ROLE_WRITE"] = "Créer/Modifier/Supprimer des rôles",
                ["ADMIN_ACCESS"] = "Accès complet : tous les droits"
            };
            foreach (var (code, description) in descriptions)
            {
                var permission = context.Permissions.FirstOrDefault(p => p.Code == code);
                if (permission != null && permission.Description != description) permission.Description = description;
            }
            context.SaveChanges();
        }
    }
}