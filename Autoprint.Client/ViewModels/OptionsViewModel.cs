using Autoprint.Client.Services;
using Autoprint.Shared.IPC;
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
        private readonly IpcService _ipcService;
        private int _secretClickCount = 0;
        private readonly ConfigurationService _configService;

        public OptionsViewModel(UserPreferencesService prefService, IpcService ipcService, ConfigurationService configService)
        {
            _prefService = prefService;
            _ipcService = ipcService;
            _configService = configService;

            AdminServerUrl = _configService.PrintServerName ?? "";
            AdminApiKey = _configService.ApiKey ?? "";

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
            TestAndSaveCommand = new RelayCommand(async param => await TestAndSaveAsync());
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
            get => _prefService.IsWindowsStartupEnabled();
            set
            {
                if (_prefService.IsWindowsStartupEnabled() != value)
                {
                    _prefService.SetWindowsStartup(value);
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

        private Brush _testStatusColor = Brushes.Black;
        public Brush TestStatusColor
        {
            get => _testStatusColor;
            set { _testStatusColor = value; OnPropertyChanged(); }
        }

        public ICommand VersionClickCommand { get; }
        public ICommand TestAndSaveCommand { get; }

        private void OnVersionClicked()
        {
            if (IsAdminPanelVisible) return;

            _secretClickCount++;
            Debug.WriteLine($"Clic Secret : {_secretClickCount}/5");

            if (_secretClickCount >= 5)
            {
                IsAdminPanelVisible = true;
                _secretClickCount = 0;
            }
        }

        private async Task TestAndSaveAsync()
        {
            if (IsTesting) return;

            if (string.IsNullOrWhiteSpace(AdminServerUrl) || string.IsNullOrWhiteSpace(AdminApiKey))
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
                var tempApiService = new ApiService(urlNettoyee, AdminApiKey);
                await tempApiService.GetLieuxAsync();

                SetStatus("Connexion OK. Sauvegarde système...", Brushes.Blue);

                var request = new IpcRequest
                {
                    Action = "UPDATE_CONFIG",
                    ConfigServerUrl = urlNettoyee,
                    ConfigApiKey = AdminApiKey
                };

                bool ipcSuccess = await _ipcService.SendRequestAsync(request);

                if (ipcSuccess)
                {
                    SetStatus("✅ Sauvegarde Système (HKLM) réussie !", Brushes.Green);

                    _prefService.Save();

                    await Task.Delay(2000);
                    IsAdminPanelVisible = false;
                    SetStatus("", Brushes.Black);
                    _secretClickCount = 0;
                }
                else
                {
                    SetStatus("⚠️ Erreur : Le Service n'a pas confirmé la sauvegarde.", Brushes.Red);
                }
            }
            catch (Exception ex)
            {
                string msg = ex.Message;
                if (msg.Contains("401")) msg = "Clé API refusée (401)";
                else if (msg.Contains("404")) msg = "Serveur introuvable (404)";

                SetStatus($"ÉCHEC : {msg}", Brushes.Red);
            }
            finally
            {
                IsTesting = false;
            }
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