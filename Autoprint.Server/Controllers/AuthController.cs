using System.Security.Cryptography;
using System.Text;
using Autoprint.Server.Data;
using Autoprint.Server.Helpers;
using Autoprint.Shared;
using Autoprint.Server.Services;
using Autoprint.Shared.DTOs;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Autoprint.Server.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private static readonly TimeSpan ResetTokenLifetime = TimeSpan.FromMinutes(30);

        private readonly IAuthService _authService;
        private readonly LoginThrottleService _throttle;
        private readonly ILogger<AuthController> _logger;
        private readonly ApplicationDbContext _context;
        private readonly IEmailService _emailService;
        private readonly AuditService _auditService;
        private readonly IConfiguration _configuration;

        public AuthController(IAuthService authService, LoginThrottleService throttle, ILogger<AuthController> logger,
                              ApplicationDbContext context, IEmailService emailService, AuditService auditService, IConfiguration configuration)
        {
            _authService = authService;
            _throttle = throttle;
            _logger = logger;
            _context = context;
            _emailService = emailService;
            _auditService = auditService;
            _configuration = configuration;
        }

        [HttpPost("login")]
        [EnableRateLimiting("login")]
        public async Task<ActionResult<LoginResponse>> Login([FromBody] LoginRequest request)
        {
            if (_throttle.IsLocked(request.Username, out var remaining))
            {
                int minutes = Math.Max(1, (int)Math.Ceiling(remaining.TotalMinutes));
                return StatusCode(429, $"Trop de tentatives pour ce compte. Réessayez dans {minutes} minute(s).");
            }

            try
            {
                var result = await _authService.LoginAsync(request);

                if (result == null)
                {
                    _throttle.RegisterFailure(request.Username);
                    return Unauthorized("Identifiants incorrects.");
                }

                _throttle.RegisterSuccess(request.Username);
                return Ok(result);
            }
            catch (DirectoryUnavailableException ex)
            {
                _logger.LogWarning(ex.InnerException, "Connexion AD refusée : annuaire injoignable");
                return StatusCode(503, ex.Message);
            }
            catch (UnauthorizedAccessException)
            {
                return StatusCode(403, "Connexion réussie, mais vous n'avez aucun droit d'accès à l'application. Contactez l'administrateur.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur lors de la connexion");
                return StatusCode(500, "Erreur serveur lors de la connexion.");
            }
        }

        // Renouvellement silencieux par la console avant expiration (backlog B19).
        // Le jeton présenté est déjà validé (signature, expiration, empreinte de sécurité, compte actif).
        [HttpPost("refresh")]
        [Microsoft.AspNetCore.Authorization.Authorize]
        public async Task<ActionResult<LoginResponse>> Refresh()
        {
            if (!int.TryParse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value, out int userId)
                || !long.TryParse(User.FindFirst("auth_time")?.Value, out long authTime))
                return Unauthorized("Session à renouveler : reconnectez-vous.");

            var result = await _authService.RefreshAsync(userId, authTime);
            return result == null ? Unauthorized("Session expirée : reconnectez-vous.") : Ok(result);
        }

        // Réponse toujours identique : on ne révèle pas si le compte existe (backlog B5)
        private const string ForgotPasswordGenericAnswer =
            "Si un compte local avec une adresse e-mail correspond, un lien de réinitialisation valable 30 minutes vient d'être envoyé.";

        [HttpPost("forgot-password")]
        [EnableRateLimiting("login")]
        public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest request)
        {
            var key = (request?.EmailOrUsername ?? "").Trim();
            if (key.Length == 0 || key.Length > 256) return Ok(new { message = ForgotPasswordGenericAnswer });

            try
            {
                var user = await _context.Users.FirstOrDefaultAsync(u =>
                    !u.IsAdUser && u.IsActive && u.Email != null && u.Email != "" &&
                    (u.Username == key || u.Email == key));

                // Comptes AD : mot de passe géré par Windows. Une demande en cours non expirée n'est pas renvoyée (anti-spam).
                if (user == null || (user.ResetTokenExpires != null && user.ResetTokenExpires > DateTime.UtcNow.AddMinutes(25)))
                    return Ok(new { message = ForgotPasswordGenericAnswer });

                var baseUrl = GetPublicBaseUrl();
                if (baseUrl == null)
                {
                    _logger.LogWarning("Réinitialisation de mot de passe impossible : nom public de la console non configuré (AllowedHosts).");
                    return Ok(new { message = ForgotPasswordGenericAnswer });
                }

                var token = Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
                user.PasswordResetToken = HashToken(token);
                user.ResetTokenExpires = DateTime.UtcNow.Add(ResetTokenLifetime);
                await _context.SaveChangesAsync();

                var link = $"{baseUrl}/reset-password?token={token}";
                var html =
                    $"<p>Bonjour {System.Net.WebUtility.HtmlEncode(user.DisplayName)},</p>" +
                    "<p>Une réinitialisation du mot de passe de votre compte Autoprint a été demandée.</p>" +
                    $"<p><a href=\"{link}\">Choisir un nouveau mot de passe</a> (lien valable 30 minutes, utilisable une seule fois).</p>" +
                    "<p>Si vous n'êtes pas à l'origine de cette demande, ignorez ce message : votre mot de passe actuel reste valable.</p>";
                await _emailService.SendEmailAsync(user.Email!, "Autoprint : réinitialisation du mot de passe", html);

                _auditService.LogAction("USER_PWD_RESET_REQUEST", "Demande de réinitialisation du mot de passe par e-mail", user.Username, "WARNING", resourceName: user.Username);
                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Échec de l'envoi du lien de réinitialisation");
            }

            return Ok(new { message = ForgotPasswordGenericAnswer });
        }

        [HttpPost("reset-password")]
        [EnableRateLimiting("login")]
        public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordDto request)
        {
            const string invalidLink = "Lien de réinitialisation invalide ou expiré. Faites une nouvelle demande.";
            if (string.IsNullOrWhiteSpace(request.Token)) return BadRequest(invalidLink);

            var hash = HashToken(request.Token.Trim());
            var user = await _context.Users.FirstOrDefaultAsync(u => u.PasswordResetToken == hash);
            if (user == null || user.IsAdUser || !user.IsActive || user.ResetTokenExpires == null || user.ResetTokenExpires < DateTime.UtcNow)
                return BadRequest(invalidLink);

            if (string.Equals(request.NewPassword, user.Username, StringComparison.OrdinalIgnoreCase))
                return BadRequest("Le mot de passe ne peut pas être identique au nom d'utilisateur.");

            user.PasswordHash = SecurityHelper.HashPassword(request.NewPassword);
            user.LastPasswordChangeDate = DateTime.UtcNow;
            user.ForceChangePassword = false;
            // Lien à usage unique, et toutes les sessions ouvertes sont coupées
            user.PasswordResetToken = null;
            user.ResetTokenExpires = null;
            user.RenewSecurityStamp();

            _auditService.LogAction("USER_PWD_RESET", "Mot de passe réinitialisé par lien e-mail", user.Username, "WARNING", resourceName: user.Username);
            await _context.SaveChangesAsync();
            _throttle.RegisterSuccess(user.Username);

            return Ok(new { message = "Mot de passe modifié. Vous pouvez vous connecter." });
        }

        // Le lien est construit à partir du nom public configuré, jamais de l'en-tête Host de la requête
        // (sinon un attaquant pourrait faire envoyer un lien vers son propre site)
        private string? GetPublicBaseUrl()
        {
            var hosts = (_configuration["AllowedHosts"] ?? "").Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var publicHost = hosts.FirstOrDefault(h => h != "*" && !h.Equals("localhost", StringComparison.OrdinalIgnoreCase) && !h.StartsWith("*."));
            return publicHost == null ? null : $"https://{publicHost}";
        }

        private static string HashToken(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token))).ToLowerInvariant();

        private static string Base64UrlEncode(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }
}
