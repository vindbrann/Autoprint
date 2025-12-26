using System.ComponentModel.DataAnnotations;

namespace Autoprint.Shared
{
    public abstract class BaseEntity
    {
        [Key]
        public int Id { get; set; }

        public DateTime DateModification { get; set; } = DateTime.UtcNow;

        public bool EstSupprime { get; set; } = false;
    }
}