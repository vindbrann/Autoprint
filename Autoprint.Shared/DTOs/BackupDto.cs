using Autoprint.Shared.Enums;

namespace Autoprint.Shared.DTOs
{
    public class BackupRootDto
    {
        public string Version { get; set; } = "1.0";
        public DateTime CreatedAt { get; set; }
        public string CreatedBy { get; set; } = "";

        // Les données
        public List<BackupMarqueDto> Marques { get; set; } = new();
        public List<BackupModeleDto> Modeles { get; set; } = new();
        public List<BackupLieuDto> Lieux { get; set; } = new();
        public List<BackupPiloteDto> Pilotes { get; set; } = new();
        public List<BackupImprimanteDto> Imprimantes { get; set; } = new();
        public List<BackupRoleDto> Roles { get; set; } = new();
        public List<BackupUserDto> Users { get; set; } = new();
        public List<BackupSettingDto> Settings { get; set; } = new();
        public List<BackupAdMappingDto> AdMappings { get; set; } = new();
    }

    public class BackupMarqueDto { public int Id { get; set; } public string Nom { get; set; } = ""; }
    public class BackupModeleDto { public int Id { get; set; } public string Nom { get; set; } = ""; public int MarqueId { get; set; } public int? PiloteId { get; set; } }
    public class BackupLieuDto { public int Id { get; set; } public string Nom { get; set; } = ""; public string Code { get; set; } = ""; public string Cidr { get; set; } = ""; }
    public class BackupPiloteDto { public int Id { get; set; } public string Nom { get; set; } = ""; public string Version { get; set; } = ""; public bool EstInstalle { get; set; } }

    public class BackupImprimanteDto
    {
        public int Id { get; set; }
        public string NomAffiche { get; set; } = "";
        public string AdresseIp { get; set; } = "";
        public string NomPartage { get; set; } = "";
        public bool EstPartagee { get; set; }
        public int ModeleId { get; set; }
        public int EmplacementId { get; set; }
        public PrinterStatus Status { get; set; }
    }

    public class BackupUserDto
    {
        public int Id { get; set; }
        public string Username { get; set; } = "";
        public string DisplayName { get; set; } = "";
        public string? Email { get; set; }
        public bool IsAdUser { get; set; }
        public bool IsActive { get; set; }
        public List<int> RoleIds { get; set; } = new();
    }

    public class BackupRoleDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public string Description { get; set; } = "";
        public List<int> PermissionIds { get; set; } = new();
    }

    public class BackupSettingDto { public string Key { get; set; } = ""; public string Value { get; set; } = ""; public string Type { get; set; } = ""; }
    public class BackupAdMappingDto { public string Identifier { get; set; } = ""; public int Type { get; set; } public int RoleId { get; set; } }
}