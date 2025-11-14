using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Autoprint.Server.Models
{
    public class Imprimante : BaseEntity
    {
        [Required]
        [MaxLength(200)]
        public string NomAffiche { get; set; } = string.Empty;

        [Required]
        [MaxLength(50)]
        public string AdresseIp { get; set; } = string.Empty;

        public bool EstPartagee { get; set; } = false;
        public string? NomPartage { get; set; }
        public string? Commentaire { get; set; }

        public bool EstParDefaut { get; set; } = false;

        // Relations
        public int EmplacementId { get; set; }
        public Emplacement? Emplacement { get; set; }

        [MaxLength(100)]
        public string? Localisation { get; set; } // Ex: "Bureau 402", "Accueil"
        public int ModeleId { get; set; }
        public Modele? Modele { get; set; }

        public int PiloteId { get; set; }
        public Pilote? Pilote { get; set; }
    }
}