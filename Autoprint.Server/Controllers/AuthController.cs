using Autoprint.Server.Helpers;
using Autoprint.Server.Services;
using Autoprint.Shared.DTOs;
using Autoprint.Server.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Autoprint.Server.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _authService;
        private readonly ApplicationDbContext _context;
        private readonly IEmailService _emailService;
        private readonly IConfiguration _configuration; // Pour lire l'URL

        public AuthController(IAuthService authService, ApplicationDbContext context, IEmailService emailService, IConfiguration configuration)
        {
            _authService = authService;
            _context = context;
            _emailService = emailService;
            _configuration = configuration;
        }

        // POST: api/auth/login
        [HttpPost("login")]
        public async Task<ActionResult<LoginResponse>> Login([FromBody] LoginRequest request)
        {
            var response = await _authService.LoginAsync(request);
            if (response == null) return Unauthorized(new { message = "Identifiants incorrects." });
            return Ok(response);
        }

        // GET: api/auth/me
        [Authorize]
        [HttpGet("me")]
        public IActionResult GetMyInfo()
        {
            return Ok(new { User = User.Identity?.Name, Permissions = User.FindAll("Permission").Select(c => c.Value) });
        }

        // POST: api/auth/forgot-password
        [HttpPost("forgot-password")]
        [AllowAnonymous]
        public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.EmailOrUsername)) return BadRequest("Requis.");

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Username == request.EmailOrUsername || u.Email == request.EmailOrUsername);

            if (user == null || string.IsNullOrEmpty(user.Email) || user.IsAdUser)
            {
                await Task.Delay(1000);
                return Ok(new { message = "Email envoyé." });
            }

            string token = Guid.NewGuid().ToString("N");
            user.PasswordResetToken = token;
            user.ResetTokenExpires = DateTime.UtcNow.AddMinutes(15);
            await _context.SaveChangesAsync();

            // --- CORRECTION URL DYNAMIQUE ---
            // On lit l'URL depuis appsettings.json
            string baseUrl = _configuration["ClientUrl"] ?? "https://localhost:7169";
            string resetLink = $"{baseUrl}/reset-password?token={token}";
            // --------------------------------

            string body = $@"
                <div style='font-family:sans-serif; padding:20px;'>
                    <h3>Réinitialisation de sécurité</h3>
                    <p>Cliquez sur le lien ci-dessous pour définir votre nouveau mot de passe :</p>
                    <p><a href='{resetLink}' style='background:#0d6efd; color:white; padding:10px 15px; text-decoration:none; border-radius:5px;'>Définir mon nouveau mot de passe</a></p>
                    <small>Ce lien expire dans 15 minutes.</small>
                </div>";

            try { await _emailService.SendEmailAsync(user.Email, "[Autoprint] Lien de réinitialisation", body); }
            catch { /* Log */ }

            return Ok(new { message = "Email envoyé." });
        }

        // POST: api/auth/reset-password
        [HttpPost("reset-password")]
        [AllowAnonymous]
        public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordDto request)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.PasswordResetToken == request.Token);

            if (user == null || user.ResetTokenExpires < DateTime.UtcNow)
            {
                return BadRequest("Ce lien est invalide ou a expiré.");
            }

            // Application du nouveau mot de passe
            user.PasswordHash = SecurityHelper.ComputeSha256Hash(request.NewPassword);

            // --- NETTOYAGE COMPLET ---
            user.PasswordResetToken = null;
            user.ResetTokenExpires = null;

            // IMPORTANT : On déverrouille l'utilisateur
            user.ForceChangePassword = false;

            // IMPORTANT : On met la date à "Maintenant" pour éviter l'expiration immédiate
            user.LastPasswordChangeDate = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            return Ok(new { message = "Mot de passe modifié avec succès." });
        }
    }
}