using System.Net.Http.Json;
using Autoprint.Shared.DTOs;

namespace Autoprint.Web.Services
{
    public interface ISyncService
    {
        Task<List<SyncPreviewDto>> GetPreview();
        Task<BatchResult> ApplyChanges(List<int> ids);
    }

    public class SyncService : ISyncService
    {
        private readonly HttpClient _http;

        public SyncService(HttpClient http)
        {
            _http = http;
        }

        public async Task<List<SyncPreviewDto>> GetPreview()
        {
            return await _http.GetFromJsonAsync<List<SyncPreviewDto>>("api/Sync/preview")
                   ?? new List<SyncPreviewDto>();
        }

        public async Task<BatchResult> ApplyChanges(List<int> ids)
        {
            var response = await _http.PostAsJsonAsync("api/Sync/apply", ids);
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<BatchResult>()
                       ?? new BatchResult { Success = false };
            }
            return new BatchResult { Success = false, Messages = new List<string> { "Erreur HTTP" } };
        }
    }
}