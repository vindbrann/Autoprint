using System.Net.Http.Json;
using Autoprint.Shared.DTOs; // Assure-toi que le DTO existe côté Shared

namespace Autoprint.Web.Services
{
    public class AlertService
    {
        private readonly HttpClient _http;

        // État des alertes (Tout vert par défaut)
        public SystemAlertsDto State { get; private set; } = new();

        // Événement de notification pour le Menu
        public event Action? OnChange;

        public AlertService(HttpClient http)
        {
            _http = http;
        }

        public async Task RefreshState()
        {
            try
            {
                // Appel vers l'API pour savoir s'il y a des problèmes
                var result = await _http.GetFromJsonAsync<SystemAlertsDto>("api/Alerts");
                if (result != null)
                {
                    State = result;
                    NotifyStateChanged();
                }
            }
            catch
            {
                // On ignore les erreurs réseau (le menu restera sans alerte)
            }
        }

        private void NotifyStateChanged() => OnChange?.Invoke();
    }
}