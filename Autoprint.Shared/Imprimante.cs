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
        // Chiffrée en base (préfixe enc:v1:, B32) : longueur libre, une valeur chiffrée dépasse 100 caractères.
        // Jamais renvoyée par l'API : vide à l'enregistrement = valeur actuelle conservée.
        public string SnmpCommunity { get; set; } = "public";
        public int SnmpVersion { get; set; } = 2; // 1 = v1, 2 = v2c, 3 = v3 (identifiant SNMP enregistré)

        // #40 : false = connexion du profil SNMP du modèle ; true = connexion propre à cette imprimante
        // (SnmpVersion + SnmpCommunity, ou SnmpCredentialId en v3), pour les cas particuliers
        public bool SnmpConnexionPropre { get; set; } = false;

        // Identifiant SNMP enregistré (v3) de la connexion propre
        public int? SnmpCredentialId { get; set; }

        [MaxLength(20)]
        public string MonitoringStatus { get; set; } = "Inconnu";
        [MaxLength(100)]
        public string? SerialNumber { get; set; }
        public DateTime? LastSeen { get; set; }

        // #52 : consommable qui finira le plus tôt (calculé par l'API depuis SupplySnapshots, pas en base)
        [NotMapped]
        public string? NextSupplyName { get; set; }
        [NotMapped]
        public string? NextSupplyDetail { get; set; }
        /// <summary>Urgence pour le tri (plus petit = plus urgent), null sans relevé de consommable.</summary>
        [NotMapped]
        public int? NextSupplyUrgency { get; set; }
        [NotMapped]
        public int SuppliesToOrder { get; set; }
        public bool IsArchived { get; set; } = false;
    }
}