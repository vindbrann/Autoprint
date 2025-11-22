using System.ComponentModel.DataAnnotations;

namespace Autoprint.Shared.DTOs
{
    // Ce qu'on envoie au serveur pour demander un scan distant
    public class RemoteScanRequestDto
    {
        [Required]
        public string TargetHost { get; set; } = "localhost"; // IP ou Nom DNS

        public string? Username { get; set; } // Optionnel (si null -> Auth Windows intégrée)
        public string? Password { get; set; }
    }

    // Résultat : Une imprimante détectée
    public class DiscoveredPrinterDto
    {
        public string Name { get; set; } = "";
        public string ShareName { get; set; } = "";
        public string DriverName { get; set; } = "";
        public string PortName { get; set; } = "";
        public bool IsShared { get; set; }

        // Info BDD
        public bool ExistsInDb { get; set; }
        public int? ExistingId { get; set; }
    }

    // Résultat : Un pilote détecté (État de synchro)
    public class DiscoveredDriverDto
    {
        public string Name { get; set; } = "";
        public string DriverVersion { get; set; } = "";

        // États possibles : 
        // New (Nouveau sur Windows), 
        // Synced (Déjà en BDD), 
        // Missing (En BDD mais plus sur Windows - géré côté serveur)
        public string SyncStatus { get; set; } = "New";
    }
}