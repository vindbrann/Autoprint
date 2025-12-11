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

            if (!context.Users.Any())
            {
                Console.WriteLine("--> Initialisation Admin (admin/admin123)...");
                var adminUser = new User
                {
                    Username = "admin",
                    DisplayName = "Administrateur",
                    IsAdUser = false,
                    IsActive = true,
                    PasswordHash = SecurityHelper.ComputeSha256Hash("admin123"),
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