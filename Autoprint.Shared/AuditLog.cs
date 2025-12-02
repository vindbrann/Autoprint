using System.ComponentModel.DataAnnotations;

namespace Autoprint.Shared
{
    public class AuditLog
    {
        [Key]
        public int Id { get; set; }

        public DateTime DateAction { get; set; } = DateTime.UtcNow;

        [MaxLength(100)]
        public string Utilisateur { get; set; } = "System";

        [MaxLength(50)]
        public string Action { get; set; } = string.Empty;

        [MaxLength(50)]
        public string? ResourceName { get; set; }

        public string? OldValues { get; set; }

        public string? NewValues { get; set; }

        public string Details { get; set; } = string.Empty;

        [MaxLength(20)]
        public string Niveau { get; set; } = "INFO";
    }
}