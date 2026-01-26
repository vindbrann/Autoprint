using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Net.NetworkInformation;
using System.Printing;
using System.Runtime.InteropServices;
using System.Threading; // <--- Nécessaire pour SemaphoreSlim
using System.Threading.Tasks;
using System.Windows;
using Autoprint.Client.Models;
using Autoprint.Client.Services;
using Autoprint.Client.ViewModels;
using Autoprint.Shared;
using Autoprint.Shared.Enums;
using Hardcodet.Wpf.TaskbarNotification;
using MaterialDesignThemes.Wpf;
using Microsoft.Win32;

namespace Autoprint.Client
{
    public partial class App : Application
    {
        [DllImport("winspool.drv", CharSet = CharSet.Auto, SetLastError = true)]
        public static extern bool SetDefaultPrinter(string Name);

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

        // Variables Anti-Spam (Mémoire)
        private string _lastLocationCode = string.Empty;
        private string _lastPrinterSignature = string.Empty;

        // 🔒 LE VERROU (SEMAPHORE)
        // Permet d'assurer qu'une seule mise à jour se fait à la fois
        private readonly SemaphoreSlim _verrouRafraichissement = new SemaphoreSlim(1, 1);

        protected override async void OnStartup(StartupEventArgs e)
        {
            SystemEvents.UserPreferenceChanged += (s, args) =>
            {
                if (args.Category == UserPreferenceCategory.General) UpdateTheme();
            };
            UpdateTheme();

            base.OnStartup(e);

            if (e.Args.Contains("--firstrun"))
            {
                SetupAutoStart();
            }

            try
            {
                _notifyIcon = (TaskbarIcon)FindResource("MainNotifyIcon");
                if (_notifyIcon != null)
                    _notifyIcon.IconSource = new System.Windows.Media.Imaging.BitmapImage(new Uri("pack://application:,,,/Autoprint.ico"));
            }
            catch { }

            _mainViewModel = new MainWindowViewModel(_prefService, _ipcService, _configService);
            _mainWindow = new MainWindow();
            _mainWindow.DataContext = _mainViewModel;

            _optionsViewModel = new OptionsViewModel(_prefService, _ipcService, _configService);
            _manageViewModel = new ManagePrintersViewModel(_prefService);

            try
            {
                _configService.Initialize(e.Args, _prefService);

                string serverInput = _configService.PrintServerName;
                if (string.IsNullOrWhiteSpace(serverInput)) serverInput = "localhost";

                string baseUrl = serverInput.StartsWith("http", StringComparison.OrdinalIgnoreCase)
                                ? serverInput
                                : $"https://{serverInput}";

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
                        // On lance une maj d'interface, mais attention aux threads, on passe par Rafraichir pour la sécurité
                        _ = RafraichirDonneesAsync(false);
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
            // 🔒 ENTREE DU SAS DE SÉCURITÉ
            // Si une mise à jour est déjà en cours, on attend qu'elle finisse.
            await _verrouRafraichissement.WaitAsync();

            try
            {
                string targetServer = _configService.PrintServerName ?? "localhost";
                _ipActuelle = _networkService.GetActiveLocalIpAddress(targetServer);

                List<Emplacement> lieux = new List<Emplacement>();

                // --- 1. RÉCUPÉRATION DES DONNÉES ---
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
                    Debug.WriteLine($"Synchro KO : {ex.Message} -> Utilisation du cache.");
                    if (_dataService != null)
                    {
                        lieux = await _dataService.GetEmplacementsAsync();
                        _toutesLesImprimantes = await _dataService.GetImprimantesAsync();
                    }
                }

                // Filtrage global
                lieux = lieux.Where(l => l.Status == LieuStatus.Active).ToList();
                _toutesLesImprimantes = _toutesLesImprimantes
                                        .Where(i => i.Status == PrinterStatus.Synchronized)
                                        .ToList();

                // --- 2. CALCUL DU LIEU ACTUEL ---
                List<Emplacement> lieuxAffiches = new List<Emplacement>();
                List<Emplacement> tousLesLieuxCompatibles = new List<Emplacement>();

                if (lieux.Count > 0 && !string.IsNullOrEmpty(_ipActuelle) && _ipActuelle != "127.0.0.1")
                {
                    foreach (var lieu in lieux)
                    {
                        if (lieu.Networks == null) continue;
                        if (lieu.Networks.Any(net => IpHelper.IsIpInCidr(_ipActuelle, net.CidrIpv4)))
                        {
                            tousLesLieuxCompatibles.Add(lieu);
                        }
                    }
                }

                var lieuPrincipalTrouve = tousLesLieuxCompatibles.FirstOrDefault(l =>
                    l.Networks.Any(n => n.IsPrimary && IpHelper.IsIpInCidr(_ipActuelle, n.CidrIpv4)));

                if (lieuPrincipalTrouve != null)
                {
                    lieuxAffiches.Add(lieuPrincipalTrouve);
                    _lieuActuel = lieuPrincipalTrouve;
                }
                else
                {
                    lieuxAffiches = tousLesLieuxCompatibles;
                    _lieuActuel = lieuxAffiches.FirstOrDefault();
                }

                // --- 3. DÉTECTION DE CHANGEMENT (ANTI-SPAM) ---

                var idsLieux = lieuxAffiches.Select(l => l.Id).ToList();
                var imprimantesPourCeLieu = _toutesLesImprimantes
                                            .Where(i => idsLieux.Contains(i.EmplacementId))
                                            .ToList();

                // Signature unique du contexte actuel
                string newLocationCode = lieuxAffiches.FirstOrDefault()?.Code ?? "UNKNOWN";
                // On trie par ID pour que l'ordre ne fausse pas la signature
                string newPrinterSignature = string.Join(",", imprimantesPourCeLieu
                                                              .OrderBy(p => p.Id)
                                                              .Select(p => p.Id));

                bool changementDetecte = false;

                // Comparaison avec la mémoire
                if (newLocationCode != _lastLocationCode) changementDetecte = true;
                if (newPrinterSignature != _lastPrinterSignature) changementDetecte = true;

                // Mise à jour de la mémoire pour le prochain passage
                _lastLocationCode = newLocationCode;
                _lastPrinterSignature = newPrinterSignature;


                // --- 4. PRÉPARATION DE LA NOTIFICATION ---
                string titreNotif = "Autoprint";
                string messageNotif = "Mise à jour...";
                BalloonIcon iconeNotif = BalloonIcon.None;
                if (_estHorsLigne) iconeNotif = BalloonIcon.Warning;

                if (lieuxAffiches.Any())
                {
                    if (_lieuActuel != null && _prefService.Current.LastDetectedLocationCode != _lieuActuel.Code)
                    {
                        _prefService.Current.LastDetectedLocationCode = _lieuActuel.Code;
                        _prefService.Save();
                    }

                    int nbImprimantes = imprimantesPourCeLieu.Count;

                    if (lieuxAffiches.Count == 1)
                        titreNotif = $"📍 {lieuxAffiches[0].Nom}";
                    else
                        titreNotif = $"📍 Zone Partagée ({lieuxAffiches.Count} lieux)";

                    messageNotif = $"{nbImprimantes} imprimante(s) disponible(s).";

                    string messageSwitch = "";
                    foreach (var lieu in lieuxAffiches)
                    {
                        if (_prefService.Current.PreferredPrinters.TryGetValue(lieu.Code ?? "", out string? fav))
                        {
                            // On tente le switch d'imprimante par défaut uniquement si changement réel ou démarrage
                            if (estDemarrage || changementDetecte)
                            {
                                if (SetDefaultPrinterSafe(fav))
                                {
                                    messageSwitch = $"\n✅ {fav} par défaut.";
                                    break;
                                }
                            }
                        }
                    }
                    if (!string.IsNullOrEmpty(messageSwitch)) messageNotif += messageSwitch;
                }
                else
                {
                    // Cas "Lieu Inconnu"
                    if (estDemarrage || changementDetecte)
                    {
                        _lieuActuel = null;
                        titreNotif = "⛔ Lieu Inconnu";
                        messageNotif = $"IP: {_ipActuelle}\nAucun lieu ne correspond.";
                        iconeNotif = BalloonIcon.Warning;
                    }
                }

                // Mise à jour de l'interface (WPF gère son propre thread UI, donc c'est safe)
                MettreAJourInterfaceMulti(lieuxAffiches, _estHorsLigne);

                // --- 5. AFFICHAGE DE LA NOTIF (FILTRÉE) ---
                if (_notifyIcon != null && _prefService.Current.EnableNotifications)
                {
                    // LE FILTRE EST ICI :
                    // Si ce n'est pas le démarrage ET que rien n'a changé => On ne fait RIEN.
                    // Si une autre tâche parallèle arrive ici, 'changementDetecte' sera false car la mémoire a été mise à jour plus haut.
                    if (estDemarrage || changementDetecte)
                    {
                        // Petite sécurité pour ne pas afficher une bulle vide
                        if (!string.IsNullOrEmpty(messageNotif))
                        {
                            _notifyIcon.ShowBalloonTip(titreNotif, messageNotif, iconeNotif);
                        }
                    }
                    else
                    {
                        Debug.WriteLine("[Refresh] Données identiques, notification ignorée (Anti-Spam).");
                    }
                }
            }
            finally
            {
                // 🔓 SORTIE DU SAS : On libère la place pour la prochaine demande (s'il y en a une)
                _verrouRafraichissement.Release();
            }
        }

