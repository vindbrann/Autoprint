using Autoprint.Client.Models;
using System;

namespace Autoprint.Client.Services
{
    public class ConfigurationService
    {
        // Propriétés exposées en lecture seule (pointent vers les données chargées)
        public string ApiKey { get; private set; } = string.Empty;
        public string PrintServerName { get; private set; } = string.Empty;

        // On injecte le UserPreferencesService pour sauvegarder les changements
        public void Initialize(string[] args, UserPreferencesService prefService)
        {
            // 1. Charger l'état actuel depuis le fichier JSON
            ApiKey = prefService.Current.AgentApiKey ?? string.Empty;
            PrintServerName = prefService.Current.PrintServerName ?? string.Empty;

            bool modificationDetectee = false;

            // 2. Vérifier les Arguments CLI (MSI / Raccourci)
            // ex: --api-key "XYZ" --print-server "SRV-01"
            string? argKey = GetArgValue(args, "--api-key");
            string? argServer = GetArgValue(args, "--print-server");

            // Si un argument est fourni et différent de la config actuelle -> Mise à jour
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

            // 3. Sauvegarder si nécessaire (Persistance)
            if (modificationDetectee)
            {
                prefService.Save();
            }
        }

        // Helper simple pour lire les arguments
        private string? GetArgValue(string[] args, string name)
        {
            for (int i = 0; i < args.Length; i++)
            {
                // Supporte --api-key et /api-key
                if ((args[i] == name || args[i] == name.Replace("--", "/")) && i + 1 < args.Length)
                    return args[i + 1];
            }
            return null;
        }
    }
}