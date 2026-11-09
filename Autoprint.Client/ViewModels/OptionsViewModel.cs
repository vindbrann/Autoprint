using Autoprint.Client.Services;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Media;

namespace Autoprint.Client.ViewModels
{
    public class OptionsViewModel : INotifyPropertyChanged
    {
        private readonly UserPreferencesService _prefService;
        private int _secretClickCount = 0;
        private readonly ConfigurationService _configService;

        public OptionsViewModel(UserPreferencesService prefService, ConfigurationService configService)
        {
            _prefService = prefService;
            OnPropertyChanged(nameof(AutoSwitchDefaultPrinter));
            OnPropertyChanged(nameof(EnableNotifications));
            OnPropertyChanged(nameof(StartWithWindows));
            _configService = configService;

            AdminServerUrl = _configService.PrintServerName ?? "";
            AdminApiKey = MaskKey(_configService.ApiKey);

            var assembly = Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();
            var infoAttr = assembly.GetCustomAttributes(typeof(AssemblyInformationalVersionAttribute), false)
                                    .FirstOrDefault() as AssemblyInformationalVersionAttribute;

            if (infoAttr != null)
            {
                string versionRaw = infoAttr.InformationalVersion;
                if (versionRaw.Contains("+")) versionRaw = versionRaw.Split('+')[0];
                VersionText = $"v{versionRaw}";
            }
            else
            {
                var version = assembly.GetName().Version;
                VersionText = version != null ? $"v{version.ToString(3)}" : "v1.0.0";
            }

            VersionClickCommand = new RelayCommand(param => OnVersionClicked());
            TestConnectionCommand = new RelayCommand(async param => await TestConnectionAsync());
            CloseAdminPanelCommand = new RelayCommand(_ => ResetAdminPanel());
        }

        public bool EnableNotifications
        {
            get => _prefService.Current.EnableNotifications;
            set
            {
                if (_prefService.Current.EnableNotifications != value)
                {
                    _prefService.Current.EnableNotifications = value;
                    OnPropertyChanged();
                    _prefService.Save();
                }
            }
        }

        public bool AutoSwitchDefaultPrinter
        {
            get => _prefService.Current.AutoSwitchDefaultPrinter;
            set
            {
                if (_prefService.Current.AutoSwitchDefaultPrinter != value)
                {
                    _prefService.Current.AutoSwitchDefaultPrinter = value;
                    OnPropertyChanged();
                    _prefService.Save();
                }
            }
        }

        public bool StartWithWindows
        {
            get => _prefService.RunAtStartup;

            set
            {
                if (_prefService.RunAtStartup != value)
                {
                    _prefService.ToggleStartup(value);
                    OnPropertyChanged();
                }
            }
        }

        public string VersionText { get; }

        private bool _isAdminPanelVisible;
        public bool IsAdminPanelVisible
        {
            get => _isAdminPanelVisible;
            set { _isAdminPanelVisible = value; OnPropertyChanged(); }
        }

        private string _adminServerUrl = "";
        public string AdminServerUrl
        {
            get => _adminServerUrl;
            set { _adminServerUrl = value; OnPropertyChanged(); }
        }

        private string _adminApiKey = "";
        public string AdminApiKey
        {
            get => _adminApiKey;
            set { _adminApiKey = value; OnPropertyChanged(); }
        }

