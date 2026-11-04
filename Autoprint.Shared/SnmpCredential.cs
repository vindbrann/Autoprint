using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Autoprint.Shared
{
    /// <summary>
    /// Profil d'identifiants SNMP (communauté v1 / v2c ou utilisateur v3).
    /// Rattaché à un modèle d'imprimante (valeur par défaut de toutes ses imprimantes), et remplaçable imprimante par imprimante.
    /// Les secrets (communauté, mots de passe v3) sont chiffrés en base par le serveur et ne sont jamais renvoyés à la console.
    /// </summary>
    public class SnmpCredential : BaseEntity
    {
        [Required, MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        [MaxLength(255)]
        public string? Description { get; set; }

        /// <summary>1 = v1, 2 = v2c, 3 = v3</summary>
        public int Version { get; set; } = 2;

        // v1 / v2c (chiffré)
        [JsonIgnore]
        public string? Community { get; set; }

        // v3
        [MaxLength(100)]
        public string? V3Username { get; set; }
        public SnmpV3SecurityLevel V3SecurityLevel { get; set; } = SnmpV3SecurityLevel.AuthPriv;
        public SnmpAuthProtocol V3AuthProtocol { get; set; } = SnmpAuthProtocol.Sha256;
        [JsonIgnore]
        public string? V3AuthPassword { get; set; }
        public SnmpPrivProtocol V3PrivProtocol { get; set; } = SnmpPrivProtocol.Aes128;
        [JsonIgnore]
        public string? V3PrivPassword { get; set; }
    }

    public enum SnmpV3SecurityLevel
    {
        NoAuthNoPriv = 0,
        AuthNoPriv = 1,
        AuthPriv = 2
    }

    public enum SnmpAuthProtocol
    {
        Sha1 = 0,
        Sha256 = 1,
        Sha384 = 2,
        Sha512 = 3,
        Md5 = 4 // déconseillé, pour les anciens matériels uniquement
    }

    public enum SnmpPrivProtocol
    {
        Aes128 = 0,
        Aes192 = 1,
        Aes256 = 2,
        TripleDes = 3 // déconseillé, pour les anciens matériels uniquement
    }
}
