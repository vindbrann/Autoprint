using System.ComponentModel.DataAnnotations;

namespace Autoprint.Shared
{
    public class ServerSetting
    {
        [Key]
        [MaxLength(50)]
        public string Key { get; set; } = string.Empty; // La clé unique (ex: "SmtpHost")

        public string Value { get; set; } = string.Empty; // La valeur (ex: "smtp.office365.com")

        [MaxLength(200)]
        public string Description { get; set; } = string.Empty; // Pour aider l'admin : "Adresse du serveur mail"

        [MaxLength(20)]
        public string Type { get; set; } = "STRING"; // STRING, INT, BOOL, PASSWORD (pour masquer dans l'UI)
    }
}