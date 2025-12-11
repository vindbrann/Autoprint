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
            await _http.PostAsJsonAsync("api/users", user);
        }

        public async Task UpdateUser(int id, UpdateUserDto user)
        {
            await _http.PutAsJsonAsync($"api/users/{id}", user);
        }

        public async Task DeleteUser(int id)
        {
            await _http.DeleteAsync($"api/users/{id}");
        }
    }
}