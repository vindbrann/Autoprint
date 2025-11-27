using Autoprint.Client.Data;
using Autoprint.Client.Models;
using Autoprint.Client.Services;
using Autoprint.Client.ViewModels;
using Autoprint.Shared;
using Hardcodet.Wpf.TaskbarNotification;
using MaterialDesignThemes.Wpf;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.NetworkInformation;
using System.Threading.Tasks;
using System.Windows;

namespace Autoprint.Client
{
    public partial class App : Application
    {
        private TaskbarIcon? _notifyIcon;

        // Services
        private readonly PathService _pathService = new PathService();
        private readonly NetworkService _networkService = new NetworkService();
        private readonly ConfigurationService _configService = new ConfigurationService();
        private readonly UserPreferencesService _prefService = new UserPreferencesService();
        private readonly IpcService _ipcService = new IpcService();

        private ApiService? _apiService;
        private DataService? _dataService;

        // [SIGNALR] : Ajout du nouveau service Temps Réel
        private RealTimeService? _realTimeService;

        // UI & ViewModels
        private MainWindow? _mainWindow;
        private MainWindowViewModel? _mainViewModel;
        private OptionsWindow? _optionsWindow;
        private OptionsViewModel? _optionsViewModel;
        private ManagePrintersWindow? _manageWindow;
        private ManagePrintersViewModel? _manageViewModel;

        // État de l'application
        private List<Imprimante> _toutesLesImprimantes = new List<Imprimante>();
        private Emplacement? _lieuActuel = null;
        private string _ipActuelle = "Inconnue";

        // État de Connexion
        private bool _estHorsLigne = false;

        // [SIGNALR] : Suppression de l'ancien _retryTimer (remplacé par RealTimeService)

        protected override async void OnStartup(StartupEventArgs e)
        {
            // 1. Surveillance Thème
            SystemEvents.UserPreferenceChanged += (s, args) =>
            {
                if (args.Category == UserPreferenceCategory.General) UpdateTheme();
            };
            UpdateTheme();

            base.OnStartup(e);

            // 2. Init UI
            _notifyIcon = (TaskbarIcon)FindResource("MainNotifyIcon");
            if (_notifyIcon != null)
            {
                _notifyIcon.IconSource = new System.Windows.Media.Imaging.BitmapImage(new Uri("pack://application:,,,/Autoprint.ico"));
            }

            _mainViewModel = new MainWindowViewModel(_prefService, _ipcService);
            _mainWindow = new MainWindow();
            _mainWindow.DataContext = _mainViewModel;

            _optionsViewModel = new OptionsViewModel(_prefService);
            _optionsWindow = new OptionsWindow();
            _optionsWindow.DataContext = _optionsViewModel;

            _manageViewModel = new ManagePrintersViewModel();
            _manageWindow = new ManagePrintersWindow();
            _manageWindow.DataContext = _manageViewModel;

            // 3. Init Services & Configuration
            _configService.Initialize(e.Args, _prefService);
            _apiService = new ApiService(_configService.ApiKey);

            _pathService.Initialize(e.Args);
            _dataService = new DataService(_pathService);
            await _dataService.InitializeAsync();

            // 4. [SIGNALR] : Initialisation du Temps Réel
            string serverUrl = $"https://{_configService.PrintServerName}:7159";

            _realTimeService = new RealTimeService(serverUrl);

            // On branche les callbacks (Ce que le service doit faire quand il a des infos)
            _realTimeService.Initialize(
                onRefreshRequested: async () =>
                {
                    // Le serveur a dit "Refresh" ou on vient de se reconnecter
                    await Dispatcher.InvokeAsync(async () => await RafraichirDonneesAsync(estDemarrage: false));
                },
                onStatusChanged: (isOnline) =>
                {
                    // L'état de connexion a changé (piloté par le Ping/Pong SignalR)
                    _estHorsLigne = !isOnline;
                    MettreAJourInterface(_estHorsLigne);
                }
            );

            // On lance la connexion (démarre le Watchdog interne si échec)
            await _realTimeService.StartAsync();

            // 5. Écouteur Réseau (Câble débranché/rebranché)
            NetworkChange.NetworkAddressChanged += async (s, args) => await OnReseauChangeAsync();

            // 6. Premier chargement de données
            await RafraichirDonneesAsync(estDemarrage: true);
        }

        // ============================================================
        // CŒUR DE L'APPLICATION (Logique Métier & Réseau)
        // ============================================================
        public async Task RafraichirDonneesAsync(bool estDemarrage = false)
        {
            string? myIp = _networkService.GetLocalIpAddress();
            _ipActuelle = myIp ?? "Non connecté";

            List<Emplacement> lieux = new List<Emplacement>();

            // [SIGNALR] : On ne force pas _estHorsLigne à false ici. 
            // C'est le RealTimeService qui est le maître de l'état de connexion.

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

                    if (_dataService != null)
                    {
                        await _dataService.UpdateCacheAsync(lieux, _toutesLesImprimantes);
                    }
                }
            }
            catch
            {
                System.Diagnostics.Debug.WriteLine("Échec récupération données API (Cache utilisé)");

                if (_dataService != null)
                {
                    lieux = await _dataService.GetEmplacementsAsync();
                    _toutesLesImprimantes = await _dataService.GetImprimantesAsync();
                }
            }

