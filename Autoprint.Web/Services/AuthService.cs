using System.Net.Http.Json;
using Autoprint.Shared.DTOs;
using Blazored.LocalStorage;
using Microsoft.AspNetCore.Components.Authorization;

namespace Autoprint.Web.Services
{
    public class AuthService : IAuthService
    {
        private readonly HttpClient _httpClient;
        private readonly AuthenticationStateProvider _authStateProvider;
        private readonly ILocalStorageService _localStorage;

        public AuthService(HttpClient httpClient,
                           AuthenticationStateProvider authStateProvider,
                           ILocalStorageService localStorage)
        {
            _httpClient = httpClient;
            _authStateProvider = authStateProvider;
            _localStorage = localStorage;
        }

        public async Task<LoginResponse> Login(LoginRequest loginRequest)
        {
            var response = await _httpClient.PostAsJsonAsync("api/auth/login", loginRequest);

            if (!response.IsSuccessStatusCode)
            {
                // C'EST ICI QUE ÇA SE JOUE : On lit le message du serveur
                // Le serveur renvoie "Identifiants incorrects" (401) OU "Connexion réussie mais..." (403)
                var serverMessage = await response.Content.ReadAsStringAsync();

                // Si le message est vide ou technique (JSON d'erreur par défaut), on met un message générique
                if (string.IsNullOrWhiteSpace(serverMessage) || serverMessage.StartsWith("{"))
                {
                    throw new Exception("Échec de la connexion (Erreur serveur).");
                }

                // Sinon on renvoie le message précis (ex: "Connexion réussie, mais vous n'avez aucun droit...")
                throw new Exception(serverMessage);
            }

            // Si succès, on traite le token comme d'habitude
            var result = await response.Content.ReadFromJsonAsync<LoginResponse>();
            await _localStorage.SetItemAsync("authToken", result!.Token);
            ((CustomAuthStateProvider)_authStateProvider).MarkUserAsAuthenticated(result.Token);

            return result;
        }

        public async Task Logout()
        {
            await _localStorage.RemoveItemAsync("authToken");
            ((CustomAuthStateProvider)_authStateProvider).MarkUserAsLoggedOut();
            _httpClient.DefaultRequestHeaders.Authorization = null;
        }
    }
}