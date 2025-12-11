using System.Net.Http.Json;
using Autoprint.Shared.DTOs;

namespace Autoprint.Web.Services
{
    public class AlertService
    {
        private readonly HttpClient _http;
        public SystemAlertsDto State { get; private set; } = new();
        public event Action? OnChange;

        public AlertService(HttpClient http)
        {
            _http = http;
        }

        public async Task RefreshState()
        {
            try
            {
                var result = await _http.GetFromJsonAsync<SystemAlertsDto>("api/Alerts");
                if (result != null)
                {
                    State = result;
                    NotifyStateChanged();
                }
            }
            catch
            {
            }
        }

        private void NotifyStateChanged() => OnChange?.Invoke();
    }
}