using System.Net.Http.Json;
using Autoprint.Shared.DTOs;

namespace Autoprint.Web.Services
{
    public interface IUserService
    {
        Task<List<UserViewDto>> GetUsers();
        Task CreateUser(CreateUserDto user);
        Task UpdateUser(int id, UpdateUserDto user);
        Task DeleteUser(int id);
    }

    public class UserService : IUserService
    {
        private readonly HttpClient _http;

        public UserService(HttpClient http)
        {
            _http = http;
        }

        public async Task<List<UserViewDto>> GetUsers()
        {
            return await _http.GetFromJsonAsync<List<UserViewDto>>("api/users")
                    ?? new List<UserViewDto>();
        }

        public async Task CreateUser(CreateUserDto user)
        {
            var response = await _http.PostAsJsonAsync("api/users", user);

            if (!response.IsSuccessStatusCode)
            {
                var errorMsg = await response.Content.ReadAsStringAsync();
                throw new Exception($"Erreur Création : {errorMsg}");
            }
        }

        public async Task UpdateUser(int id, UpdateUserDto user)
        {
            var response = await _http.PutAsJsonAsync($"api/users/{id}", user);

            if (!response.IsSuccessStatusCode)
            {
                var errorMsg = await response.Content.ReadAsStringAsync();
                throw new Exception($"Erreur Modification : {errorMsg}");
            }
        }

        public async Task DeleteUser(int id)
        {
            var response = await _http.DeleteAsync($"api/users/{id}");

            if (!response.IsSuccessStatusCode)
            {
                var errorMsg = await response.Content.ReadAsStringAsync();
                throw new Exception($"Erreur Suppression : {errorMsg}");
            }
        }
    }
}