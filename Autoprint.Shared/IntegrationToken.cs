using System;
using System.ComponentModel.DataAnnotations;

namespace Autoprint.Shared
{
    public class IntegrationToken : BaseEntity
    {
        [Required]
        [MaxLength(256)]
        public string TokenHash { get; set; } = string.Empty;

        [MaxLength(500)]
        public string? Description { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? ExpiresAt { get; set; }
    }

    public class IntegrationTokenCreationDto
    {
        public string? Description { get; set; }
        public DateTime? ExpiresAt { get; set; }
    }

    public class IntegrationTokenCreationResultDto
    {
        public int Id { get; set; }
        public string RawToken { get; set; } = string.Empty;
        public string? Description { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? ExpiresAt { get; set; }
    }
}