        private bool _isTesting;
        public bool IsTesting
        {
            get => _isTesting;
            set
            {
                _isTesting = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsNotTesting));
            }
        }

        public bool IsNotTesting => !IsTesting;

        private string _testStatusMessage = "";
        public string TestStatusMessage
        {
            get => _testStatusMessage;
            set { _testStatusMessage = value; OnPropertyChanged(); }
        }

        private Brush _testStatusColor = Brushes.Gray;
        public Brush TestStatusColor
        {
            get => _testStatusColor;
            set { _testStatusColor = value; OnPropertyChanged(); }
        }

        public ICommand VersionClickCommand { get; }
        public ICommand TestConnectionCommand { get; }
        public ICommand CloseAdminPanelCommand { get; }

        public void ReloadConfig()
        {
            AdminServerUrl = _configService?.PrintServerName ?? "";
            AdminApiKey = MaskKey(_configService?.ApiKey);
        }

        // La clé n'est jamais affichée en clair sur le poste (audit #11) : seuls les 4 derniers caractères permettent de la comparer à la console
        private static string MaskKey(string? key)
        {
            if (string.IsNullOrEmpty(key)) return "(aucune clé configurée)";
            return key.Length <= 4 ? "••••••••" : "••••••••" + key[^4..];
        }

        public void ResetAdminPanel()
        {
            IsAdminPanelVisible = false;
            _secretClickCount = 0;
            TestStatusMessage = "";
            ReloadConfig();
        }

        private void OnVersionClicked()
        {
            if (IsAdminPanelVisible) return;

            _secretClickCount++;
            Debug.WriteLine($"Clic Secret : {_secretClickCount}/5");

            if (_secretClickCount >= 5)
            {
                ReloadConfig();
                IsAdminPanelVisible = true;
                _secretClickCount = 0;
            }
        }

        private async Task TestConnectionAsync()
        {
            if (IsTesting) return;

            string apiKey = _configService?.ApiKey ?? "";
            if (string.IsNullOrWhiteSpace(AdminServerUrl) || string.IsNullOrWhiteSpace(apiKey))
            {
                SetStatus("Champs vides !", Brushes.Red);
                return;
            }

            string urlNettoyee = AdminServerUrl.Trim();
            if (!urlNettoyee.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
                !urlNettoyee.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                urlNettoyee = "https://" + urlNettoyee;
            }
            AdminServerUrl = urlNettoyee;

            if (!Uri.TryCreate(urlNettoyee, UriKind.Absolute, out Uri? uriResult) ||
                (uriResult.Scheme != Uri.UriSchemeHttp && uriResult.Scheme != Uri.UriSchemeHttps))
            {
                SetStatus("URL invalide (http/https requis)", Brushes.Red);
                return;
            }

            IsTesting = true;
            SetStatus("Test de connexion API...", Brushes.Orange);

            try
            {
                var tempApiService = new ApiService(urlNettoyee, apiKey);
                await tempApiService.GetLieuxAsync();

                SetStatus("✅ Connexion réussie !", Brushes.Green);
            }
            catch (Exception ex)
            {
                SetStatus($"❌ {DescribeConnectionError(ex)}", Brushes.Red);
            }
            finally
            {
                IsTesting = false;
            }
        }

        // Message compréhensible par un technicien pour chaque cause d'échec
        private static string DescribeConnectionError(Exception ex)
        {
            if (ex is TaskCanceledException) return "Serveur injoignable (délai dépassé). Vérifiez le nom du serveur, le réseau et le pare-feu.";

            if (ex is System.Net.Http.HttpRequestException httpEx)
            {
                if (httpEx.InnerException is System.Security.Authentication.AuthenticationException)
                    return "Certificat du serveur refusé. Le certificat HTTPS n'est pas reconnu par ce poste (autorité interne non installée ?).";

                switch (httpEx.StatusCode)
                {
                    case System.Net.HttpStatusCode.Unauthorized:
                        return "Clé refusée par le serveur. Comparez les 4 derniers caractères avec la console (Paramètres > Sécurité).";
                    case System.Net.HttpStatusCode.NotFound:
                        return "Adresse incorrecte : le serveur répond mais ce n'est pas Autoprint.";
                    case System.Net.HttpStatusCode.BadRequest:
                        return "Nom de serveur non autorisé : utilisez le nom public de la console (pas l'adresse IP ni un alias).";
                    case null:
                        return "Serveur injoignable. Vérifiez le nom du serveur, le réseau et le pare-feu.";
                    default:
                        return $"Erreur du serveur ({(int)httpEx.StatusCode}).";
                }
            }

            return "Erreur inattendue : " + ex.Message;
        }

        private void SetStatus(string msg, Brush color)
        {
            TestStatusMessage = msg;
            TestStatusColor = color;
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}