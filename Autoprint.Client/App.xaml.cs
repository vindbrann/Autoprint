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

        // État de Connexion (Bloc B)
        private bool _estHorsLigne = false;
        private System.Timers.Timer? _retryTimer;

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

            // 3. Init Services & Configuration (Bloc A - Validé)
            // On passe les arguments et le service de préférences pour gérer la persistance JSON
            _configService.Initialize(e.Args, _prefService);

            // L'API Service est initialisé avec la clé consolidée
            _apiService = new ApiService(_configService.ApiKey);

            _pathService.Initialize(e.Args);
            _dataService = new DataService(_pathService);
            await _dataService.InitializeAsync();

            // 4. Init Timer de reconnexion (Bloc B - Watchdog)
            _retryTimer = new System.Timers.Timer(30000); // 30 secondes
            _retryTimer.AutoReset = true;
            _retryTimer.Elapsed += async (s, args) =>
            {
                // On revient sur le thread UI pour relancer la tentative
                await Dispatcher.InvokeAsync(async () => await RafraichirDonneesAsync());
            };

            // 5. Écouteur Réseau
            NetworkChange.NetworkAddressChanged += async (s, args) => await OnReseauChangeAsync();

            // 6. Premier Lancement
            await RafraichirDonneesAsync(estDemarrage: true);
        }

        // ============================================================
        // CŒUR DE L'APPLICATION (Logique Métier & Réseau)
        // ============================================================
        public async Task RafraichirDonneesAsync(bool estDemarrage = false)
        {
            // A. Détection IP
            string? myIp = _networkService.GetLocalIpAddress();
            _ipActuelle = myIp ?? "Non connecté";

            List<Emplacement> lieux = new List<Emplacement>();

            // On part du principe qu'on va y arriver
            bool echecConnexion = false;

            // On reset l'état local avant de tenter (sauf si on tombe dans le catch)
            _estHorsLigne = false;

            // B. Tentative de Connexion
            try
            {
                if (_apiService != null)
                {
                    var taskLieux = _apiService.GetLieuxAsync();
                    var taskImprimantes = _apiService.GetImprimantesAsync();
                    await Task.WhenAll(taskLieux, taskImprimantes);

                    lieux = taskLieux.Result;
                    _toutesLesImprimantes = taskImprimantes.Result;

                    // Si l'API renvoie 0 lieu alors qu'on attend des données, on considère ça comme une erreur
                    if (lieux.Count == 0) throw new Exception("API vide ou injoignable");

                    // SUCCÈS : Mise à jour du cache local
                    if (_dataService != null)
                    {
                        await _dataService.UpdateCacheAsync(lieux, _toutesLesImprimantes);
                    }

                    // On arrête le timer de retry car tout va bien
                    _retryTimer?.Stop();
                }
            }
            catch
            {
                System.Diagnostics.Debug.WriteLine("Passage en mode HORS LIGNE");
                echecConnexion = true;
                _estHorsLigne = true;

                // ÉCHEC : On active le Watchdog pour réessayer plus tard
                if (_retryTimer != null && !_retryTimer.Enabled)
                {
                    _retryTimer.Start();
                }

                // Fallback sur le Cache SQLite
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
            bool shouldNotify = _prefService.Current.EnableNotifications;

            if (lieux.Count > 0 && !string.IsNullOrEmpty(myIp))
            {
                var lieuTrouve = lieux.Find(l => IpHelper.IsIpInCidr(myIp, l.CidrIpv4));

                if (lieuTrouve != null)
                {
                    _lieuActuel = lieuTrouve;

                    // Sauvegarde du dernier lieu connu
                    if (_prefService.Current.LastDetectedLocationCode != lieuTrouve.Code)
                    {
                        _prefService.Current.LastDetectedLocationCode = lieuTrouve.Code;
                        _prefService.Save();
                    }

                    // Mise à jour de l'UI avec l'état de connexion
                    MettreAJourInterface(_estHorsLigne);

                    int nbImprimantesZone = _toutesLesImprimantes.Count(i => i.EmplacementId == lieuTrouve.Id);
                    titreNotif = $"📍 Lieu : {lieuTrouve.Nom}";

                    // Switch automatique de l'imprimante par défaut
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
                        iconeNotif = BalloonIcon.Warning;
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
                // Cas Critique : Pas de réseau ET pas de cache
                MettreAJourInterface(_estHorsLigne);
                titreNotif = "Erreur";
                messageNotif = "Impossible de récupérer la configuration.";
                iconeNotif = BalloonIcon.Error;
                shouldNotify = true;
            }

            // Affichage de la bulle (éviter le spam en cas de boucle retry)
            // On notifie seulement au démarrage ou si ce n'est pas un retry automatique silencieux
            if (_notifyIcon != null && shouldNotify && estDemarrage)
            {
                _notifyIcon.ShowBalloonTip(titreNotif, messageNotif, iconeNotif);
            }
        }

        private void MettreAJourInterface(bool isOffline = false)
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
            _retryTimer?.Stop();
            _retryTimer?.Dispose();
            base.OnExit(e);
        }
    }
}