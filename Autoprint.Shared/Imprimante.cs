using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Autoprint.Shared
{
    public class Imprimante : BaseEntity
    {
        [Required]
        [MaxLength(200)]
        public string NomAffiche { get; set; } = string.Empty;

        [MaxLength(50)]
        public string? Code { get; set; }

        [Required]
        [MaxLength(50)]
        public string AdresseIp { get; set; } = string.Empty;

        public bool EstPartagee { get; set; } = false;
        public string? NomPartage { get; set; }
        public string? Commentaire { get; set; }

        // Relations
        public int EmplacementId { get; set; }
        public Emplacement? Emplacement { get; set; }

        [MaxLength(100)]
        public string? Localisation { get; set; }
        public int ModeleId { get; set; }
        public Modele? Modele { get; set; }

    }
}