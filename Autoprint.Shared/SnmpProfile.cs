using System.Collections.Generic;
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

        public bool IsColor { get; set; } = false;

        public List<SnmpProfileItem> Items { get; set; } = new();
    }

    public class SnmpProfileImportDto
    {
        public string Name { get; set; } = string.Empty;
        public bool IsColor { get; set; } = false;
        public string? OidTonerBlack { get; set; }
        public string? OidTonerCyan { get; set; }
        public string? OidTonerMagenta { get; set; }
        public string? OidTonerYellow { get; set; }
        public string? OidPageCounter { get; set; }
        public List<SnmpProfileItemImportDto>? Items { get; set; }
    }

    public class SnmpProfileItemImportDto
    {
        public SnmpItemCategory Category { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Oid { get; set; } = string.Empty;
        public string? OidMaxCapacity { get; set; }
        public SnmpValueType ValueType { get; set; }
        public string ColorHex { get; set; } = "#212529";
        public int SortOrder { get; set; }
    }
}

