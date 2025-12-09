using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.NetworkInformation;
using System.Threading.Tasks;
using System.Windows;
using Autoprint.Client.Models;
using Autoprint.Client.Services;
using Autoprint.Client.ViewModels;
using Autoprint.Shared;
using Hardcodet.Wpf.TaskbarNotification;
using MaterialDesignThemes.Wpf;
using Microsoft.Win32;

namespace Autoprint.Client
{
    public partial class App : Application
    {
        private TaskbarIcon? _notifyIcon;
        private readonly PathService _pathService = new PathService();
        private readonly NetworkService _networkService = new NetworkService();
        private readonly ConfigurationService _configService = new ConfigurationService();
        private readonly UserPreferencesService _prefService = new UserPreferencesService();
        private readonly IpcService _ipcService = new IpcService();
        private ApiService? _apiService;
        private DataService? _dataService;
        private RealTimeService? _realTimeService;
        private MainWindow? _mainWindow;
        private MainWindowViewModel? _mainViewModel;
        private OptionsWindow? _optionsWindow;
        private OptionsViewModel? _optionsViewModel;
        private ManagePrintersWindow? _manageWindow;
        private ManagePrintersViewModel? _manageViewModel;
        private List<Imprimante> _toutesLesImprimantes = new List<Imprimante>();
        private Emplacement? _lieuActuel = null;
        private string _ipActuelle = "Inconnue";
        private bool _estHorsLigne = false;

