using System.Text.Json;
using Radzen;

namespace Autoprint.Web.Services
{
    /// <summary>
    /// Retour d'un appel d'enregistrement : les modales affichaient « Succès » même quand le serveur refusait
    /// (code HTTP non vérifié). Affiche le message du serveur et renvoie faux en cas de refus.
    /// </summary>
    public static class ApiFeedback
    {
        public static async Task<bool> CheckAsync(this HttpResponseMessage response, NotificationService notifications, string summary = "Enregistrement refusé")
        {
            if (response.IsSuccessStatusCode) return true;
            notifications.Notify(NotificationSeverity.Error, summary, await ReadMessageAsync(response));
            return false;
        }

        public static async Task<string> ReadMessageAsync(HttpResponseMessage response)
        {
            var text = (await response.Content.ReadAsStringAsync()).Trim();
            if (text.StartsWith('{'))
            {
                try
                {
                    using var doc = JsonDocument.Parse(text);
                    var root = doc.RootElement;
                    if (root.TryGetProperty("errors", out var errors) && errors.ValueKind == JsonValueKind.Object)
                        foreach (var e in errors.EnumerateObject())
                            if (e.Value.ValueKind == JsonValueKind.Array && e.Value.GetArrayLength() > 0) return e.Value[0].GetString() ?? text;
                    foreach (var key in new[] { "message", "detail", "title" })
                        if (root.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.String) return v.GetString() ?? text;
                }
                catch (JsonException) { }
            }
            if (text.StartsWith('"') && text.EndsWith('"') && text.Length > 1) text = text[1..^1];
            return string.IsNullOrWhiteSpace(text) ? $"Le serveur a répondu {(int)response.StatusCode}." : text;
        }
    }
}