        private void SetupAutoStart()
        {
            try
            {
                string exePath = Process.GetCurrentProcess().MainModule?.FileName;
                if (string.IsNullOrEmpty(exePath)) return;

                using var key = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", true);
                if (key != null)
                {
                    key.SetValue("AutoprintClient", $"\"{exePath}\"");
                    Debug.WriteLine("[FirstRun] Inscription démarrage auto réussie.");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[FirstRun] Erreur registre : {ex.Message}");
            }
        }

        private void MettreAJourInterfaceMulti(List<Emplacement> lieux, bool isOffline)
        {
            if (_mainViewModel == null) return;

            string nomAffiche;
            string codeAffiche;
            List<Imprimante> imprimantesFinales = new List<Imprimante>();

            if (lieux.Any())
            {
                if (lieux.Count == 1)
                {
                    nomAffiche = lieux[0].Nom;
                    codeAffiche = lieux[0].Code ?? "";
                }
                else
                {
                    nomAffiche = string.Join(" + ", lieux.Select(l => l.Nom));
                    codeAffiche = "MULTI";
                }

                var ids = lieux.Select(l => l.Id).ToList();
                imprimantesFinales = _toutesLesImprimantes
                                     .Where(i => ids.Contains(i.EmplacementId))
                                     .ToList();
            }
            else
            {
                nomAffiche = "Lieu Inconnu";
                codeAffiche = "";
            }

            Application.Current.Dispatcher.Invoke(() =>
            {
                _mainViewModel.ChargerDonnees(nomAffiche, codeAffiche, imprimantesFinales, _ipActuelle, isOffline);
            });
        }

        public bool SetDefaultPrinterSafe(string printerShortName)
        {
            try
            {
                string? serverName = _configService.PrintServerName;
                if (string.IsNullOrEmpty(serverName)) return false;

                serverName = serverName.Replace("https://", "").Replace("http://", "").Trim('/');
                string targetUncName = $@"\\{serverName}\{printerShortName}";
                bool isInstalled = false;

                try
                {
                    using (var printServer = new LocalPrintServer())
                    {
                        var queues = printServer.GetPrintQueues(new[] { EnumeratedPrintQueueTypes.Local, EnumeratedPrintQueueTypes.Connections });
                        foreach (var q in queues)
                        {
                            if (q.FullName.Equals(targetUncName, StringComparison.OrdinalIgnoreCase) ||
                                q.Name.Equals(printerShortName, StringComparison.OrdinalIgnoreCase))
                            {
                                isInstalled = true;
                                targetUncName = q.FullName;
                                break;
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Erreur lecture Spouleur : {ex.Message}");
                    isInstalled = false;
                }

                if (!isInstalled) return false;

                return SetDefaultPrinter(targetUncName);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Erreur SetDefaultPrinter : {ex.Message}");
                return false;
            }
        }

        private void MenuOptions_Click(object sender, RoutedEventArgs e)
        {
            if (_optionsWindow == null || !_optionsWindow.IsLoaded)
                _optionsWindow = new Autoprint.Client.OptionsWindow(_prefService, _ipcService, _configService);

            _optionsWindow.Show();
            _optionsWindow.Activate();
        }

        private void MenuManage_Click(object sender, RoutedEventArgs e)
        {
            if (_manageWindow != null && _manageWindow.IsLoaded)
            {
                _manageWindow.Activate();
                return;
            }
            _manageWindow = new ManagePrintersWindow(_prefService);
            _manageWindow.Closed += (s, args) =>
            {
                _manageWindow = null;
                _mainViewModel?.RafraichirEtatInstallation();
            };
            _manageWindow.Show();
        }

        private void MenuWindowsPrinters_Click(object sender, RoutedEventArgs e)
        {
            try { Process.Start(new ProcessStartInfo("ms-settings:printers") { UseShellExecute = true }); }
            catch { try { Process.Start(new ProcessStartInfo("control", "printers") { UseShellExecute = true }); } catch { } }
        }

        private void AfficherFenetre()
        {
            if (_mainWindow == null) return;
            MettreAJourInterfaceMulti(new List<Emplacement>(), _estHorsLigne);
            _ = RafraichirDonneesAsync(false);
            _mainViewModel?.RafraichirEtatInstallation();

            if (_mainWindow.Visibility == Visibility.Visible) _mainWindow.Activate();
            else _mainWindow.Show();
        }

        private async Task OnReseauChangeAsync()
        {
            await Task.Delay(3000);
            await Dispatcher.InvokeAsync(async () => await RafraichirDonneesAsync());
        }

        private void UpdateTheme()
        {
            var paletteHelper = new PaletteHelper();
            var theme = paletteHelper.GetTheme();
            var systemTheme = Theme.GetSystemTheme() ?? BaseTheme.Light;
            theme.SetBaseTheme(systemTheme);
            paletteHelper.SetTheme(theme);
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