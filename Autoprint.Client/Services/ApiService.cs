using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Autoprint.Shared;

namespace Autoprint.Client.Services
{
    public class ApiService
    {
        private readonly HttpClient _httpClient;

        public ApiService(string baseUrl, string apiKey)
        {
            _httpClient = new HttpClient
            {
                BaseAddress = new Uri(baseUrl)
            };

            _httpClient.DefaultRequestHeaders.Add("X-Agent-Secret", apiKey);
        }

        public async Task<List<Emplacement>> GetLieuxAsync()
        {
            try
            {
                var result = await _httpClient.GetFromJsonAsync<List<Emplacement>>("api/emplacements");
                return result ?? new List<Emplacement>();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Erreur API : {ex.Message}");
                return new List<Emplacement>();
            }
        }

        public async Task<List<Imprimante>> GetImprimantesAsync()
        {
            try
            {
                var result = await _httpClient.GetFromJsonAsync<List<Imprimante>>("api/Imprimantes");
                return result ?? new List<Imprimante>();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Erreur API Imprimantes : {ex.Message}");
                return new List<Imprimante>();
            }
        }
    }
}