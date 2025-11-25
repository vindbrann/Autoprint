using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Autoprint.Shared; // Grâce à la référence vers le projet Shared

namespace Autoprint.Client.Services
{
    public class ApiService
    {
        private readonly HttpClient _httpClient;

        // ⚠️ REMPLACE 7001 PAR TON PORT EXACT
        private const string BaseUrl = "https://localhost:7159";

        public ApiService(string apiKey)
        {
            var handler = new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true
            };

            _httpClient = new HttpClient(handler)
            {
                BaseAddress = new Uri(BaseUrl)
            };

            // AJOUT : On injecte le Header de sécurité
            _httpClient.DefaultRequestHeaders.Add("X-Agent-Secret", apiKey);
        }

        /// <summary>
        /// Récupère la liste de tous les lieux configurés sur le serveur
        /// </summary>
        public async Task<List<Emplacement>> GetLieuxAsync()
        {
            try
            {
                // On appelle l'API : GET /api/emplacements
                // Attention : Vérifie si ton contrôleur s'appelle "Emplacements" ou "Lieux" dans l'API
                var result = await _httpClient.GetFromJsonAsync<List<Emplacement>>("api/emplacements");
                return result ?? new List<Emplacement>();
            }
            catch (Exception ex)
            {
                // Si le serveur est éteint ou erreur, on renvoie une liste vide pour ne pas crasher
                System.Diagnostics.Debug.WriteLine($"Erreur API : {ex.Message}");
                return new List<Emplacement>();
            }
        }

        /// <summary>
        /// Récupère l'inventaire complet des imprimantes (avec Modèles, Marques, Lieux)
        /// </summary>
        public async Task<List<Imprimante>> GetImprimantesAsync()
        {
            try
            {
                // Note : Le serveur renvoie les objets imbriqués (Includes) grâce à notre modif
                var result = await _httpClient.GetFromJsonAsync<List<Imprimante>>("api/Imprimantes");
                return result ?? new List<Imprimante>();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Erreur API Imprimantes : {ex.Message}");
                // En cas d'erreur, on renvoie une liste vide pour ne pas crasher
                return new List<Imprimante>();
            }
        }
    }
}