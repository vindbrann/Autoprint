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
        public bool IsDirectPrintingEnabled { get; set; } = false;
        public int EmplacementId { get; set; }
        public Emplacement? Emplacement { get; set; }
        [MaxLength(100)]
        public string? Localisation { get; set; }
        public int ModeleId { get; set; }
        public Modele? Modele { get; set; }
        public PrinterStatus Status { get; set; } = PrinterStatus.PendingCreation;

        public int SnmpPort { get; set; } = 161;
        [MaxLength(100)]
        public string SnmpCommunity { get; set; } = "public";
        public int SnmpVersion { get; set; } = 2; // 1 = v1, 2 = v2c

        [MaxLength(20)]
        public string MonitoringStatus { get; set; } = "Inconnu";
        [MaxLength(100)]
        public string? SerialNumber { get; set; }
        public DateTime? LastSeen { get; set; }
        public bool IsArchived { get; set; } = false;
    }
}