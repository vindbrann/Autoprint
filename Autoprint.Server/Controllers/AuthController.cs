using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Autoprint.Shared;
using Autoprint.Server.Services;
using System.Security.Claims;

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

        // POST: api/auth/login
        [HttpPost("login")]
        public async Task<ActionResult<LoginResponse>> Login([FromBody] LoginRequest request)
        {
            // 1. On demande au Service de vérifier l'identité
            var response = await _authService.LoginAsync(request);

            if (response == null)
            {
                return Unauthorized(new { message = "Nom d'utilisateur ou mot de passe incorrect." });
            }

            // 2. Si c'est bon, on renvoie le Token
            return Ok(response);
        }

        // GET: api/auth/me
        // Cette route sert juste à vérifier que le token fonctionne.
        // Seul un utilisateur connecté (avec un Token valide) peut l'appeler.
        [Authorize]
        [HttpGet("me")]
        public IActionResult GetMyInfo()
        {
            // On lit les infos cachées dans le "Badge" (Claims)
            var username = User.Identity?.Name;
            var roles = User.FindAll("Permission").Select(c => c.Value).ToList();

            return Ok(new
            {
                Message = "Connexion réussie !",
                User = username,
                Permissions = roles
            });
        }
    }
}