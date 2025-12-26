using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Net.NetworkInformation;
using System.Printing;
using System.Runtime.InteropServices;
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
            _manageViewModel = new ManagePrintersViewModel(_prefService);

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
            var mesIps = _networkService.GetAllLocalIpAddresses();
            _ipActuelle = mesIps.FirstOrDefault() ?? "Non connecté";

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
                Debug.WriteLine($"Synchro KO : {ex.Message} -> Utilisation du cache.");
                if (_dataService != null)
                {
                    lieux = await _dataService.GetEmplacementsAsync();
                    _toutesLesImprimantes = await _dataService.GetImprimantesAsync();
                }
            }

            lieux = lieux.Where(l => l.Status == LieuStatus.Active).ToList();
            _toutesLesImprimantes = _toutesLesImprimantes
                                    .Where(i => i.Status == PrinterStatus.Synchronized)
                                    .ToList();

            Emplacement? meilleurLieu = null;
            string meilleureIp = _ipActuelle;
            int meilleurScoreCidr = -1;

            if (lieux.Count > 0 && mesIps.Count > 0)
            {
                foreach (var ip in mesIps)
                {
                    foreach (var lieu in lieux)
                    {
                        if (IpHelper.IsIpInCidr(ip, lieu.CidrIpv4))
                        {
                            int score = 0;
                            if (lieu.CidrIpv4 != null && lieu.CidrIpv4.Contains("/"))
                                int.TryParse(lieu.CidrIpv4.Split('/')[1], out score);

                            if (score > meilleurScoreCidr)
                            {
                                meilleurScoreCidr = score;
                                meilleurLieu = lieu;
                                meilleureIp = ip;
                            }
                        }
                    }
                }
            }

            string titreNotif = "Autoprint";
            string messageNotif = "Mise à jour...";
            BalloonIcon iconeNotif = BalloonIcon.None;
            bool lieuAChange = false;

            if (_estHorsLigne) iconeNotif = BalloonIcon.Warning;

            if (meilleurLieu != null)
            {
                _ipActuelle = meilleureIp;

                if (_lieuActuel?.Code != meilleurLieu.Code) lieuAChange = true;

                _lieuActuel = meilleurLieu;

                if (_prefService.Current.LastDetectedLocationCode != meilleurLieu.Code)
                {
                    _prefService.Current.LastDetectedLocationCode = meilleurLieu.Code;
                    _prefService.Save();
                }

                int nbImprimantesZone = _toutesLesImprimantes.Count(i =>
                    i.EmplacementId == meilleurLieu.Id ||
                    (i.Emplacement != null && i.Emplacement.Id == meilleurLieu.Id));

                titreNotif = $"📍 Lieu : {meilleurLieu.Nom}";

                string messageSwitch = "";

                if (_prefService.Current.PreferredPrinters.TryGetValue(meilleurLieu.Code, out string? favoritePrinterName))
                {
                    bool favoriExisteSurServeur = _toutesLesImprimantes.Any(p => p.NomAffiche == favoritePrinterName);

                    if (!favoriExisteSurServeur)
                    {
                        Debug.WriteLine($"[Startup] Le favori '{favoritePrinterName}' n'existe plus sur le serveur. Suppression.");
                        _prefService.RemovePreferredPrinter(meilleurLieu.Code);
                    }
                    else
                    {
                        if (_prefService.Current.AutoSwitchDefaultPrinter && (lieuAChange || estDemarrage))
                        {
                            if (SetDefaultPrinterSafe(favoritePrinterName))
                                messageSwitch = $"\n✅ {favoritePrinterName} par défaut.";
                        }
                    }
                }

                if (!string.IsNullOrEmpty(messageSwitch))
                    messageNotif = $"Bienvenue à {meilleurLieu.Nom}{messageSwitch}";
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
                messageNotif = $"IPs: {string.Join(", ", mesIps)}\n(Lieu non reconnu)";
                iconeNotif = BalloonIcon.Warning;
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

        public bool SetDefaultPrinterSafe(string printerShortName)
        {
            try
            {
                string? serverName = _prefService.Current.PrintServerName;

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

                if (!isInstalled)
                {
                    if (_notifyIcon != null && _prefService.Current.EnableNotifications)
                    {
                        Debug.WriteLine($"[Switch] Impossible de mettre par défaut : '{printerShortName}' non trouvée.");
                    }
                    return false;
                }

                bool result = SetDefaultPrinter(targetUncName);

                if (result)
                {
                    Debug.WriteLine($"[Switch] Succès : '{targetUncName}' est maintenant par défaut.");
                    return true;
                }
                else
                {
                    int errorCode = Marshal.GetLastWin32Error();
                    Debug.WriteLine($"[Switch] Échec API SetDefaultPrinter. Code: {errorCode}");
                    return false;
                }
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
            {
                _optionsWindow = new Autoprint.Client.OptionsWindow(_prefService);
                _optionsWindow.DataContext = _optionsViewModel;
            }
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
            try
            {
                Process.Start(new ProcessStartInfo("ms-settings:printers") { UseShellExecute = true });
            }
            catch
            {
                try { Process.Start(new ProcessStartInfo("control", "printers") { UseShellExecute = true }); } catch { }
            }
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