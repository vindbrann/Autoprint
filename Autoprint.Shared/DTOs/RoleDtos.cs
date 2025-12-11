using System.ComponentModel.DataAnnotations;

namespace Autoprint.Shared.DTOs
{
    // 1. Pour afficher un Rôle dans la liste
    public class RoleViewDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;

        // La liste des codes de permission (ex: ["PRINTER_READ", "USER_MANAGE"])
        public List<string> PermissionCodes { get; set; } = new();
    }

    // 2. Pour afficher une Permission (la case à cocher)
    public class PermissionDto
    {
        public int Id { get; set; }
        public string Code { get; set; } = string.Empty; // ex: PRINTER_DELETE
        public string Description { get; set; } = string.Empty; // ex: "Supprimer des imprimantes"
    }

    // 3. Pour Créer ou Modifier un Rôle
    public class RoleEditDto
    {
        [Required]
        public string Name { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;

        // La liste des ID des permissions cochées
        public List<int> PermissionIds { get; set; } = new();
    }
}