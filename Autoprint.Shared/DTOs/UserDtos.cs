using System.ComponentModel.DataAnnotations;

namespace Autoprint.Shared.DTOs
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
        public bool ForceChangePassword { get; set; }
        public DateTime LastLogin { get; set; }
        public List<string> Roles { get; set; } = new();
    }

    public class CreateUserDto
    {
        [Required]
        public string Username { get; set; } = string.Empty;
        [Required, MinLength(PasswordPolicy.MinLength, ErrorMessage = PasswordPolicy.MinLengthMessage)]
        public string Password { get; set; } = string.Empty;
        [Required]
        public string DisplayName { get; set; } = string.Empty;
        [EmailAddress]
        public string? Email { get; set; }
        public int RoleId { get; set; }
        // Coché par défaut : le mot de passe choisi par l'administrateur est provisoire (backlog B6)
        public bool ForceChangePassword { get; set; } = true;
    }

    public class UpdateUserDto
    {
        [Required]
        public string DisplayName { get; set; } = string.Empty;
        [EmailAddress]
        public string? Email { get; set; }
        public bool IsActive { get; set; }
        [MinLength(PasswordPolicy.MinLength, ErrorMessage = PasswordPolicy.MinLengthMessage)]
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
        public bool IsAdUser { get; set; }
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
        [Required, MinLength(PasswordPolicy.MinLength, ErrorMessage = PasswordPolicy.MinLengthMessage)]
        public string NewPassword { get; set; } = string.Empty;
        [Compare("NewPassword")]
        public string ConfirmPassword { get; set; } = string.Empty;
    }
    public class ResetPasswordDto
    {
        [Required, MaxLength(128)]
        public string Token { get; set; } = string.Empty; // Le jeton reçu par mail

        [Required, MinLength(PasswordPolicy.MinLength, ErrorMessage = PasswordPolicy.MinLengthMessage)]
        public string NewPassword { get; set; } = string.Empty;

        [Compare("NewPassword", ErrorMessage = "La confirmation ne correspond pas")]
        public string ConfirmPassword { get; set; } = string.Empty;
    }
}