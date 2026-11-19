using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace Autoprint.Shared
{
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

