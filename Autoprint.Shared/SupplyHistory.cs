using System;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Autoprint.Shared.DTOs;

namespace Autoprint.Shared
{
    /// <summary>
    /// Relevé d'un consommable ou d'un élément de maintenance (#51), repéré par sa clé stable (« supply:1.2 », ou
    /// « name:… » pour un champ de profil manuel). Écrit quand le niveau ou l'état change, et au moins une fois par jour.
    /// </summary>
    public class SupplyHistory : BaseEntity
    {
        public int ImprimanteId { get; set; }

        [JsonIgnore]
        public Imprimante? Imprimante { get; set; }

        [Required]
        [MaxLength(120)]
        public string SupplyKey { get; set; } = string.Empty;

        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        /// <summary>Niveau en %, -1 si l'imprimante ne le communique pas.</summary>
        public int Level { get; set; } = -1;

        public SupplyState State { get; set; } = SupplyState.Unknown;

        public bool IsReceptacle { get; set; }

        /// <summary>Compteur total de pages de l'imprimante au moment du relevé (0 si inconnu).</summary>
        public long PageCounter { get; set; }

        /// <summary>Premier relevé d'un nouveau consommable (niveau qui remonte nettement, ou retour à OK après Bas/Vide).</summary>
        public bool IsReplacement { get; set; }

        public DateTime RecordedAt { get; set; } = DateTime.UtcNow;
    }
}
