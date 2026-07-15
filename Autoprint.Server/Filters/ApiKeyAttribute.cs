using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Threading.Tasks;
using Autoprint.Server.Data;
using Microsoft.EntityFrameworkCore;

namespace Autoprint.Server.Filters
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
    public class ApiKeyAttribute : Attribute, IAsyncActionFilter
    {
        private const string ApiKeyHeaderName = "X-Api-Token";

        public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            if (!context.HttpContext.Request.Headers.TryGetValue(ApiKeyHeaderName, out var extractedApiKey))
            {
                context.Result = new ContentResult()
                {
                    StatusCode = 401,
                    Content = "En-tête X-Api-Token manquant."
                };
                return;
            }

            var dbContext = context.HttpContext.RequestServices.GetRequiredService<ApplicationDbContext>();
            
            var tokenStr = extractedApiKey.ToString();
            var tokenHash = HashToken(tokenStr);

            var tokenEntity = await dbContext.IntegrationTokens
                .FirstOrDefaultAsync(t => t.TokenHash == tokenHash);

            if (tokenEntity == null || (tokenEntity.ExpiresAt != null && tokenEntity.ExpiresAt < DateTime.UtcNow))
            {
                context.Result = new ContentResult()
                {
                    StatusCode = 401,
                    Content = "Jeton d'intégration invalide ou expiré."
                };
                return;
            }

            await next();
        }

        private string HashToken(string token)
        {
            using var sha256 = System.Security.Cryptography.SHA256.Create();
            var bytes = sha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes(token));
            return Convert.ToHexString(bytes).ToLower();
        }
    }
}
