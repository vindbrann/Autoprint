using System.Net.Http.Json;
using Autoprint.Shared;

namespace Autoprint.Client.Services
{
    public class NetworkScannerService
    {
        private readonly HttpClient _http;

        public NetworkScannerService(HttpClient http)
        {
            _http = http;
        }

        public async Task<List<DiscoveryProfile>> GetAllProfilesAsync()
        {
            return await _http.GetFromJsonAsync<List<DiscoveryProfile>>("api/discovery") ?? new List<DiscoveryProfile>();
        }

        public async Task SaveProfileAsync(DiscoveryProfile profile)
        {
            await _http.PostAsJsonAsync("api/discovery", profile);
        }

        public async Task DeleteProfileAsync(int id)
        {
            await _http.DeleteAsync($"api/discovery/{id}");
        }

        public async Task<DiscoveryProfile?> RunScanNowAsync(int profileId)
        {
            var response = await _http.PostAsync($"api/discovery/{profileId}/run", null);

            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<DiscoveryProfile>();
            }
            return null;
        }
    }
}