using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Autoprint.Shared;

namespace Autoprint.Client.Services
{
    public class ApiService
    {
        private readonly HttpClient _httpClient;
        private readonly JsonSerializerOptions _jsonOptions;

        public ApiService(string baseUrl, string apiKey)
        {
            var handler = new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true
            };

            _httpClient = new HttpClient(handler)
            {
                BaseAddress = new Uri(baseUrl),
                Timeout = TimeSpan.FromSeconds(10)
            };

            _httpClient.DefaultRequestHeaders.Add("X-Agent-Secret", apiKey);

            _jsonOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                ReferenceHandler = ReferenceHandler.IgnoreCycles
            };
        }

        public async Task<List<Emplacement>> GetLieuxAsync()
        {
            var response = await _httpClient.GetAsync("api/emplacements");
            response.EnsureSuccessStatusCode();
            var result = await response.Content.ReadFromJsonAsync<List<Emplacement>>(_jsonOptions);
            return result ?? new List<Emplacement>();
        }

        public async Task<List<Imprimante>> GetImprimantesAsync()
        {
            var response = await _httpClient.GetAsync("api/imprimantes");
            response.EnsureSuccessStatusCode();
            var result = await response.Content.ReadFromJsonAsync<List<Imprimante>>(_jsonOptions);
            return result ?? new List<Imprimante>();
        }
    }
}