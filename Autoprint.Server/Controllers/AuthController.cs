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
                    return Unauthorized("Identifiants incorrects.");
                }

                return Ok(result);
            }
            catch (UnauthorizedAccessException)
            {
                return StatusCode(403, "Connexion réussie, mais vous n'avez aucun droit d'accès à l'application. Contactez l'administrateur.");
            }
            catch (Exception ex)
            {
                return StatusCode(500, "Erreur serveur : " + ex.Message);
            }
        }

        [HttpPost("forgot-password")]
        public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest request)
        {
            return Ok();
        }
    }
}