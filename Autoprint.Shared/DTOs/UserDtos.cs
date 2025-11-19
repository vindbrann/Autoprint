using System.ComponentModel.DataAnnotations;

namespace Autoprint.Shared.DTOs // <--- IMPORTANT : C'est DTOs ici
{
    // --- AUTHENTIFICATION ---

    public class LoginRequest
    {
        [Required]
        public string Username { get; set; } = string.Empty;
        [Required]
        public string Password { get; set; } = string.Empty;
    }

    public class LoginResponse
    {
        public string Token { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public List<string> Permissions { get; set; } = new();

        // Indispensable pour la modal d'expiration !
        public bool PasswordExpired { get; set; }
    }

    public class ForgotPasswordRequest
    {
        public string EmailOrUsername { get; set; } = string.Empty;
    }

    // --- GESTION UTILISATEURS ---

    public class UserViewDto
    {
        public int Id { get; set; }
        public string Username { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string? Email { get; set; }
        public bool IsAdUser { get; set; }
        public bool IsActive { get; set; }
        public DateTime LastLogin { get; set; }
        public List<string> Roles { get; set; } = new();
    }

    public class CreateUserDto
    {
        [Required]
        public string Username { get; set; } = string.Empty;
        [Required, MinLength(6)]
        public string Password { get; set; } = string.Empty;
        [Required]
        public string DisplayName { get; set; } = string.Empty;
        [EmailAddress]
        public string? Email { get; set; }
        public int RoleId { get; set; }
    }

    public class UpdateUserDto
    {
        [Required]
        public string DisplayName { get; set; } = string.Empty;
        [EmailAddress]
        public string? Email { get; set; }
        public bool IsActive { get; set; }
        public string? NewPassword { get; set; }
        public int RoleId { get; set; }
        public bool ForceChangePassword { get; set; }
    }

    // --- PROFIL ---

    public class UserProfileDto
    {
        public string Username { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string? Email { get; set; }
        public string Role { get; set; } = string.Empty;
        public int DaysUntilExpiration { get; set; }
        public bool PasswordExpired { get; set; }
    }

    public class UpdateProfileDto
    {
        [Required]
        public string DisplayName { get; set; } = string.Empty;
        [EmailAddress]
        public string? Email { get; set; }
    }

    public class ChangePasswordDto
    {
        [Required]
        public string CurrentPassword { get; set; } = string.Empty;
        [Required, MinLength(6)]
        public string NewPassword { get; set; } = string.Empty;
        [Compare("NewPassword")]
        public string ConfirmPassword { get; set; } = string.Empty;
    }
    public class ResetPasswordDto
    {
        [Required]
        public string Token { get; set; } = string.Empty; // Le jeton reçu par mail

        [Required, MinLength(6, ErrorMessage = "6 caractères minimum")]
        public string NewPassword { get; set; } = string.Empty;

        [Compare("NewPassword", ErrorMessage = "La confirmation ne correspond pas")]
        public string ConfirmPassword { get; set; } = string.Empty;
    }
}