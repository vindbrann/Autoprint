using System.Net.Http.Json;
using Autoprint.Shared.DTOs;

namespace Autoprint.Web.Services
{
    public interface IRoleService
    {
        Task<List<RoleViewDto>> GetRoles();
        Task<List<PermissionDto>> GetAllPermissions(); // Pour récupérer la liste des cases à cocher
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
            await _http.PostAsJsonAsync("api/roles", role);
        }

        public async Task UpdateRole(int id, RoleEditDto role)
        {
            await _http.PutAsJsonAsync($"api/roles/{id}", role);
        }

        public async Task DeleteRole(int id)
        {
            await _http.DeleteAsync($"api/roles/{id}");
        }
    }
}