using System.ComponentModel.DataAnnotations;

namespace Autoprint.Shared
{
    public class AuditLog
    {
        [Key]
        public int Id { get; set; }

        public DateTime DateAction { get; set; } = DateTime.UtcNow;

        [MaxLength(100)]
        public string Utilisateur { get; set; } = "System"; // Pour l'instant "System", plus tard on mettra le vrai login

        [MaxLength(50)]
        public string Action { get; set; } = string.Empty; // Ex: "SYNC_ERROR", "CREATE_PRINTER"

        // Pas de limite de taille pour les détails (ex: message d'erreur complet)
        public string Details { get; set; } = string.Empty;

        [MaxLength(20)]
        public string Niveau { get; set; } = "INFO"; // INFO, WARNING, ERROR
    }
}