using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Autoprint.Shared.Enums;

namespace Autoprint.Server.Models.Security
{
    public class User
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string Username { get; set; } = string.Empty;

        public string DisplayName { get; set; } = string.Empty;

        public string? Email { get; set; }
        public DateTime? LastPasswordChangeDate { get; set; }

        public string? PasswordHash { get; set; }

        public bool IsAdUser { get; set; } = false;
        public bool IsActive { get; set; } = true;

        public DateTime LastLogin { get; set; }

        public List<UserRole> UserRoles { get; set; } = new();

        public bool ForceChangePassword { get; set; } = false;

        public string? PasswordResetToken { get; set; }
        public DateTime? ResetTokenExpires { get; set; }
    }

    public class Role
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string Name { get; set; } = string.Empty; 
        public string Description { get; set; } = string.Empty;

        public List<UserRole> UserRoles { get; set; } = new();
        public List<RolePermission> RolePermissions { get; set; } = new();
    }

    public class Permission
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string Code { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;

        public List<RolePermission> RolePermissions { get; set; } = new();
    }

    [Table("AdRoleMappings")]
    public class AdRoleMapping
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(100)]
        public string AdIdentifier { get; set; } = string.Empty;

        [Required]
        public AdMappingType MappingType { get; set; } = AdMappingType.Group;

        public int RoleId { get; set; }

        [ForeignKey("RoleId")]
        public Role Role { get; set; } = null!;
    }


    public class UserRole
    {
        public int UserId { get; set; }
        public User User { get; set; } = null!;
        public int RoleId { get; set; }
        public Role Role { get; set; } = null!;
    }

    public class RolePermission
    {
        public int RoleId { get; set; }
        public Role Role { get; set; } = null!;
        public int PermissionId { get; set; }
        public Permission Permission { get; set; } = null!;
    }
}