using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace Autoprint.Shared
{
    public class Marque : BaseEntity
    {
        [Required]
        [MaxLength(100)]
        public string Nom { get; set; } = string.Empty;
        [NotMapped]
        public int PrinterCount { get; set; }
    }

    public class Modele : BaseEntity
    {
        [Required]
        [MaxLength(100)]
        public string Nom { get; set; } = string.Empty;

        [MaxLength(50)]
        public string? Code { get; set; }
        public int MarqueId { get; set; }
        public Marque? Marque { get; set; }
        public int? PiloteId { get; set; }
        public Pilote? Pilote { get; set; }
        [NotMapped]
        public int PrinterCount { get; set; }

    }

    public class EmplacementNetwork : BaseEntity
    {
        [Required]
        public int EmplacementId { get; set; }

        [JsonIgnore]
        public Emplacement? Emplacement { get; set; }

        [Required]
        [MaxLength(50)]
        public string CidrIpv4 { get; set; } = string.Empty;

        [MaxLength(100)]
        public string? Description { get; set; }
        public bool IsPrimary { get; set; }
    }

    public class Emplacement : BaseEntity
    {
        [Required]
        [MaxLength(200)]
        public string Nom { get; set; } = string.Empty;

        [MaxLength(50)]
        public string? Code { get; set; }
        public List<EmplacementNetwork> Networks { get; set; } = new();
        public LieuStatus Status { get; set; } = LieuStatus.Active;

        [NotMapped]
        public int PrinterCount { get; set; }

        [NotMapped]
        public string NetworkSummary => Networks != null && Networks.Any()
            ? string.Join(", ", Networks.Select(n => n.CidrIpv4))
            : "Aucun réseau";

        [NotMapped]
        public string PrimaryCidr => Networks != null && Networks.Any()
            ? (Networks.FirstOrDefault(n => n.IsPrimary)?.CidrIpv4 ?? string.Empty)
            : string.Empty;
    }
}