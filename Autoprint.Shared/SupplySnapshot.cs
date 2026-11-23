using System;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Autoprint.Shared.DTOs;

namespace Autoprint.Shared
{
    /// <summary>
    /// État actuel d'un consommable (#52), mis à jour à chaque supervision et à chaque diagnostic : la liste du parc et
    /// la vue « à commander » le lisent sans interroger les imprimantes.
    /// </summary>
    public class SupplySnapshot : BaseEntity
    {
        public int ImprimanteId { get; set; }

        [JsonIgnore]
        public Imprimante? Imprimante { get; set; }

        [Required]
        [MaxLength(120)]
        public string SupplyKey { get; set; } = string.Empty;

        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        public SupplyKind Kind { get; set; }
        public int Level { get; set; } = -1;
        public SupplyState State { get; set; } = SupplyState.Unknown;
        public bool IsReceptacle { get; set; }
        public int? DaysRemaining { get; set; }
        public ForecastConfidence Confidence { get; set; }
        public bool NeedsOrder { get; set; }
        public DateTime? LowSince { get; set; }
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
