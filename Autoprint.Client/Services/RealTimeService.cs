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

        private readonly Random _random = new Random();
        private const int MAX_JITTER_SECONDS = 30;

        private Func<Task>? _refreshAction;
        private Action<bool>? _statusChangedAction;

        public bool IsConnected => _hubConnection.State == HubConnectionState.Connected;

        public RealTimeService(string baseUrl)
        {
            _watchdogTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(30) };
            _watchdogTimer.Tick += async (s, e) => await CheckConnectivityAsync();

            _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
            _pingUrl = $"{baseUrl.TrimEnd('/')}/api/settings/public";

            _hubConnection = new HubConnectionBuilder()
                .WithUrl($"{baseUrl.TrimEnd('/')}/hubs/events")
                .WithAutomaticReconnect(new[] { TimeSpan.Zero, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(30) })
                .Build();

            _hubConnection.On("RefreshPrinters", async () =>
            {
                await ApplyJitterAndRefreshAsync();
            });

            _hubConnection.Closed += async (error) =>
            {
                NotifyStatus(false);
                _watchdogTimer.Start();
                await Task.CompletedTask;
            };

            _hubConnection.Reconnected += async (connectionId) =>
            {
                NotifyStatus(true);
                _watchdogTimer.Stop();
                await ApplyJitterAndRefreshAsync();
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
                _watchdogTimer.Stop();
            }
            catch (Exception ex)
            {
                NotifyStatus(false);
                _watchdogTimer.Start();
                System.Diagnostics.Debug.WriteLine($"ERREUR SIGNALR : {ex.Message}");
            }
        }

        private async Task CheckConnectivityAsync()
        {
            try
            {
                var response = await _httpClient.GetAsync(_pingUrl);
                if (response.IsSuccessStatusCode)
                {
                    await StartAsync();
                    await ApplyJitterAndRefreshAsync();
                }
            }
            catch {  }
        }

        private async Task ApplyJitterAndRefreshAsync()
        {
            if (_refreshAction == null) return;
            int delaySeconds = _random.Next(0, MAX_JITTER_SECONDS);
            await Task.Delay(TimeSpan.FromSeconds(delaySeconds));
            await _refreshAction.Invoke();
        }

        private void NotifyStatus(bool isOnline)
        {
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                _statusChangedAction?.Invoke(isOnline);
            });
        }
    }
}