            // C. Logique de Lieu et Notifications
            string titreNotif = "Autoprint";
            string messageNotif = "Mise à jour...";
            BalloonIcon iconeNotif = BalloonIcon.None;

            if (_estHorsLigne) iconeNotif = BalloonIcon.Warning;

            if (lieux.Count > 0 && !string.IsNullOrEmpty(myIp))
            {
                var lieuTrouve = lieux.Find(l => IpHelper.IsIpInCidr(myIp, l.CidrIpv4));

                if (lieuTrouve != null)
                {
                    _lieuActuel = lieuTrouve;

                    if (_prefService.Current.LastDetectedLocationCode != lieuTrouve.Code)
                    {
                        _prefService.Current.LastDetectedLocationCode = lieuTrouve.Code;
                        _prefService.Save();
                    }

                    MettreAJourInterface(_estHorsLigne);

                    int nbImprimantesZone = _toutesLesImprimantes.Count(i => i.EmplacementId == lieuTrouve.Id);
                    titreNotif = $"📍 Lieu : {lieuTrouve.Nom}";

                    // Logique Auto-Switch
                    string messageSwitch = "";
                    if (_prefService.Current.AutoSwitchDefaultPrinter)
                    {
                        if (_prefService.Current.PreferredPrinters.TryGetValue(lieuTrouve.Code, out string? favoritePrinterName))
                        {
                            if (SetDefaultPrinter(favoritePrinterName))
                            {
                                messageSwitch = $"\n✅ {favoritePrinterName} définie par défaut.";
                            }
                        }
                    }

                    if (!string.IsNullOrEmpty(messageSwitch))
                        messageNotif = $"Bienvenue à {lieuTrouve.Nom}{messageSwitch}";
                    else if (nbImprimantesZone > 0)
                        messageNotif = $"{nbImprimantesZone} imprimante(s) disponible(s).";
                    else
                        messageNotif = "Aucune imprimante pour cette zone.";

                    if (_estHorsLigne)
                    {
                        messageNotif += "\n(Mode Hors-Ligne ⚠️)";
                    }
                }
                else
                {
                    _lieuActuel = null;
                    MettreAJourInterface(_estHorsLigne);
                    titreNotif = "⛔ Lieu Inconnu";
                    messageNotif = $"IP : {myIp}\nAucune zone correspondante.";
                    iconeNotif = BalloonIcon.Warning;
                }
            }
            else
            {
                MettreAJourInterface(_estHorsLigne);
                titreNotif = "Erreur";
                messageNotif = "Impossible de récupérer la configuration.";
                iconeNotif = BalloonIcon.Error;
            }

            if (_notifyIcon != null && _prefService.Current.EnableNotifications && estDemarrage)
            {
                _notifyIcon.ShowBalloonTip(titreNotif, messageNotif, iconeNotif);
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

        // ============================================================
        // GESTION DES FENÊTRES ET MENUS
        // ============================================================

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
                _manageWindow.Closed += (s, args) =>
                {
                    _mainViewModel?.RafraichirEtatInstallation();
                    _manageWindow = null;
                };
            }
            _manageWindow.Show();
            _manageWindow.Activate();
        }

        private void OnTrayBalloonTipClicked(object sender, RoutedEventArgs e) => AfficherFenetre();
        private void OnTrayDoubleClick(object sender, RoutedEventArgs e) => AfficherFenetre();
        private void MenuOpen_Click(object sender, RoutedEventArgs e) => AfficherFenetre();
        private void MenuExit_Click(object sender, RoutedEventArgs e) { Shutdown(); }

        private void AfficherFenetre()
        {
            if (_mainWindow == null) return;

            // On rafraîchit l'interface avec le DERNIER état connu (Online/Offline)
            MettreAJourInterface(_estHorsLigne);

            if (_mainViewModel != null)
            {
                _mainViewModel.RafraichirEtatInstallation();
            }

            if (_mainWindow.Visibility == Visibility.Visible)
                _mainWindow.Activate();
            else
                _mainWindow.Show();
        }

        private async Task OnReseauChangeAsync()
        {
            await Task.Delay(3000); // Debounce
            await Dispatcher.InvokeAsync(async () =>
            {
                await RafraichirDonneesAsync();
            });
        }

        protected override void OnExit(ExitEventArgs e)
        {
            _notifyIcon?.Dispose();
            base.OnExit(e);
        }
    }
}