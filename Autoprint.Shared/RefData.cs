using System.ComponentModel.DataAnnotations;

namespace Autoprint.Shared
{
    public class Marque : BaseEntity
    {
        [Required]
        [MaxLength(100)]
        public string Nom { get; set; } = string.Empty;
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
    }

    public class Emplacement : BaseEntity
    {
        [Required]
        [MaxLength(200)]
        public string Nom { get; set; } = string.Empty;

        [MaxLength(50)]
        public string? Code { get; set; }

        [Required]
        [MaxLength(50)]
        public string CidrIpv4 { get; set; } = string.Empty;
    }
}