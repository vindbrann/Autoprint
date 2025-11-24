using Autoprint.Server.Services;
using Autoprint.Shared.DTOs;
using Microsoft.AspNetCore.Mvc;

namespace Autoprint.Server.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _authService;

        public AuthController(IAuthService authService)
        {
            _authService = authService;
        }

        [HttpPost("login")]
        public async Task<ActionResult<LoginResponse>> Login([FromBody] LoginRequest request)
        {
            try
            {
                var result = await _authService.LoginAsync(request);

                if (result == null)
                {
                    // Cas 1 : Identifiants incorrects
                    return Unauthorized("Identifiants incorrects.");
                }

                return Ok(result);
            }
            catch (UnauthorizedAccessException)
            {
                // Cas 2 : Identifiants OK, mais pas de rôle (Le "Throw" de AuthService)
                return StatusCode(403, "Connexion réussie, mais vous n'avez aucun droit d'accès à l'application. Contactez l'administrateur.");
            }
            catch (Exception ex)
            {
                // Cas 3 : Erreur technique (AD Down, SQL HS...)
                return StatusCode(500, "Erreur serveur : " + ex.Message);
            }
        }

        [HttpPost("forgot-password")]
        public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest request)
        {
            // Implémentation future si besoin (Actuellement géré par UserService/Mail)
            return Ok();
        }
    }
}