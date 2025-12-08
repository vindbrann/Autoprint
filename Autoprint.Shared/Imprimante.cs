using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Autoprint.Shared.Enums;

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
        public bool IsBranchOfficeEnabled { get; set; } = false;
        // Relations
        public int EmplacementId { get; set; }
        public Emplacement? Emplacement { get; set; }

        [MaxLength(100)]
        public string? Localisation { get; set; }
        public int ModeleId { get; set; }
        public Modele? Modele { get; set; }
        public PrinterStatus Status { get; set; } = PrinterStatus.PendingCreation;
    }
}