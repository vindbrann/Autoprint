using System.ComponentModel.DataAnnotations;

namespace Autoprint.Server.Models
{
    public class Pilote : BaseEntity
    {
        [Required]
        [MaxLength(200)]
        public string Nom { get; set; } = string.Empty;

        [MaxLength(50)]
        public string Version { get; set; } = string.Empty;

        // Chemin où le fichier est stocké sur le serveur
        public string CheminFichier { get; set; } = string.Empty;

        // Nom du fichier .inf pour l'installation silencieuse
        public string NomFichierInf { get; set; } = string.Empty;

        // Sécurité : Hash pour valider que le fichier n'est pas corrompu [cite: 63]
        [MaxLength(64)]
        public string Checksum { get; set; } = string.Empty;
    }
}