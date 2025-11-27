using Microsoft.AspNetCore.SignalR.Client;
using System;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace Autoprint.Client.Services
{
    public class RealTimeService
    {
        private readonly HubConnection _hubConnection;
        private readonly DispatcherTimer _watchdogTimer;
        private readonly HttpClient _httpClient;
        private readonly string _pingUrl;

        // Le Jitter (Délai aléatoire)
        private readonly Random _random = new Random();
        private const int MAX_JITTER_SECONDS = 30;

        // Callbacks (Actions à exécuter)
        private Func<Task>? _refreshAction;
        private Action<bool>? _statusChangedAction; // true = Online, false = Offline

        public bool IsConnected => _hubConnection.State == HubConnectionState.Connected;

        public RealTimeService(string baseUrl)
        {
            // 1. Config du Watchdog (30s)
            _watchdogTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(30) };
            _watchdogTimer.Tick += async (s, e) => await CheckConnectivityAsync();

            // 2. Config HTTP pour le Ping
            _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
            _pingUrl = $"{baseUrl.TrimEnd('/')}/api/settings/public"; // Une route légère

            // 3. Config SignalR
            _hubConnection = new HubConnectionBuilder()
                .WithUrl($"{baseUrl.TrimEnd('/')}/hubs/events")
                .WithAutomaticReconnect(new[] { TimeSpan.Zero, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(30) }) // Retry natif
                .Build();

            // 4. Écoute du signal "RefreshPrinters"
            _hubConnection.On("RefreshPrinters", async () =>
            {
                await ApplyJitterAndRefreshAsync();
            });

            // 5. Gestion des coupures SignalR
            _hubConnection.Closed += async (error) =>
            {
                NotifyStatus(false); // Passe en rouge
                _watchdogTimer.Start(); // Démarre le Watchdog
                await Task.CompletedTask;
            };

            _hubConnection.Reconnected += async (connectionId) =>
            {
                NotifyStatus(true); // Repasse en vert
                _watchdogTimer.Stop();
                await ApplyJitterAndRefreshAsync(); // On se met à jour au cas où
            };
        }

        public void Initialize(Func<Task> onRefreshRequested, Action<bool> onStatusChanged)
        {
            _refreshAction = onRefreshRequested;
            _statusChangedAction = onStatusChanged;
        }

        public async Task StartAsync()
        {
            try
            {
                await _hubConnection.StartAsync();
                NotifyStatus(true);
                _watchdogTimer.Stop(); // Pas besoin si connecté
            }
            catch
            {
                NotifyStatus(false);
                _watchdogTimer.Start(); // Démarrage immédiat du Watchdog
            }
        }

        private async Task CheckConnectivityAsync()
        {
            // Le Watchdog tente un Ping HTTP simple
            try
            {
                var response = await _httpClient.GetAsync(_pingUrl);
                if (response.IsSuccessStatusCode)
                {
                    // Le serveur est revenu ! On tente de reconnecter SignalR
                    await StartAsync();

                    // Si StartAsync réussit, le Timer s'arrête tout seul.
                    // On lance une synchro pour récupérer le retard.
                    await ApplyJitterAndRefreshAsync();
                }
            }
            catch
            {
                // Toujours KO... on attend le prochain Tick du Timer.
            }
        }

        private async Task ApplyJitterAndRefreshAsync()
        {
            if (_refreshAction == null) return;

            // Calcul du délai aléatoire (0 à 30s) pour éviter l'effet de meute
            int delaySeconds = _random.Next(0, MAX_JITTER_SECONDS);

            // On attend...
            await Task.Delay(TimeSpan.FromSeconds(delaySeconds));

            // On exécute la synchro (sur le Thread UI si besoin, mais Task gère ça)
            await _refreshAction.Invoke();
        }

        private void NotifyStatus(bool isOnline)
        {
            // On s'assure que ça tourne sur le Thread UI
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                _statusChangedAction?.Invoke(isOnline);
            });
        }
    }
}