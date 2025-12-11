using Autoprint.Client.Models;
using System;

namespace Autoprint.Client.Services
{
    public class ConfigurationService
    {
        public string ApiKey { get; private set; } = string.Empty;
        public string PrintServerName { get; private set; } = string.Empty;

        public void Initialize(string[] args, UserPreferencesService prefService)
        {
            ApiKey = prefService.Current.AgentApiKey ?? string.Empty;
            PrintServerName = prefService.Current.PrintServerName ?? string.Empty;

            bool modificationDetectee = false;

            string? argKey = GetArgValue(args, "--api-key");
            string? argServer = GetArgValue(args, "--print-server");

            if (!string.IsNullOrEmpty(argKey) && argKey != ApiKey)
            {
                ApiKey = argKey;
                prefService.Current.AgentApiKey = ApiKey;
                modificationDetectee = true;
            }

            if (!string.IsNullOrEmpty(argServer) && argServer != PrintServerName)
            {
                PrintServerName = argServer;
                prefService.Current.PrintServerName = PrintServerName;
                modificationDetectee = true;
            }

            if (modificationDetectee)
            {
                prefService.Save();
            }
        }

        private string? GetArgValue(string[] args, string name)
        {
            for (int i = 0; i < args.Length; i++)
            {
                if ((args[i] == name || args[i] == name.Replace("--", "/")) && i + 1 < args.Length)
                    return args[i + 1];
            }
            return null;
        }
    }
}