using System.ComponentModel.DataAnnotations;

namespace Autoprint.Server.Models
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

        public int MarqueId { get; set; }
        public Marque? Marque { get; set; }
    }

    public class Emplacement : BaseEntity
    {
        [Required]
        [MaxLength(200)]
        public string Nom { get; set; } = string.Empty;

        // Le CIDR est crucial pour la détection réseau (ex: 192.168.1.0/24) [cite: 15]
        [Required]
        [MaxLength(50)]
        public string CidrIpv4 { get; set; } = string.Empty;
    }
}