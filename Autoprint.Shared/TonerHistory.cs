using System;
using System.ComponentModel.DataAnnotations;

namespace Autoprint.Shared
{
    public class TonerHistory : BaseEntity
    {
        public int ImprimanteId { get; set; }
        public Imprimante? Imprimante { get; set; }

        [Required]
        [MaxLength(50)]
        public string ComponentColor { get; set; } = string.Empty;

        public int LevelPercent { get; set; }

        public DateTime RecordedAt { get; set; } = DateTime.UtcNow;
    }
}
