using System.Security.Cryptography;
using System.Text;
using Autoprint.Server.Data;
using Microsoft.EntityFrameworkCore;

namespace Autoprint.Server.Helpers
{
    /// <summary>
    /// Contrôle de la clé partagée des postes clients (en-tête X-Agent-Secret).
    /// La clé est lisible sur chaque poste : elle ne doit donner accès qu'aux données nécessaires au client (audit #11).
    /// </summary>
    public static class AgentAuth
    {
        public const string HeaderName = "X-Agent-Secret";

        public static async Task<bool> IsValidAgentRequestAsync(HttpRequest request, ApplicationDbContext context)
        {
            if (!request.Headers.TryGetValue(HeaderName, out var received) || string.IsNullOrEmpty(received)) return false;

            var settings = await context.ServerSettings
                .Where(s => s.Key == "AgentApiKey" || s.Key == "AgentApiKeyPrevious" || s.Key == "AgentApiKeyPreviousExpiresAt")
                .ToDictionaryAsync(s => s.Key, s => s.Value);
            var secrets = request.HttpContext.RequestServices.GetRequiredService<Autoprint.Server.Services.SecretProtector>();

            var receivedHash = SHA256.HashData(Encoding.UTF8.GetBytes(received.ToString()));

            // Clé actuelle
            var current = secrets.Unprotect(settings.GetValueOrDefault("AgentApiKey"));
            if (!string.IsNullOrEmpty(current) && CryptographicOperations.FixedTimeEquals(receivedHash, SHA256.HashData(Encoding.UTF8.GetBytes(current))))
                return true;

            // Ancienne clé, acceptée jusqu'à la fin de la période de transition (backlog B18)
            if (DateTime.TryParse(settings.GetValueOrDefault("AgentApiKeyPreviousExpiresAt"), null, System.Globalization.DateTimeStyles.RoundtripKind, out var until) && until > DateTime.UtcNow)
            {
                var previous = secrets.Unprotect(settings.GetValueOrDefault("AgentApiKeyPrevious"));
                if (!string.IsNullOrEmpty(previous) && CryptographicOperations.FixedTimeEquals(receivedHash, SHA256.HashData(Encoding.UTF8.GetBytes(previous))))
                    return true;
            }
            return false;
        }
    }
}
