using System;
using System.ComponentModel.DataAnnotations;

namespace Autoprint.Shared
{
    public class ReportSchedule : BaseEntity
    {
        [Required]
        [MaxLength(100)]
        public string ReportName { get; set; } = string.Empty;

        [Required]
        public string SelectedMetricsJson { get; set; } = "[]"; // ex: ["Pages", "Toner", "Availability", "Alerts"]

        [Required]
        public string ScopeFilterJson { get; set; } = "{}"; // ex: {"BrandId": 0, "ModelId": 0, "LocationId": 0}

        [Required]
        [MaxLength(50)]
        public string Frequency { get; set; } = "Quotidien"; // Quotidien, Hebdomadaire, Mensuel

        [Required]
        [MaxLength(500)]
        public string EmailRecipients { get; set; } = string.Empty;

        [Required]
        [MaxLength(20)]
        public string Format { get; set; } = "PDF"; // CSV, PDF

        public DateTime? LastRunAt { get; set; }
        public DateTime? NextRunAt { get; set; }
    }
}
