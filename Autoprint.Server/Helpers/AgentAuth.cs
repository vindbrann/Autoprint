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

            var expected = await context.ServerSettings
                .Where(s => s.Key == "AgentApiKey")
                .Select(s => s.Value)
                .FirstOrDefaultAsync();
            if (string.IsNullOrEmpty(expected)) return false;

            // Comparaison en temps constant (les empreintes ont toujours la même longueur)
            var receivedHash = SHA256.HashData(Encoding.UTF8.GetBytes(received.ToString()));
            var expectedHash = SHA256.HashData(Encoding.UTF8.GetBytes(expected));
            return CryptographicOperations.FixedTimeEquals(receivedHash, expectedHash);
        }
    }
}
