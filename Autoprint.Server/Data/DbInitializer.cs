using Autoprint.Server.Helpers;
using Autoprint.Server.Models.Security;
using Autoprint.Shared;
using System;
using System.Linq;

namespace Autoprint.Server.Data
{
    public static class DbInitializer
    {
        public static void Initialize(ApplicationDbContext context)
        {
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
        }
    }
}