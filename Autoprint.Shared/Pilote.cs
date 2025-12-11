using System.ComponentModel.DataAnnotations;

namespace Autoprint.Shared
{
    public class Pilote : BaseEntity
    {
        [Required]
        [MaxLength(200)]
        public string Nom { get; set; } = string.Empty;

        [MaxLength(50)]
        public string Version { get; set; } = "";

        public bool EstInstalle { get; set; } = true;
    }
}