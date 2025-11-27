using System.Collections.Generic;

namespace Autoprint.Client.Models
{
    public class UserPreferences
    {
        // Préférences Générales
        public bool EnableNotifications { get; set; } = true;
        public bool AutoSwitchDefaultPrinter { get; set; } = false; // Le switch demandé
        public bool StartMinimized { get; set; } = true;

        // CONFIGURATION SYSTÈME (Sauvegardée ici)
        public string? PrintServerName { get; set; }

        // Clé d'API pour l'authentification Agent ---
        public string? AgentApiKey { get; set; }

        // Historique & Intelligence (Location Awareness)
        public string? LastDetectedLocationCode { get; set; }

        // Mémorisation des choix utilisateur par Lieu
        // Clé = Code du Lieu, Valeur = Nom de l'imprimante
        public Dictionary<string, string> PreferredPrinters { get; set; } = new Dictionary<string, string>();
    }
}