        protected override async void OnStartup(StartupEventArgs e)
        {
            SystemEvents.UserPreferenceChanged += (s, args) =>
            {
                if (args.Category == UserPreferenceCategory.General) UpdateTheme();
            };
            UpdateTheme();

            base.OnStartup(e);

            try
            {
                _notifyIcon = (TaskbarIcon)FindResource("MainNotifyIcon");
                if (_notifyIcon != null)
                    _notifyIcon.IconSource = new System.Windows.Media.Imaging.BitmapImage(new Uri("pack://application:,,,/Autoprint.ico"));
            }
            catch { }

            _mainViewModel = new MainWindowViewModel(_prefService, _ipcService);
            _mainWindow = new MainWindow();
            _mainWindow.DataContext = _mainViewModel;

            _optionsViewModel = new OptionsViewModel(_prefService);
            _manageViewModel = new ManagePrintersViewModel();

            try
            {
                _configService.Initialize(e.Args, _prefService);

                string serverInput = _configService.PrintServerName;

                if (string.IsNullOrWhiteSpace(serverInput)) serverInput = "localhost";

                string baseUrl;
                if (serverInput.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                {
                    baseUrl = serverInput;
                }
                else
                {
                    baseUrl = $"https://{serverInput}";
                }

                _apiService = new ApiService(baseUrl, _configService.ApiKey);

                _pathService.Initialize(e.Args);
                _dataService = new DataService(_pathService);

                await _dataService.InitializeDatabaseAsync();
                _realTimeService = new RealTimeService(baseUrl);

                _realTimeService.Initialize(
                    onRefreshRequested: async () => await Dispatcher.InvokeAsync(async () => await RafraichirDonneesAsync(false)),
                    onStatusChanged: (isOnline) =>
                    {
                        _estHorsLigne = !isOnline;
                        MettreAJourInterface(_estHorsLigne);
                    }
                );

                _ = _realTimeService.StartAsync();

                NetworkChange.NetworkAddressChanged += async (s, args) => await OnReseauChangeAsync();

                await RafraichirDonneesAsync(estDemarrage: true);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Erreur init services/BDD : {ex.Message}");
            }
        }

        public async Task RafraichirDonneesAsync(bool estDemarrage = false)
        {
            string? myIp = _networkService.GetLocalIpAddress();
            _ipActuelle = myIp ?? "Non connecté";

            List<Emplacement> lieux = new List<Emplacement>();

            try
            {
                if (_apiService != null)
                {
                    var taskLieux = _apiService.GetLieuxAsync();
                    var taskImprimantes = _apiService.GetImprimantesAsync();
                    await Task.WhenAll(taskLieux, taskImprimantes);

                    lieux = taskLieux.Result;
                    _toutesLesImprimantes = taskImprimantes.Result;

                    if (lieux.Count == 0) throw new Exception("API vide");

                    if (_dataService != null) await _dataService.UpdateCacheAsync(lieux, _toutesLesImprimantes);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ERREUR SYNC API/CACHE : {ex.Message}");
                if (_dataService != null)
                {
                    lieux = await _dataService.GetEmplacementsAsync();
                    _toutesLesImprimantes = await _dataService.GetImprimantesAsync();
                }
            }

            string titreNotif = "Autoprint";
            string messageNotif = "Mise à jour...";
            BalloonIcon iconeNotif = BalloonIcon.None;
            bool lieuAChange = false;

            if (_estHorsLigne) iconeNotif = BalloonIcon.Warning;

            if (lieux.Count > 0 && !string.IsNullOrEmpty(myIp))
            {
                var lieuTrouve = lieux.Find(l => IpHelper.IsIpInCidr(myIp, l.CidrIpv4));

                if (lieuTrouve != null)
                {
                    if (_lieuActuel?.Code != lieuTrouve.Code)
                    {
                        lieuAChange = true;
                    }

                    _lieuActuel = lieuTrouve;

                    if (_prefService.Current.LastDetectedLocationCode != lieuTrouve.Code)
                    {
                        _prefService.Current.LastDetectedLocationCode = lieuTrouve.Code;
                        _prefService.Save();
                    }

                    int nbImprimantesZone = _toutesLesImprimantes.Count(i => i.EmplacementId == lieuTrouve.Id);
                    titreNotif = $"📍 Lieu : {lieuTrouve.Nom}";

                    string messageSwitch = "";
                    if (_prefService.Current.AutoSwitchDefaultPrinter && (lieuAChange || estDemarrage))
                    {
                        if (_prefService.Current.PreferredPrinters.TryGetValue(lieuTrouve.Code, out string? favoritePrinterName))
                        {
                            if (SetDefaultPrinter(favoritePrinterName))
                                messageSwitch = $"\n✅ {favoritePrinterName} par défaut.";
                        }
                    }

                    if (!string.IsNullOrEmpty(messageSwitch))
                        messageNotif = $"Bienvenue à {lieuTrouve.Nom}{messageSwitch}";
                    else if (nbImprimantesZone > 0)
                        messageNotif = $"{nbImprimantesZone} imprimante(s) disponible(s).";
                    else
                        messageNotif = "Aucune imprimante pour cette zone.";
                }
                else
                {
                    if (_lieuActuel != null) lieuAChange = true;

                    _lieuActuel = null;
                    titreNotif = "⛔ Lieu Inconnu";
                    messageNotif = $"IP : {myIp}";
                    iconeNotif = BalloonIcon.Warning;
                }
            }
            else
            {
                titreNotif = "Erreur";
                messageNotif = "Impossible de récupérer la configuration.";
                iconeNotif = BalloonIcon.Error;
            }

            MettreAJourInterface(_estHorsLigne);

            if (_notifyIcon != null && _prefService.Current.EnableNotifications)
            {
                if (estDemarrage || lieuAChange)
                {
                    _notifyIcon.ShowBalloonTip(titreNotif, messageNotif, iconeNotif);
                }
            }
        }

        private void MettreAJourInterface(bool isOffline)
        {
            if (_mainViewModel == null) return;

            string nomLieu = _lieuActuel?.Nom ?? "Lieu Inconnu";
            string codeLieu = _lieuActuel?.Code ?? "";

            List<Imprimante> imprimantesDuLieu;

            if (_lieuActuel != null)
                imprimantesDuLieu = _toutesLesImprimantes.Where(i => i.EmplacementId == _lieuActuel.Id).ToList();
            else
                imprimantesDuLieu = new List<Imprimante>();

            Application.Current.Dispatcher.Invoke(() =>
            {
                _mainViewModel.ChargerDonnees(nomLieu, codeLieu, imprimantesDuLieu, _ipActuelle, isOffline);
            });
        }

        private void UpdateTheme()
        {
            var paletteHelper = new PaletteHelper();
            var theme = paletteHelper.GetTheme();
            var systemTheme = Theme.GetSystemTheme() ?? BaseTheme.Light;
            theme.SetBaseTheme(systemTheme);
            paletteHelper.SetTheme(theme);
        }

        private bool SetDefaultPrinter(string printerName)
        {
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo
                {
                    FileName = "rundll32.exe",
                    Arguments = $"printui.dll,PrintUIEntry /y /n \"{printerName}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                Process.Start(psi);
                return true;
            }
            catch { return false; }
        }

        private void MenuOptions_Click(object sender, RoutedEventArgs e)
        {
            if (_optionsWindow == null || !_optionsWindow.IsLoaded)
            {
                _optionsWindow = new OptionsWindow();
                _optionsWindow.DataContext = _optionsViewModel;
            }
            _optionsWindow.Show();
            _optionsWindow.Activate();
        }

        private void MenuManage_Click(object sender, RoutedEventArgs e)
        {
            if (_manageViewModel == null) return;
            _manageViewModel.RefreshPrinters();
            if (_manageWindow == null || !_manageWindow.IsLoaded)
            {
                _manageWindow = new ManagePrintersWindow();
                _manageWindow.DataContext = _manageViewModel;
                _manageWindow.Closed += (s, args) => _mainViewModel?.RafraichirEtatInstallation();
            }
            _manageWindow.Show();
            _manageWindow.Activate();
        }

        private void AfficherFenetre()
        {
            if (_mainWindow == null) return;
            MettreAJourInterface(_estHorsLigne);
            _mainViewModel?.RafraichirEtatInstallation();

            if (_mainWindow.Visibility == Visibility.Visible)
                _mainWindow.Activate();
            else
                _mainWindow.Show();
        }

        private async Task OnReseauChangeAsync()
        {
            await Task.Delay(3000);
            await Dispatcher.InvokeAsync(async () => await RafraichirDonneesAsync());
        }

        private void MenuExit_Click(object sender, RoutedEventArgs e) { Shutdown(); }
        private void OnTrayBalloonTipClicked(object sender, RoutedEventArgs e) => AfficherFenetre();
        private void OnTrayDoubleClick(object sender, RoutedEventArgs e) => AfficherFenetre();
        private void MenuOpen_Click(object sender, RoutedEventArgs e) => AfficherFenetre();

        protected override void OnExit(ExitEventArgs e)
        {
            _notifyIcon?.Dispose();
            base.OnExit(e);
        }
    }
}