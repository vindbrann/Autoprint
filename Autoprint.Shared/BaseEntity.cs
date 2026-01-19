using System.ComponentModel.DataAnnotations;

namespace Autoprint.Shared
{
    public abstract class BaseEntity
    {
        [Key]
        public int Id { get; set; }

        public DateTime DateModification { get; set; } = DateTime.UtcNow;
        [MaxLength(100)]
        public string? ModifiePar { get; set; }
        public bool EstSupprime { get; set; } = false;
    }
}