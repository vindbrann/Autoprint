using Autoprint.Client.Models;
using Autoprint.Client.Services;
using Autoprint.Shared;
using Autoprint.Shared.IPC;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Printing;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace Autoprint.Client.ViewModels
{
    public class MainWindowViewModel : INotifyPropertyChanged
    {
        private readonly UserPreferencesService? _prefService;
        private readonly IpcService? _ipcService;

        private string _appVersion = "v1.0";
        public string AppVersion
        {
            get => _appVersion;
            set { if (_appVersion != value) { _appVersion = value; OnPropertyChanged(); } }
        }

        private string _codeLieuActuel = string.Empty;
        private bool _isUpdatingFavorites = false;
        private string _titreLieu = "Chargement...";
        public string TitreLieu
        {
            get => _titreLieu;
            set { _titreLieu = value; OnPropertyChanged(); }
        }

        private bool _isOffline;
        public bool IsOffline
        {
            get => _isOffline;
            set { if (_isOffline != value) { _isOffline = value; OnPropertyChanged(); OnPropertyChanged(nameof(CanInstall)); } }
        }

        private bool _isSmbAvailable = true;
        public bool IsSmbAvailable
        {
            get => _isSmbAvailable;
            set { if (_isSmbAvailable != value) { _isSmbAvailable = value; OnPropertyChanged(); OnPropertyChanged(nameof(CanInstall)); } }
        }

        public bool CanInstall => !IsOffline && IsSmbAvailable;

        public ObservableCollection<ImprimanteUiItem> Imprimantes { get; set; } = new ObservableCollection<ImprimanteUiItem>();

        public MainWindowViewModel() { }

        public MainWindowViewModel(UserPreferencesService prefService, IpcService ipcService)
        {
            _prefService = prefService;
            _ipcService = ipcService;
        }

        public void ChargerDonnees(string nomLieu, string codeLieu, List<Imprimante> toutesLesImprimantes, string ipLocale, bool isOffline)
        {
            IsOffline = isOffline;
            TitreLieu = nomLieu;

            if (!isOffline && _prefService?.Current.PrintServerName != null)
            {
                _ = CheckSmbAccessAsync(_prefService.Current.PrintServerName);
            }

            var v = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
            if (v != null) AppVersion = $"v{v.Major}.{v.Minor}.{v.Build}";

            _codeLieuActuel = codeLieu;
            Imprimantes.Clear();

            string? favoriActuel = null;
            if (_prefService != null && !string.IsNullOrEmpty(codeLieu))
            {
                if (_prefService.Current.PreferredPrinters != null)
                    _prefService.Current.PreferredPrinters.TryGetValue(codeLieu, out favoriActuel);
            }

            var installedMap = GetInstalledPrintersMap();

            if (toutesLesImprimantes == null) toutesLesImprimantes = new List<Imprimante>();

            foreach (var imp in toutesLesImprimantes)
            {
                var item = new ImprimanteUiItem(imp, this, OnFavoriChanged, OnInstallStatusChanged);
                string nomApp = imp.NomAffiche.ToLower();

                if (IsPrinterInstalled(nomApp, installedMap)) item.IsInstalled = true;
                if (favoriActuel != null && imp.NomAffiche == favoriActuel) item.IsFavorite = true;

                Imprimantes.Add(item);
            }
        }

        private async Task CheckSmbAccessAsync(string serverName)
        {
            try
            {
                await Task.Run(() =>
                {
                    string testPath = $@"\\{serverName}\print$";
                    bool accessOk = Directory.Exists(testPath);
                    Application.Current.Dispatcher.Invoke(() => IsSmbAvailable = accessOk);
                });
            }
            catch { IsSmbAvailable = false; }
        }

        public void RafraichirEtatInstallation()
        {
            var installedMap = GetInstalledPrintersMap();
            foreach (var item in Imprimantes)
            {
                string nomApp = item.Data.NomAffiche.ToLower();
                bool estInstallee = IsPrinterInstalled(nomApp, installedMap);
                if (item.IsInstalled != estInstallee) item.IsInstalled = estInstallee;
            }
        }

        private Dictionary<string, string> GetInstalledPrintersMap()
        {
            var map = new Dictionary<string, string>();
            try
            {
                using (var printServer = new LocalPrintServer())
                {
                    var queues = printServer.GetPrintQueues(new[] { EnumeratedPrintQueueTypes.Local, EnumeratedPrintQueueTypes.Connections });
                    foreach (var q in queues)
                    {
                        string name = q.Name.ToLower();
                        string path = !string.IsNullOrEmpty(q.FullName) ? q.FullName : q.Name;
                        if (!map.ContainsKey(name)) map.Add(name, path);
                    }
                }
            }
            catch { }
            return map;
        }

        private bool IsPrinterInstalled(string nomApp, Dictionary<string, string> map)
        {
            if (map.ContainsKey(nomApp)) return true;
            return map.Keys.Any(k => k.EndsWith($"\\{nomApp}") || k.Contains(nomApp));
        }

        private void OnFavoriChanged(ImprimanteUiItem itemClicked)
        {
            if (_prefService == null || string.IsNullOrEmpty(_codeLieuActuel) || _isUpdatingFavorites) return;
            try
            {
                _isUpdatingFavorites = true;
                if (itemClicked.IsFavorite)
                {
                    foreach (var item in Imprimantes) if (item != itemClicked) item.IsFavorite = false;
                    _prefService.SetPreferredPrinter(_codeLieuActuel, itemClicked.Data.NomAffiche);
                }
                else
                {
                    _prefService.RemovePreferredPrinter(_codeLieuActuel);
                }
            }
            finally { _isUpdatingFavorites = false; }
        }

        private async void OnInstallStatusChanged(ImprimanteUiItem item, bool install)
        {
            if (install && !CanInstall)
            {
                MessageBox.Show("Le serveur d'impression est inaccessible (SMB).\nVérifiez votre connexion.", "Erreur Réseau", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (_ipcService == null || _prefService == null) return;

            string printerName = item.Data.NomAffiche;
            string driverName = item.Data.Modele?.Pilote?.Nom ?? "INCONNU";
            string? serverName = _prefService.Current.PrintServerName;

            if (string.IsNullOrWhiteSpace(serverName)) return;

            string uncPath = $@"\\{serverName}\{printerName}";

            if (install)
            {
                var request = new IpcRequest
                {
                    Action = "INSTALL_DRIVER",
                    PrinterName = printerName,
                    DriverModelName = driverName,
                    UncPath = uncPath
                };
                await _ipcService.SendRequestAsync(request);

                bool mapSuccess = RunUserCommand($"printui.dll,PrintUIEntry /in /n \"{uncPath}\"");

                if (mapSuccess)
                {
                    item.IsInstalled = true;
                    RafraichirEtatInstallation();
                }
                else
                {
                    MessageBox.Show("Échec de la connexion à l'imprimante.", "Erreur", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            else
            {
                if (RunUserCommand($"printui.dll,PrintUIEntry /dn /q /n \"{uncPath}\""))
                {
                    item.IsInstalled = false;
                    item.IsFavorite = false;
                    RafraichirEtatInstallation();
                }
            }
        }

        private bool RunUserCommand(string arguments)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "rundll32.exe",
                    Arguments = arguments,
                    UseShellExecute = false
                };
                var p = Process.Start(psi);
                p?.WaitForExit();
                return p?.ExitCode == 0;
            }
            catch { return false; }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public class ImprimanteUiItem : INotifyPropertyChanged
    {
        private readonly MainWindowViewModel _parent;
        private readonly Action<ImprimanteUiItem>? _onFavoriChanged;
        private readonly Action<ImprimanteUiItem, bool>? _onInstallStatusChanged;

        public Imprimante Data { get; }

        private bool _isFavorite;
        public bool IsFavorite
        {
            get => _isFavorite;
            set { if (_isFavorite != value) { _isFavorite = value; OnPropertyChanged(); _onFavoriChanged?.Invoke(this); } }
        }

        private bool _isInstalled;
        public bool IsInstalled
        {
            get => _isInstalled;
            set { if (_isInstalled != value) { _isInstalled = value; OnPropertyChanged(); } }
        }

        public ICommand InstallCommand { get; }
        public ICommand UninstallCommand { get; }

        public ImprimanteUiItem(Imprimante data, MainWindowViewModel parent, Action<ImprimanteUiItem>? onFav, Action<ImprimanteUiItem, bool>? onInst)
        {
            Data = data;
            _parent = parent;
            _onFavoriChanged = onFav;
            _onInstallStatusChanged = onInst;

            InstallCommand = new RelayCommand(_ => _onInstallStatusChanged?.Invoke(this, true), _ => _parent.CanInstall);
            UninstallCommand = new RelayCommand(_ => _onInstallStatusChanged?.Invoke(this, false));
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}