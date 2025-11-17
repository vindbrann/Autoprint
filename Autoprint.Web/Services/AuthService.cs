using System.Net.Http.Json;
using Autoprint.Shared;
using Blazored.LocalStorage;
using Microsoft.AspNetCore.Components.Authorization;
using System.Net.Http.Headers;

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
            var result = await _httpClient.PostAsJsonAsync("api/auth/login", loginRequest);

            if (!result.IsSuccessStatusCode)
            {
                return new LoginResponse { Token = string.Empty };
            }

            var response = await result.Content.ReadFromJsonAsync<LoginResponse>();

            if (response != null && !string.IsNullOrEmpty(response.Token))
            {
                // 1. Stockage
                await _localStorage.SetItemAsync("authToken", response.Token);

                // 2. Notification immédiate au système (Le FIX est ici)
                ((CustomAuthStateProvider)_authStateProvider).NotifyUserAuthentication(response.Token);

                // 3. Header HTTP
                _httpClient.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", response.Token);
            }

            return response!;
        }

        public async Task Logout()
        {
            await _localStorage.RemoveItemAsync("authToken");

            // Notification de déconnexion
            ((CustomAuthStateProvider)_authStateProvider).NotifyUserLogout();

            _httpClient.DefaultRequestHeaders.Authorization = null;
        }
    }
}