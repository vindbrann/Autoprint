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
                context.ServerSettings.Add(new ServerSetting
                {
                    Key = "AgentApiKey",
                    Value = Guid.NewGuid().ToString(),
                    Description = "Clé API Agent (Sécurité)",
                    Type = "PASSWORD"
                });

                context.SaveChanges();
            }
        }
    }
}