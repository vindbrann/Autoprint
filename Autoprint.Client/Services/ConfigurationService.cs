using Autoprint.Client.Models;
using Microsoft.Win32;
using System;
using System.Diagnostics;

namespace Autoprint.Client.Services
{
    public class ConfigurationService
    {
        public string ApiKey { get; private set; } = string.Empty;
        public string PrintServerName { get; private set; } = string.Empty;

        public void Initialize(string[] args, UserPreferencesService prefService)
        {
            LoadFromRegistry();

            string? argKey = GetArgValue(args, "--api-key");
            string? argServer = GetArgValue(args, "--print-server");

            if (!string.IsNullOrEmpty(argKey))
            {
                ApiKey = argKey;
                Debug.WriteLine($"[Config] Surcharge API Key via arguments : {ApiKey}");
            }

            if (!string.IsNullOrEmpty(argServer))
            {
                PrintServerName = argServer;
                Debug.WriteLine($"[Config] Surcharge Serveur via arguments : {PrintServerName}");
            }

        }

        private void LoadFromRegistry()
        {
            try
            {
                using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Autoprint");

                if (key != null)
                {
                    PrintServerName = key.GetValue("PRINTSERVER") as string ?? string.Empty;
                    ApiKey = key.GetValue("APIKEY") as string ?? string.Empty;
                    Debug.WriteLine($"[Config] Chargé depuis Registre : Serveur={PrintServerName}");
                }
                else
                {
                    Debug.WriteLine("[Config] Aucune clé HKLM trouvée (Première installation ?)");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Config] Erreur lecture Registre : {ex.Message}");
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