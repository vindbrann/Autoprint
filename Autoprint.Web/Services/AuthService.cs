using System.Net.Http.Json;
using Autoprint.Shared;
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

        // La signature correspond ici exactement à l'interface (Task<LoginResponse?>)
        public async Task<LoginResponse?> Login(LoginRequest request)
        {
            try
            {
                var result = await _httpClient.PostAsJsonAsync("api/auth/login", request);

                if (result.IsSuccessStatusCode)
                {
                    var response = await result.Content.ReadFromJsonAsync<LoginResponse>();

                    if (response != null)
                    {
                        await _localStorage.SetItemAsync("authToken", response.Token);
                        ((CustomAuthStateProvider)_authStateProvider).MarkUserAsAuthenticated(response.Token);
                        return response;
                    }
                }
            }
            catch
            {
                return null;
            }

            return null;
        }

        public async Task Logout()
        {
            await _localStorage.RemoveItemAsync("authToken");
            ((CustomAuthStateProvider)_authStateProvider).MarkUserAsLoggedOut();
            _httpClient.DefaultRequestHeaders.Authorization = null;
        }
    }
}