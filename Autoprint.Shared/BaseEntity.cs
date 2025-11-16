using System.ComponentModel.DataAnnotations;

namespace Autoprint.Shared
{
    public abstract class BaseEntity
    {
        [Key]
        public int Id { get; set; }

        // Utile pour la synchronisation Delta (Phase 5) [cite: 243]
        public DateTime DateModification { get; set; } = DateTime.UtcNow;

        // Soft Delete : on ne supprime jamais vraiment une ligne [cite: 18]
        public bool EstSupprime { get; set; } = false;
    }
}