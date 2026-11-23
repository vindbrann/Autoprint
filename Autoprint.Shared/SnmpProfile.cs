using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace Autoprint.Shared
{
    /// <summary>#50 : Automatique = tout ce que l'imprimante décrit (Printer MIB) + modifications ; Manuel = seuls les champs déclarés.</summary>
    public enum SnmpProfileMode
    {
        Manuel = 0,
        Automatique = 1
    }

    public class SnmpProfile : BaseEntity
    {
        [Required]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        [MaxLength(150)]
        public string? OidTonerBlack { get; set; }

        [MaxLength(150)]
        public string? OidTonerCyan { get; set; }

        [MaxLength(150)]
        public string? OidTonerMagenta { get; set; }

        [MaxLength(150)]
        public string? OidTonerYellow { get; set; }

        [MaxLength(150)]
        public string? OidPageCounter { get; set; }

        public bool IsColor { get; set; } = false;

        // #40 : connexion SNMP du profil. Elle sert au scan et aux tests, et c'est la connexion par défaut des imprimantes
        // du modèle (sauf connexion propre de l'imprimante). 1 = v1, 2 = v2c, 3 = v3 (identifiant SNMP enregistré).
        public int SnmpVersion { get; set; } = 2;

        // Communauté v1 / v2c chiffrée en base (B32) : jamais sérialisée, ni dans l'API, ni dans le journal d'audit
        [JsonIgnore]
        [Column("SnmpCommunity")]
        public string? SnmpCommunityChiffree { get; set; }

        // Saisie seulement (jamais renvoyée) : vide à l'enregistrement = valeur actuelle conservée
        [NotMapped]
        public string? SnmpCommunity { get; set; }

        // Renseigné par l'API (la communauté elle-même n'est jamais renvoyée)
        [NotMapped]
        public bool HasSnmpCommunity { get; set; }

        // Identifiant SNMP enregistré (v3)
        public int? SnmpCredentialId { get; set; }

        public List<SnmpProfileItem> Items { get; set; } = new();

        // #50 : mode automatique par défaut pour les nouveaux profils (les profils existants sont convertis au démarrage)
        public SnmpProfileMode Mode { get; set; } = SnmpProfileMode.Automatique;

        // Seuil « bas » propre au profil (1-99 %), sinon réglage global
        public int? LowThresholdPercent { get; set; }

        // #51 : délai d'approvisionnement propre au profil (jours), sinon réglage global
        public int? SupplyLeadTimeDays { get; set; }

        // Modifications des éléments détectés automatiquement (renommer, masquer, couleur, seuil)
        public List<SnmpProfileOverride> Overrides { get; set; } = new();
    }

    /// <summary>
    /// Modification d'un élément détecté automatiquement (#50), repéré par sa clé stable dans la Printer MIB
    /// (« supply:1.2 » pour un consommable, « input:1.1 » pour un bac).
    /// </summary>
    public class SnmpProfileOverride : BaseEntity
    {
        public int SnmpProfileId { get; set; }

        [ForeignKey("SnmpProfileId")]
        [JsonIgnore]
        public SnmpProfile? SnmpProfile { get; set; }

        [Required]
        [MaxLength(50)]
        public string Key { get; set; } = string.Empty;

        [MaxLength(100)]
        public string? DisplayName { get; set; }

        public bool Hidden { get; set; }

        [MaxLength(20)]
        public string? ColorHex { get; set; }

        public int? LowThresholdPercent { get; set; }

        [JsonIgnore]
        [NotMapped]
        public bool IsEmpty => string.IsNullOrWhiteSpace(DisplayName) && !Hidden && string.IsNullOrWhiteSpace(ColorHex) && LowThresholdPercent == null;
    }

    /// <summary>OID que la détection automatique lit déjà : un champ de profil sur l'un d'eux est ignoré en mode automatique.</summary>
    public static class SnmpStandardOids
    {
        public const string SupplyLevelPrefix = "1.3.6.1.2.1.43.11.1.1.9.";
        public const string InputLevelPrefix = "1.3.6.1.2.1.43.8.2.1.10.";
        public const string ConsolePrefix = "1.3.6.1.2.1.43.16.5.1.2.";

        private static readonly HashSet<string> Scalars = new()
        {
            "1.3.6.1.2.1.43.10.2.1.4.1.1", // compteur total
            "1.3.6.1.2.1.25.3.5.1.1.1",    // statut
            "1.3.6.1.2.1.43.5.1.1.17.1",   // numéro de série
            "1.3.6.1.2.1.1.3.0"            // uptime
        };

        private static string Norm(string? oid) => (oid ?? string.Empty).Trim().TrimStart('.');

        public static bool IsCoveredByAuto(string? oid)
        {
            var o = Norm(oid);
            return o.StartsWith(SupplyLevelPrefix) || o.StartsWith(InputLevelPrefix) || o.StartsWith(ConsolePrefix) || Scalars.Contains(o);
        }

        /// <summary>Clé de l'élément automatique correspondant à un OID de niveau standard, sinon null.</summary>
        public static string? KeyFor(string? oid)
        {
            var o = Norm(oid);
            if (o.StartsWith(SupplyLevelPrefix) && o.Length > SupplyLevelPrefix.Length) return "supply:" + o.Substring(SupplyLevelPrefix.Length);
            if (o.StartsWith(InputLevelPrefix) && o.Length > InputLevelPrefix.Length) return "input:" + o.Substring(InputLevelPrefix.Length);
            return null;
        }
    }

    public class SnmpProfileImportDto
    {
        public string Name { get; set; } = string.Empty;
        public bool IsColor { get; set; } = false;
        public string? OidTonerBlack { get; set; }
        public string? OidTonerCyan { get; set; }
        public string? OidTonerMagenta { get; set; }
        public string? OidTonerYellow { get; set; }
        public string? OidPageCounter { get; set; }
        public List<SnmpProfileItemImportDto>? Items { get; set; }
        // #50 : absents des exports antérieurs (profil alors converti comme au démarrage)
        public SnmpProfileMode? Mode { get; set; }
        public int? LowThresholdPercent { get; set; }
        public int? SupplyLeadTimeDays { get; set; }
        public List<SnmpProfileOverrideDto>? Overrides { get; set; }
    }

    public class SnmpProfileOverrideDto
    {
        public string Key { get; set; } = string.Empty;
        public string? DisplayName { get; set; }
        public bool Hidden { get; set; }
        public string? ColorHex { get; set; }
        public int? LowThresholdPercent { get; set; }
    }

    public class SnmpProfileItemImportDto
    {
        public SnmpItemCategory Category { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Oid { get; set; } = string.Empty;
        public string? OidMaxCapacity { get; set; }
        public SnmpValueType ValueType { get; set; }
        public string ColorHex { get; set; } = "#212529";
        public int SortOrder { get; set; }
    }
}

