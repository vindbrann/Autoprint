using System.ComponentModel.DataAnnotations;

namespace Autoprint.Shared.DTOs
{
    /// <summary>Profil d'identifiants SNMP tel que vu par la console : jamais de secret, seulement leur présence.</summary>
    public class SnmpCredentialViewDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public int Version { get; set; } = 2;
        public string? V3Username { get; set; }
        public SnmpV3SecurityLevel V3SecurityLevel { get; set; }
        public SnmpAuthProtocol V3AuthProtocol { get; set; }
        public SnmpPrivProtocol V3PrivProtocol { get; set; }
        public bool HasCommunity { get; set; }
        public bool HasAuthPassword { get; set; }
        public bool HasPrivPassword { get; set; }
        public int ModelCount { get; set; }
        public int PrinterCount { get; set; }
    }

    /// <summary>Création / modification. Un secret vide signifie « conserver la valeur actuelle ».</summary>
    public class SnmpCredentialEditDto
    {
        [Required, MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        [MaxLength(255)]
        public string? Description { get; set; }

        [Range(1, 3)]
        public int Version { get; set; } = 2;

        [MaxLength(100)]
        public string? Community { get; set; }

        [MaxLength(100)]
        public string? V3Username { get; set; }
        public SnmpV3SecurityLevel V3SecurityLevel { get; set; } = SnmpV3SecurityLevel.AuthPriv;
        public SnmpAuthProtocol V3AuthProtocol { get; set; } = SnmpAuthProtocol.Sha256;
        [MaxLength(100)]
        public string? V3AuthPassword { get; set; }
        public SnmpPrivProtocol V3PrivProtocol { get; set; } = SnmpPrivProtocol.Aes128;
        [MaxLength(100)]
        public string? V3PrivPassword { get; set; }
    }
}
