using System.Security.Claims;
using System.Text.Json;
using System.Net.Http.Headers;
using Blazored.LocalStorage;
using Microsoft.AspNetCore.Components.Authorization;
using System.Timers;

namespace Autoprint.Web.Services
{
    public class CustomAuthStateProvider : AuthenticationStateProvider, IDisposable
    {
        private readonly ILocalStorageService _localStorage;
        private readonly HttpClient _http;

        private System.Timers.Timer? _authTimer;

        public CustomAuthStateProvider(ILocalStorageService localStorage, HttpClient http)
        {
            _localStorage = localStorage;
            _http = http;
        }

        public override async Task<AuthenticationState> GetAuthenticationStateAsync()
        {
            var token = await _localStorage.GetItemAsync<string>("authToken");

            if (string.IsNullOrWhiteSpace(token))
            {
                return new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity()));
            }
            var claims = ParseClaimsFromJwt(token);
            var expClaim = claims.FirstOrDefault(c => c.Type == "exp");

            if (expClaim != null && long.TryParse(expClaim.Value, out long exp))
            {
                var expDate = DateTimeOffset.FromUnixTimeSeconds(exp);
                if (expDate <= DateTimeOffset.UtcNow)
                {
                    await _localStorage.RemoveItemAsync("authToken");
                    return new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity()));
                }

                StartAuthTimer(expDate);
            }

            _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("bearer", token);
            return new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity(claims, "jwt")));
        }

        public void MarkUserAsAuthenticated(string token)
        {
            var claims = ParseClaimsFromJwt(token);
            var authenticatedUser = new ClaimsPrincipal(new ClaimsIdentity(claims, "jwt"));
            var authState = Task.FromResult(new AuthenticationState(authenticatedUser));

            _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("bearer", token);
            var expClaim = claims.FirstOrDefault(c => c.Type == "exp");
            if (expClaim != null && long.TryParse(expClaim.Value, out long exp))
            {
                StartAuthTimer(DateTimeOffset.FromUnixTimeSeconds(exp));
            }

            NotifyAuthenticationStateChanged(authState);
        }

        public void MarkUserAsLoggedOut()
        {
            if (_authTimer != null)
            {
                _authTimer.Stop();
                _authTimer.Dispose();
                _authTimer = null;
            }

            var anonymousUser = new ClaimsPrincipal(new ClaimsIdentity());
            var authState = Task.FromResult(new AuthenticationState(anonymousUser));

            _http.DefaultRequestHeaders.Authorization = null;

            NotifyAuthenticationStateChanged(authState);
        }

        private void StartAuthTimer(DateTimeOffset expDate)
        {
            var timeUntilExpiry = expDate - DateTimeOffset.UtcNow;
            if (_authTimer != null)
            {
                _authTimer.Stop();
                _authTimer.Dispose();
            }

            if (timeUntilExpiry.TotalMilliseconds > 0)
            {
                _authTimer = new System.Timers.Timer(timeUntilExpiry.TotalMilliseconds);
                _authTimer.AutoReset = false;
                _authTimer.Elapsed += (sender, e) =>
                {
                    Console.WriteLine("Session expirée : Déconnexion automatique.");
                    MarkUserAsLoggedOut();
                };
                _authTimer.Start();
            }
            else
            {
                MarkUserAsLoggedOut();
            }
        }

        public async Task<bool> HasPermission(string claimType, string claimValue)
        {
            var authState = await GetAuthenticationStateAsync();
            var user = authState.User;
            if (!user.Identity?.IsAuthenticated ?? true) return false;
            return user.HasClaim(claimType, claimValue);
        }

        private IEnumerable<Claim> ParseClaimsFromJwt(string jwt)
        {
            var claims = new List<Claim>();
            var payload = jwt.Split('.')[1];
            var jsonBytes = ParseBase64WithoutPadding(payload);
            var keyValuePairs = JsonSerializer.Deserialize<Dictionary<string, object>>(jsonBytes);

            if (keyValuePairs == null) return claims;

            foreach (var kvp in keyValuePairs)
            {
                if (kvp.Value is JsonElement element && element.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in element.EnumerateArray()) claims.Add(new Claim(kvp.Key, item.ToString()));
                }
                else
                {
                    claims.Add(new Claim(kvp.Key, kvp.Value.ToString()!));
                }
            }
            return claims;
        }

        private byte[] ParseBase64WithoutPadding(string base64)
        {
            switch (base64.Length % 4)
            {
                case 2: base64 += "=="; break;
                case 3: base64 += "="; break;
            }
            return Convert.FromBase64String(base64);
        }

        public void Dispose()
        {
            _authTimer?.Dispose();
        }
    }
}