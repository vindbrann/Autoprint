using Autoprint.Server.Data;
using Autoprint.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Autoprint.Server.Services;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using System;

namespace Autoprint.Server.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(Policy = "SETTINGS_MANAGE")]
    public class IntegrationTokensController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly AuditService _auditService;

        public IntegrationTokensController(ApplicationDbContext context, AuditService auditService)
        {
            _context = context;
            _auditService = auditService;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<IntegrationToken>>> GetIntegrationTokens()
        {
            var tokens = await _context.IntegrationTokens
                .AsNoTracking()
                .ToListAsync();

            foreach (var t in tokens)
            {
                t.TokenHash = "●●●●●●●● (Haché)";
            }

            return tokens;
        }

        [HttpPost]
        public async Task<ActionResult<IntegrationTokenCreationResultDto>> PostIntegrationToken(IntegrationTokenCreationDto dto)
        {
            var rawToken = $"ap_tok_{Guid.NewGuid().ToString("N")}";
            var tokenHash = HashToken(rawToken);

            var token = new IntegrationToken
            {
                TokenHash = tokenHash,
                Description = dto.Description,
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = dto.ExpiresAt
            };

            _context.IntegrationTokens.Add(token);

            _auditService.LogAction(
                "TOKEN_CREATE",
                $"Création jeton d'intégration : {dto.Description}",
                User.Identity?.Name,
                resourceName: dto.Description);

            await _context.SaveChangesAsync();

            return Ok(new IntegrationTokenCreationResultDto
            {
                Id = token.Id,
                RawToken = rawToken,
                Description = token.Description,
                CreatedAt = token.CreatedAt,
                ExpiresAt = token.ExpiresAt
            });
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteIntegrationToken(int id)
        {
            var token = await _context.IntegrationTokens.FindAsync(id);
            if (token == null) return NotFound();

            _context.IntegrationTokens.Remove(token);

            _auditService.LogAction(
                "TOKEN_DELETE",
                $"Suppression jeton d'intégration : {token.Description}",
                User.Identity?.Name,
                resourceName: token.Description);

            await _context.SaveChangesAsync();
            return NoContent();
        }

        private string HashToken(string token)
        {
            using var sha256 = SHA256.Create();
            var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(token));
            return Convert.ToHexString(bytes).ToLower();
        }
    }
}
