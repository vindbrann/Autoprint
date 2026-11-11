using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO.Pipes;
using System.Linq;
using System.Net.NetworkInformation;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Autoprint.Client.Services;
using Autoprint.Client.ViewModels;
using Autoprint.Shared;
using Autoprint.Shared.Enums;
using Hardcodet.Wpf.TaskbarNotification;
using Microsoft.Win32;

namespace Autoprint.Client
{
    public partial class App : Application
    {
        private static Mutex? _instanceMutex;
        private readonly string _pipeName = $"AutoprintClient_WakeUp_{Environment.UserName}";

        private TaskbarIcon? _notifyIcon;
        private readonly PathService _pathService = new PathService();
        private readonly NetworkService _networkService = new NetworkService();
        private readonly ConfigurationService _configService = new ConfigurationService();
        private readonly UserPreferencesService _prefService = new UserPreferencesService();
        private ApiService? _apiService;
        private DataService? _dataService;
        private RealTimeService? _realTimeService;
        private MainWindow? _mainWindow;
        private MainWindowViewModel? _mainViewModel;

        private string _ipActuelle = "Inconnue";
        private bool _estHorsLigne = false;
        private string? _lastLocationCode;

        private readonly SemaphoreSlim _verrouRafraichissement = new SemaphoreSlim(1, 1);

        protected override async void OnStartup(StartupEventArgs e)
        {
            _instanceMutex = new Mutex(true, $@"Local\AutoprintClient_Mutex_{Environment.UserName}", out bool createdNew);

            if (!createdNew)
            {
                bool isSilentStart = e.Args.Contains("--firstrun") || e.Args.Contains("--silent");
                if (!isSilentStart) await SendWakeUpSignalAsync();
                Current.Shutdown();
                return;
            }

            _ = ListenForWakeUpSignalsAsync();

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

            try
            {
                _configService.Initialize(e.Args, _prefService);

                _mainViewModel = new MainWindowViewModel(_prefService, _configService);
                _mainWindow = new MainWindow { DataContext = _mainViewModel };

                string serverInput = _configService.PrintServerName;
                if (string.IsNullOrWhiteSpace(serverInput)) serverInput = "localhost";
                string baseUrl = serverInput.StartsWith("http", StringComparison.OrdinalIgnoreCase) ? serverInput : $"https://{serverInput}";

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
                        _ = Dispatcher.InvokeAsync(async () => await RafraichirDonneesAsync(false));
                    });
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
            await _verrouRafraichissement.WaitAsync();
            try
            {
                string targetServer = _configService.PrintServerName ?? "localhost";
                _ipActuelle = _networkService.GetActiveLocalIpAddress(targetServer);

                List<Emplacement> lieux = new();
                List<Imprimante> imprimantes = new();
                try
                {
                    if (_apiService != null)
                    {
                        var taskLieux = _apiService.GetLieuxAsync();
                        var taskImprimantes = _apiService.GetImprimantesAsync();
                        await Task.WhenAll(taskLieux, taskImprimantes);

                        lieux = taskLieux.Result;
                        imprimantes = taskImprimantes.Result;
                        if (lieux.Count == 0) throw new Exception("API vide");
                        if (_dataService != null) await _dataService.UpdateCacheAsync(lieux, imprimantes);
                        _estHorsLigne = false;
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Synchro KO : {ex.Message} -> Utilisation du cache.");
                    _estHorsLigne = true;
                    if (_dataService != null)
                    {
                        lieux = await _dataService.GetEmplacementsAsync();
                        imprimantes = await _dataService.GetImprimantesAsync();
                    }
                }

                lieux = lieux.Where(l => l.Status == LieuStatus.Active).ToList();
                imprimantes = imprimantes.Where(i => i.Status == PrinterStatus.Synchronized).DistinctBy(i => i.Id).ToList();

                // Lieu du poste : celui dont un réseau contient l'adresse IP (réseau principal en priorité)
                var compatibles = new List<Emplacement>();
                if (!string.IsNullOrEmpty(_ipActuelle) && _ipActuelle != "127.0.0.1")
                    compatibles = lieux.Where(l => l.Networks != null && l.Networks.Any(n => IpHelper.IsIpInCidr(_ipActuelle, n.CidrIpv4))).ToList();

                var principaux = compatibles.Where(l => l.Networks.Any(n => n.IsPrimary && IpHelper.IsIpInCidr(_ipActuelle, n.CidrIpv4))).ToList();
                var lieuxActuels = principaux.Any() ? principaux : compatibles;
                var lieuActuel = lieuxActuels.FirstOrDefault();

                _mainViewModel?.Load(lieuxActuels, lieux, imprimantes, _estHorsLigne);

                // Changement de site : imprimante par défaut du site et notification discrète (panneau fermé seulement)
                string? code = lieuActuel?.Code;
                bool siteChange = estDemarrage || !string.Equals(code, _lastLocationCode, StringComparison.OrdinalIgnoreCase);
                _lastLocationCode = code;
                if (!siteChange || lieuActuel == null) return;

                string? fav = !string.IsNullOrEmpty(code) && _prefService.Current.PreferredPrinters.TryGetValue(code, out var f) ? f : null;
                bool bascule = fav != null && _prefService.Current.AutoSwitchDefaultPrinter && SetDefaultPrinterSafe(fav);

                // Au démarrage, pas de notification si l'on est sur le même site qu'à la dernière session
                bool nouveauSite = !string.Equals(_prefService.Current.LastDetectedLocationCode, code, StringComparison.OrdinalIgnoreCase);
                if (nouveauSite)
                {
                    _prefService.Current.LastDetectedLocationCode = code;
                    _prefService.Save();
                }

                bool annoncer = (!estDemarrage || nouveauSite) && !_estHorsLigne && _prefService.Current.EnableNotifications && _mainWindow?.IsVisible != true;
                if (annoncer && _notifyIcon != null)
                {
                    string message = bascule ? $"Vos documents partent sur {fav}." : "Cliquez pour choisir une imprimante sur ce site.";
                    _notifyIcon.ShowBalloonTip(lieuActuel.Nom, message, BalloonIcon.None);
                }
            }
            finally
            {
                _verrouRafraichissement.Release();
            }
        }

