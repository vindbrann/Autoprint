using Autoprint.Server.Services;
using Autoprint.Shared.DTOs;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Autoprint.Server.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _authService;
        private readonly LoginThrottleService _throttle;
        private readonly ILogger<AuthController> _logger;

        public AuthController(IAuthService authService, LoginThrottleService throttle, ILogger<AuthController> logger)
        {
            _authService = authService;
            _throttle = throttle;
            _logger = logger;
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

        [HttpPost("forgot-password")]
        [EnableRateLimiting("login")]
        public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest request)
        {
            return Ok();
        }
    }
}
