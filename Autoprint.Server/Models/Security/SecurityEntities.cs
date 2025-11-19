using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

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

        // Si local : contient le Hash. Si AD : vide ou null.
        public string? PasswordHash { get; set; }

        public bool IsAdUser { get; set; } = false;
        public bool IsActive { get; set; } = true;

        public DateTime LastLogin { get; set; }

        // Relation : Un user a plusieurs rôles (groupes)
        public List<UserRole> UserRoles { get; set; } = new();

        public bool ForceChangePassword { get; set; } = false;

        public string? PasswordResetToken { get; set; }
        public DateTime? ResetTokenExpires { get; set; }
    }

    // 2. Le Rôle (Tes "Groupes" : Admin, Support, Stagiaire...)
    public class Role
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string Name { get; set; } = string.Empty; // ex: "Administrateurs"
        public string Description { get; set; } = string.Empty;

        public List<UserRole> UserRoles { get; set; } = new();
        public List<RolePermission> RolePermissions { get; set; } = new();
    }

    // 3. La Permission (L'action atomique : PRINTER_DELETE, SETTINGS_VIEW...)
    public class Permission
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string Code { get; set; } = string.Empty; // Le code testé dans le Controller
        public string Description { get; set; } = string.Empty; // Pour l'affichage UI

        public List<RolePermission> RolePermissions { get; set; } = new();
    }

    // 4. Mapping AD (Lien automatique : Groupe AD -> Rôle Autoprint)
    public class AdRoleMapping
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string AdGroupName { get; set; } = string.Empty; // ex: "GL_Autoprint_Admins"

        public int RoleId { get; set; } // ex: 1 (Admin)

        [ForeignKey("RoleId")]
        public Role Role { get; set; } = null!;
    }

    // --- Tables de liaison (Many-to-Many) ---

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