using Autoprint.Shared.Enums;

namespace Autoprint.Shared.DTOs
{
    public class SyncPreviewDto
    {
        public int Id { get; set; }
        public string NomImprimante { get; set; } = string.Empty;
        public PrinterStatus Status { get; set; }
        public string Action { get; set; } = string.Empty; // "Création", "Modif", "Suppr"
        public string ModifiePar { get; set; } = "Système"; // Nom de l'utilisateur
        public DateTime DateModification { get; set; }

        // Détails techniques (ex: "IP: 10.0.0.1")
        public string Details { get; set; } = string.Empty;

        // Pour la case à cocher (UI seulement)
        public bool IsSelected { get; set; } = true;
    }
}