using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Autoprint.Shared
{
    public enum SnmpItemCategory
    {
        Toner = 0,
        Tray = 1,
        PageCounter = 2,
        Status = 3,
        Console = 4,
        Maintenance = 5,
        SerialNumber = 6
    }

    public enum SnmpValueType
    {
        Percentage = 0,     // 0-100%
        RawWithMax = 1,     // Level / MaxCapacity * 100
        Counter = 2,        // Numeric integer
        String = 3          // Text string
    }

    public class SnmpProfileItem : BaseEntity
    {
        public int SnmpProfileId { get; set; }

        [ForeignKey("SnmpProfileId")]
        public SnmpProfile? SnmpProfile { get; set; }

        public SnmpItemCategory Category { get; set; } = SnmpItemCategory.Toner;

        [Required]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        [Required]
        [MaxLength(150)]
        public string Oid { get; set; } = string.Empty;

        [MaxLength(150)]
        public string? OidMaxCapacity { get; set; }

        public SnmpValueType ValueType { get; set; } = SnmpValueType.Percentage;

        [MaxLength(20)]
        public string ColorHex { get; set; } = "#212529";

        public int SortOrder { get; set; } = 0;
    }
}
