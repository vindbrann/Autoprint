using System.Security.Claims;
using System.Text.Json;
using Blazored.LocalStorage;
using Microsoft.AspNetCore.Components.Authorization;
using System.Net.Http.Headers;

namespace Autoprint.Web.Services
{
    public class CustomAuthStateProvider : AuthenticationStateProvider
    {
        private readonly ILocalStorageService _localStorage;
        private readonly HttpClient _http;

        public CustomAuthStateProvider(ILocalStorageService localStorage, HttpClient http)
        {
            _localStorage = localStorage;
            _http = http;
        }

        public override async Task<AuthenticationState> GetAuthenticationStateAsync()
        {
            string? token = await _localStorage.GetItemAsStringAsync("authToken");

            var identity = new ClaimsIdentity();
            _http.DefaultRequestHeaders.Authorization = null;

            if (!string.IsNullOrEmpty(token))
            {
                try
                {
                    identity = new ClaimsIdentity(ParseClaimsFromJwt(token), "jwt");
                    _http.DefaultRequestHeaders.Authorization =
                        new AuthenticationHeaderValue("Bearer", token.Replace("\"", ""));
                }
                catch
                {
                    await _localStorage.RemoveItemAsync("authToken");
                    identity = new ClaimsIdentity();
                }
            }

            var user = new ClaimsPrincipal(identity);
            var state = new AuthenticationState(user);

            // Important : On ne notifie PAS ici pour éviter les boucles
            return state;
        }

        // --- NOUVELLES MÉTHODES POUR FORCER LA MAJ ---

        public void NotifyUserAuthentication(string token)
        {
            var authenticatedUser = new ClaimsPrincipal(new ClaimsIdentity(ParseClaimsFromJwt(token), "jwt"));
            var authState = Task.FromResult(new AuthenticationState(authenticatedUser));
            NotifyAuthenticationStateChanged(authState);
        }

        public void NotifyUserLogout()
        {
            var anonymousUser = new ClaimsPrincipal(new ClaimsIdentity());
            var authState = Task.FromResult(new AuthenticationState(anonymousUser));
            NotifyAuthenticationStateChanged(authState);
        }

        // --- CORRECTION DU PARSING POUR AFFICHER LE NOM ---
        public static IEnumerable<Claim> ParseClaimsFromJwt(string jwt)
        {
            var payload = jwt.Split('.')[1];
            var jsonBytes = ParseBase64WithoutPadding(payload);
            var keyValuePairs = JsonSerializer.Deserialize<Dictionary<string, object>>(jsonBytes);
            var claims = new List<Claim>();

            if (keyValuePairs == null) return claims;

            foreach (var kvp in keyValuePairs)
            {
                var value = kvp.Value.ToString() ?? "";

                // 1. Gestion des rôles multiples (Array)
                if (kvp.Value is JsonElement element && element.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in element.EnumerateArray())
                    {
                        claims.Add(new Claim(kvp.Key, item.ToString()));
                    }
                }
                else
                {
                    // 2. Ajouter la claim brute
                    claims.Add(new Claim(kvp.Key, value));

                    // 3. MAPPING MAGIQUE (Le Fix est ici)
                    // Si la clé est "unique_name" (standard JWT pour le nom), 
                    // on l'ajoute aussi comme ClaimTypes.Name pour que Blazor le comprenne.
                    if (kvp.Key == "unique_name" || kvp.Key == "name")
                    {
                        claims.Add(new Claim(ClaimTypes.Name, value));
                    }
                }
            }
            return claims;
        }

        private static byte[] ParseBase64WithoutPadding(string base64)
        {
            switch (base64.Length % 4)
            {
                case 2: base64 += "=="; break;
                case 3: base64 += "="; break;
            }
            return Convert.FromBase64String(base64);
        }
    }
}