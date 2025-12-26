using System;
using System.ComponentModel.DataAnnotations;

namespace Autoprint.Shared
{
    public class DiscoveryProfile : BaseEntity
    {
        [Required]
        [MaxLength(100)]
        public string Name { get; set; } = "Scan Profile";

        [Required]
        public string TargetRanges { get; set; } = string.Empty;

        public string? ExcludedRanges { get; set; }

        public string ProbeTargets { get; set; } = "254;1";

        public bool SkipKnownSubnets { get; set; } = true;

        [Range(0, 23)]
        public int ScheduleHour { get; set; } = 2;

        public ScanDays ScheduleDays { get; set; } = ScanDays.EveryDay;

        public bool IsEnabled { get; set; } = false;

        public DateTime? LastRunDate { get; set; }
        public string? LastRunResult { get; set; }

        public bool SendEmailReport { get; set; }
        public string? EmailRecipients { get; set; }
    }
}