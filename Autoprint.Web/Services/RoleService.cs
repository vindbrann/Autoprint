using System.Net.Http.Json;
using Autoprint.Shared.DTOs;

namespace Autoprint.Web.Services
{
    public interface IRoleService
    {
        Task<List<RoleViewDto>> GetRoles();
        Task<List<PermissionDto>> GetAllPermissions();
        Task<RoleEditDto> GetRoleForEdit(int id);
        Task CreateRole(RoleEditDto role);
        Task UpdateRole(int id, RoleEditDto role);
        Task DeleteRole(int id);
    }

    public class RoleService : IRoleService
    {
        private readonly HttpClient _http;

        public RoleService(HttpClient http)
        {
            _http = http;
        }

        public async Task<List<RoleViewDto>> GetRoles()
        {
            return await _http.GetFromJsonAsync<List<RoleViewDto>>("api/roles")
                   ?? new List<RoleViewDto>();
        }

        public async Task<List<PermissionDto>> GetAllPermissions()
        {
            return await _http.GetFromJsonAsync<List<PermissionDto>>("api/roles/permissions")
                   ?? new List<PermissionDto>();
        }

        public async Task<RoleEditDto> GetRoleForEdit(int id)
        {
            return await _http.GetFromJsonAsync<RoleEditDto>($"api/roles/{id}")
                   ?? new RoleEditDto();
        }

        public async Task CreateRole(RoleEditDto role)
        {
            await EnsureSuccess(await _http.PostAsJsonAsync("api/roles", role));
        }

        public async Task UpdateRole(int id, RoleEditDto role)
        {
            await EnsureSuccess(await _http.PutAsJsonAsync($"api/roles/{id}", role));
        }

        public async Task DeleteRole(int id)
        {
            await EnsureSuccess(await _http.DeleteAsync($"api/roles/{id}"));
        }

        // Un refus du serveur (403 anti-escalade, 400...) doit remonter à l'écran au lieu d'afficher « Succès »
        internal static async Task EnsureSuccess(HttpResponseMessage response)
        {
            if (response.IsSuccessStatusCode) return;
            var message = (await response.Content.ReadAsStringAsync()).Trim().Trim('"');
            if (string.IsNullOrWhiteSpace(message) || message.StartsWith("{"))
                message = $"Le serveur a refusé l'opération (code {(int)response.StatusCode}).";
            throw new Exception(message);
        }
    }
}