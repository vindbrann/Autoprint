using Microsoft.Win32;
using System;
using System.Diagnostics;

namespace Autoprint.Client.Services
{
    /// <summary>
    /// Configuration machine (HKLM\SOFTWARE\Autoprint, écrite par le MSI) :
    /// PRINTSERVER = nom des partages d'impression (\\serveur\imprimante) ;
    /// SERVERURL = adresse de la console et de l'API (facultative : à défaut, https://PRINTSERVER, comme avant).
    /// </summary>
    public class ConfigurationService
    {
        public string ApiKey { get; private set; } = string.Empty;
        public string PrintServerName { get; private set; } = string.Empty;
        public string ServerUrl { get; private set; } = string.Empty;

        /// <summary>Adresse de base de l'API : SERVERURL, ou à défaut le serveur d'impression en HTTPS.</summary>
        public string WebBaseUrl
        {
            get
            {
                string raw = !string.IsNullOrWhiteSpace(ServerUrl) ? ServerUrl.Trim()
                           : !string.IsNullOrWhiteSpace(PrintServerName) ? PrintServerName.Trim()
                           : "localhost";
                return raw.StartsWith("http", StringComparison.OrdinalIgnoreCase) ? raw : $"https://{raw}";
            }
        }

        public void Initialize(string[] args, UserPreferencesService prefService)
        {
            LoadFromRegistry();

            string? argKey = GetArgValue(args, "--api-key");
            string? argServer = GetArgValue(args, "--print-server");
            string? argUrl = GetArgValue(args, "--server-url");

            if (!string.IsNullOrEmpty(argKey))
            {
                ApiKey = argKey;
                Debug.WriteLine("[Config] Surcharge API Key via arguments.");
            }

            if (!string.IsNullOrEmpty(argServer))
            {
                PrintServerName = argServer;
                Debug.WriteLine($"[Config] Surcharge serveur d'impression via arguments : {PrintServerName}");
            }

            if (!string.IsNullOrEmpty(argUrl))
            {
                ServerUrl = argUrl;
                Debug.WriteLine($"[Config] Surcharge adresse de la console via arguments : {ServerUrl}");
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
                    ServerUrl = key.GetValue("SERVERURL") as string ?? string.Empty;
                    ApiKey = key.GetValue("APIKEY") as string ?? string.Empty;
                    Debug.WriteLine($"[Config] Chargé depuis Registre : Serveur={PrintServerName}, Console={ServerUrl}");
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
