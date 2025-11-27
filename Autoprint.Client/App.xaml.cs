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
using System.Threading.Tasks;
using System.Windows;
using System.Net.NetworkInformation;

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

        // UI & État
        private MainWindow? _mainWindow;
        private MainWindowViewModel? _mainViewModel;
        private OptionsWindow? _optionsWindow;
        private OptionsViewModel? _optionsViewModel;
        private ManagePrintersWindow? _manageWindow;
        private ManagePrintersViewModel? _manageViewModel;

        // Données en mémoire
        private List<Imprimante> _toutesLesImprimantes = new List<Imprimante>();
        private Emplacement? _lieuActuel = null;
        private string _ipActuelle = "Inconnue";

        protected override async void OnStartup(StartupEventArgs e)
        {
            // 1. Surveillance Thème
            SystemEvents.UserPreferenceChanged += (s, args) =>
            {
                if (args.Category == UserPreferenceCategory.General) UpdateTheme();
            };
            UpdateTheme(); // Appliquer le thème au démarrage

            base.OnStartup(e);

            // 2. Init UI
            // IMPORTANT : On charge l'objet depuis le XAML
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

            // 3. Init Services
            _configService.Initialize(e.Args);
            _apiService = new ApiService(_configService.ApiKey);
            _pathService.Initialize(e.Args);
            _dataService = new DataService(_pathService);
            await _dataService.InitializeAsync();

            string? argServer = GetArgValue(e.Args, "--print-server");

            if (!string.IsNullOrEmpty(argServer))
            {
                // Si oui, on l'écrase dans la config utilisateur et on sauvegarde
                _prefService.Current.PrintServerName = argServer;
                _prefService.Save();
            }

            // --- AJOUT : ÉCOUTEUR RÉSEAU ---
            // Dès que l'IP change, Windows prévient l'application
            NetworkChange.NetworkAddressChanged += async (s, args) => await OnReseauChangeAsync();
            // -------------------------------

            // 4. Lancer la récupération
            await RafraichirDonneesAsync(estDemarrage: true);
        }

        // ============================================================
        // CŒUR DE L'APPLICATION
        // ============================================================
        public async Task RafraichirDonneesAsync(bool estDemarrage = false)
        {
            // A. Détection IP
            string? myIp = _networkService.GetLocalIpAddress();
            _ipActuelle = myIp ?? "Non connecté";

            // B. Récupération Données
            List<Emplacement> lieux = new List<Emplacement>();
            bool modeHorsLigne = false;

            try
            {
                if (_apiService != null)
                {
                    var taskLieux = _apiService.GetLieuxAsync();
                    var taskImprimantes = _apiService.GetImprimantesAsync();
                    await Task.WhenAll(taskLieux, taskImprimantes);

                    lieux = taskLieux.Result;
                    _toutesLesImprimantes = taskImprimantes.Result;

                    if (_dataService != null && lieux.Count > 0)
                    {
                        // On passe tout d'un coup à la méthode atomique
                        await _dataService.UpdateCacheAsync(lieux, _toutesLesImprimantes);
                    }
                }
            }
            catch
            {
                if (_dataService != null)
                {
                    lieux = await _dataService.GetEmplacementsAsync();
                    _toutesLesImprimantes = await _dataService.GetImprimantesAsync();
                    modeHorsLigne = true;
                }
            }

            // C. Logique "Métier"
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

                    if (_prefService.Current.LastDetectedLocationCode != lieuTrouve.Code)
                    {
                        _prefService.Current.LastDetectedLocationCode = lieuTrouve.Code;
                        _prefService.Save();
                    }

                    MettreAJourInterface();

                    int nbImprimantesZone = _toutesLesImprimantes.Count(i => i.EmplacementId == lieuTrouve.Id);
                    titreNotif = $"📍 Lieu : {lieuTrouve.Nom}";

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

                    if (modeHorsLigne) messageNotif += "\n(Mode Hors-Ligne ⚠️)";
                }
                else
                {
                    _lieuActuel = null;
                    MettreAJourInterface();
                    titreNotif = "⛔ Lieu Inconnu";
                    messageNotif = $"IP : {myIp}\nAucune zone correspondante.";
                    iconeNotif = BalloonIcon.Warning;
                }
            }
            else
            {
                titreNotif = "Erreur";
                messageNotif = "Impossible de récupérer la configuration.";
                iconeNotif = BalloonIcon.Error;
                shouldNotify = true;
            }

            if (_notifyIcon != null && shouldNotify)
            {
                _notifyIcon.ShowBalloonTip(titreNotif, messageNotif, iconeNotif);
            }
        }

        private void MettreAJourInterface()
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
                _mainViewModel.ChargerDonnees(nomLieu, codeLieu, imprimantesDuLieu, _ipActuelle);
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

        // --- GESTION DES MENUS CORRIGÉE (Sécurité) ---

        private void MenuOptions_Click(object sender, RoutedEventArgs e)
        {
            // SÉCURITÉ : Si la fenêtre a été fermée (Alt+F4) ou n'existe pas, on la recrée
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

                // --- AJOUT CRITIQUE : SYNCHRONISATION ---
                // Quand la fenêtre de gestion se ferme, on rafraîchit la principale
                _manageWindow.Closed += (s, args) =>
                {
                    // On met à jour le ViewModel principal
                    _mainViewModel?.RafraichirEtatInstallation();

                    // On libère la référence pour qu'elle soit recréée proprement la prochaine fois
                    _manageWindow = null;
                };
            }

            _manageWindow.Show();
            _manageWindow.Activate();
        }

        private void OnTrayBalloonTipClicked(object sender, RoutedEventArgs e) => AfficherFenetre();
        private void OnTrayDoubleClick(object sender, RoutedEventArgs e) => AfficherFenetre();
        private void MenuOpen_Click(object sender, RoutedEventArgs e) => AfficherFenetre();
        private void MenuExit_Click(object sender, RoutedEventArgs e) { _notifyIcon?.Dispose(); Shutdown(); }

        private void AfficherFenetre()
        {
            if (_mainWindow == null) return;
            MettreAJourInterface();

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
            // 1. Temporisation (Debounce)
            // On attend que Windows finisse sa négociation DHCP (obtenir l'IP)
            await Task.Delay(3000);

            // 2. On retourne sur le Thread Principal (UI)
            // Les événements réseau arrivent sur un thread secondaire, il faut revenir au principal
            // sinon l'application va crasher en essayant de toucher à l'interface.
            await Dispatcher.InvokeAsync(async () =>
            {
                // On lance le rafraîchissement silencieux (pas de paramètre ou false)
                await RafraichirDonneesAsync();
            });
        }

        private string? GetArgValue(string[] args, string name)
        {
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == name && i + 1 < args.Length) return args[i + 1];
            }
            return null;
        }

        protected override void OnExit(ExitEventArgs e)
        {
            _notifyIcon?.Dispose();
            base.OnExit(e);
        }
    }
}