using System.ComponentModel.DataAnnotations;

namespace Autoprint.Shared.DTOs
{
    // 1. Ce qu'on reçoit pour afficher la liste (Lecture seule)
    public class UserViewDto
    {
        public int Id { get; set; }
        public string Username { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public bool IsAdUser { get; set; }
        public bool IsActive { get; set; }
        public DateTime LastLogin { get; set; }
        public List<string> Roles { get; set; } = new();
    }

    // 2. Ce qu'on envoie pour créer (Formulaire Ajout)
    public class CreateUserDto
    {
        [Required(ErrorMessage = "Le nom d'utilisateur est requis")]
        public string Username { get; set; } = string.Empty;

        [Required(ErrorMessage = "Le mot de passe est requis")]
        [MinLength(6, ErrorMessage = "6 caractères minimum")]
        public string Password { get; set; } = string.Empty;

        [Required]
        public string DisplayName { get; set; } = string.Empty;

        public int RoleId { get; set; } // ID du rôle sélectionné
    }

    // 3. Ce qu'on envoie pour modifier (Formulaire Édition)
    public class UpdateUserDto
    {
        [Required]
        public string DisplayName { get; set; } = string.Empty;

        public bool IsActive { get; set; }

        public string? NewPassword { get; set; } // Optionnel (si vide, on change pas)

        public int RoleId { get; set; }
    }
}