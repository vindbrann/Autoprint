using System.ComponentModel.DataAnnotations;

namespace Autoprint.Shared
{
    public class SnmpProfile : BaseEntity
    {
        [Required]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        [MaxLength(150)]
        public string? OidTonerBlack { get; set; }

        [MaxLength(150)]
        public string? OidTonerCyan { get; set; }

        [MaxLength(150)]
        public string? OidTonerMagenta { get; set; }

        [MaxLength(150)]
        public string? OidTonerYellow { get; set; }

        [MaxLength(150)]
        public string? OidPageCounter { get; set; }
    }

    public class SnmpProfileImportDto
    {
        public string Name { get; set; } = string.Empty;
        public string? OidTonerBlack { get; set; }
        public string? OidTonerCyan { get; set; }
        public string? OidTonerMagenta { get; set; }
        public string? OidTonerYellow { get; set; }
        public string? OidPageCounter { get; set; }
    }
}
