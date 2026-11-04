using System.Text.Json;
using Microsoft.JSInterop;

namespace Autoprint.Web.Services
{
    /// <summary>
    /// Accès au localStorage du navigateur (remplace Blazored.LocalStorage, qui n'évolue plus : backlog B41).
    /// Même format de stockage que Blazored (valeurs sérialisées en JSON) : les jetons et préférences
    /// déjà enregistrés dans les navigateurs restent lisibles après la mise à jour.
    /// </summary>
    public interface ILocalStorageService
    {
        ValueTask<T?> GetItemAsync<T>(string key);
        ValueTask SetItemAsync<T>(string key, T value);
        ValueTask RemoveItemAsync(string key);
    }

    public sealed class LocalStorageService : ILocalStorageService
    {
        private readonly IJSRuntime _js;

        public LocalStorageService(IJSRuntime js) => _js = js;

        public async ValueTask<T?> GetItemAsync<T>(string key)
        {
            var raw = await _js.InvokeAsync<string?>("localStorage.getItem", key);
            if (string.IsNullOrEmpty(raw)) return default;
            try
            {
                return JsonSerializer.Deserialize<T>(raw);
            }
            catch (JsonException) when (typeof(T) == typeof(string))
            {
                // Valeur texte enregistrée sans guillemets (par un autre code) : la rendre telle quelle
                return (T)(object)raw;
            }
            catch (JsonException)
            {
                return default;
            }
        }

        public ValueTask SetItemAsync<T>(string key, T value)
            => _js.InvokeVoidAsync("localStorage.setItem", key, JsonSerializer.Serialize(value));

        public ValueTask RemoveItemAsync(string key)
            => _js.InvokeVoidAsync("localStorage.removeItem", key);
    }
}
