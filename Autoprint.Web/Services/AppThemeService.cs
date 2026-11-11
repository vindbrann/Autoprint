using Microsoft.JSInterop;
using Radzen;

namespace Autoprint.Web.Services
{
    /// <summary>
    /// Thème clair / sombre de la console (charte graphique, B77) : thème Radzen « standard » / « standard-dark »
    /// et attribut data-theme="dark" sur &lt;html&gt;, que lit autoprint-theme.css. Choix mémorisé par navigateur.
    /// </summary>
    public sealed class AppThemeService
    {
        public const string LightTheme = "standard";
        public const string DarkTheme = "standard-dark";
        private const string StorageKey = "user_theme";

        private readonly ThemeService _radzenTheme;
        private readonly IJSRuntime _js;
        private readonly ILocalStorageService _storage;

        public AppThemeService(ThemeService radzenTheme, IJSRuntime js, ILocalStorageService storage)
        {
            _radzenTheme = radzenTheme;
            _js = js;
            _storage = storage;
        }

        public bool IsDark { get; private set; }
        public event Action? OnChange;

        /// <summary>Applique le thème mémorisé (les anciennes valeurs « material » / « material-dark » sont reprises).</summary>
        public async Task InitializeAsync()
        {
            string? saved = null;
            try { saved = await _storage.GetItemAsync<string>(StorageKey); } catch { /* stockage indisponible : thème clair */ }
            await ApplyAsync(saved != null && saved.EndsWith("dark", StringComparison.OrdinalIgnoreCase), save: false);
        }

        public Task ToggleAsync() => ApplyAsync(!IsDark, save: true);

        private async Task ApplyAsync(bool dark, bool save)
        {
            IsDark = dark;
            _radzenTheme.SetTheme(dark ? DarkTheme : LightTheme);
            try { await _js.InvokeVoidAsync("autoprintSetTheme", dark); } catch (JSException) { }
            if (save)
            {
                try { await _storage.SetItemAsync(StorageKey, dark ? DarkTheme : LightTheme); } catch { }
            }
            OnChange?.Invoke();
        }
    }
}