        /// <summary>Met l'imprimante (nom court) par défaut dans Windows, si elle est installée sur le poste.</summary>
        public bool SetDefaultPrinterSafe(string printerShortName)
        {
            var queues = PrinterService.GetNetworkQueues();
            return queues.TryGetValue(printerShortName, out var fullName) && PrinterService.SetDefault(fullName);
        }

        private void AfficherPanneau()
        {
            if (_mainWindow == null) return;
            _mainViewModel?.Rebuild();
            _mainWindow.ShowPanel();
            _ = RafraichirDonneesAsync(false);
        }

        private void MenuWindowsPrinters_Click(object sender, RoutedEventArgs e)
        {
            try { Process.Start(new ProcessStartInfo("ms-settings:printers") { UseShellExecute = true }); }
            catch { try { Process.Start(new ProcessStartInfo("control", "printers") { UseShellExecute = true }); } catch { } }
        }

        private async Task OnReseauChangeAsync()
        {
            await Task.Delay(3000);
            await Dispatcher.InvokeAsync(async () => await RafraichirDonneesAsync());
        }

        // Charte graphique (B77) : palette bleue Autoprint et dictionnaire de couleurs clair / sombre selon le thème de Windows
        private void UpdateTheme() => AutoprintTheme.Apply();

        private void MenuExit_Click(object sender, RoutedEventArgs e) { Shutdown(); }
        private void MenuOpen_Click(object sender, RoutedEventArgs e) => AfficherPanneau();
        private void OnTrayBalloonTipClicked(object sender, RoutedEventArgs e) => AfficherPanneau();

        private void OnTrayLeftClick(object sender, RoutedEventArgs e)
        {
            if (_mainWindow == null) return;
            if (_mainWindow.IsVisible) _mainWindow.HidePanel();
            else { _mainViewModel?.Rebuild(); _mainWindow.TogglePanel(); if (_mainWindow.IsVisible) _ = RafraichirDonneesAsync(false); }
        }

        protected override void OnExit(ExitEventArgs e)
        {
            // Retraits demandés juste avant de quitter : on les termine (délai borné)
            try { _mainViewModel?.CommitPendingRemovalsBlocking(TimeSpan.FromSeconds(20)); } catch { }
            _notifyIcon?.Dispose();
            base.OnExit(e);
        }

        private async Task SendWakeUpSignalAsync()
        {
            try
            {
                using var client = new NamedPipeClientStream(".", _pipeName, PipeDirection.Out);
                await client.ConnectAsync(1000);
                using var writer = new System.IO.StreamWriter(client);
                await writer.WriteAsync("WAKE_UP");
            }
            catch { }
        }

        private async Task ListenForWakeUpSignalsAsync()
        {
            while (true)
            {
                try
                {
                    using var server = new NamedPipeServerStream(_pipeName, PipeDirection.In, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
                    await server.WaitForConnectionAsync();

                    using var reader = new System.IO.StreamReader(server);
                    var msg = await reader.ReadToEndAsync();

                    if (msg == "WAKE_UP") Dispatcher.Invoke(AfficherPanneau);
                }
                catch
                {
                    await Task.Delay(1000);
                }
            }
        }
    }
}
