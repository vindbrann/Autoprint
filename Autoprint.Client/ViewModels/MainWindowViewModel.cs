using Autoprint.Client.Models;
using Autoprint.Client.Services;
using Autoprint.Shared; // Pour Emplacement et Imprimante
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Printing;
using System.Runtime.CompilerServices;
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

        // --- AJOUT : État de Connexion ---
        private bool _isOffline;
        public bool IsOffline
        {
            get => _isOffline;
            set { if (_isOffline != value) { _isOffline = value; OnPropertyChanged(); } }
        }
        // ---------------------------------

        private string _titreLieu = "Chargement...";
        public string TitreLieu
        {
            get => _titreLieu;
            set { _titreLieu = value; OnPropertyChanged(); }
        }

        public ObservableCollection<ImprimanteUiItem> Imprimantes { get; set; } = new ObservableCollection<ImprimanteUiItem>();

        public MainWindowViewModel() { /* Design mode */ }

        public MainWindowViewModel(UserPreferencesService prefService, IpcService ipcService)
        {
            _prefService = prefService;
            _ipcService = ipcService;
        }

        // --- MODIFICATION : Ajout du paramètre 'isOffline' ---
        public void ChargerDonnees(string nomLieu, string codeLieu, List<Imprimante> toutesLesImprimantes, string ipLocale, bool isOffline)
        {
            IsOffline = isOffline; // On met à jour l'état

            // On adapte le titre si on est hors ligne
            TitreLieu = nomLieu;

            var v = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
            if (v != null)
            {
                // Format v1.0.2 (Majeure.Mineure.Build)
                AppVersion = $"v{v.Major}.{v.Minor}.{v.Build}";
            }

            _codeLieuActuel = codeLieu;
            Imprimantes.Clear();

            string? favoriActuel = null;
            if (_prefService != null && !string.IsNullOrEmpty(codeLieu))
            {
                _prefService.Current.PreferredPrinters.TryGetValue(codeLieu, out favoriActuel);
            }

            var installedMap = GetInstalledPrintersMap();

            foreach (var imp in toutesLesImprimantes)
            {
                var item = new ImprimanteUiItem(imp, OnFavoriChanged, OnInstallStatusChanged);
                string nomApp = imp.NomAffiche.ToLower();

                if (IsPrinterInstalled(nomApp, installedMap))
                {
                    item.IsInstalled = true;
                }

                if (favoriActuel != null && imp.NomAffiche == favoriActuel)
                {
                    item.IsFavorite = true;
                }

                Imprimantes.Add(item);
            }
        }

        public void RafraichirEtatInstallation()
        {
            var installedMap = GetInstalledPrintersMap();

            foreach (var item in Imprimantes)
            {
                string nomApp = item.Data.NomAffiche.ToLower();
                bool estInstallee = IsPrinterInstalled(nomApp, installedMap);

                if (item.IsInstalled != estInstallee)
                {
                    item.IsInstalled = estInstallee;
                }
            }
        }

        // --- LOGIQUE SYSTÈME ---

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

        // --- ACTIONS ---

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
            // Bloquer l'installation si on est Hors Ligne (optionnel, mais recommandé)
            if (IsOffline && install)
            {
                MessageBox.Show("Impossible d'installer une imprimante en mode hors ligne.\nVeuillez vous connecter au réseau.", "Mode Hors Ligne", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (_ipcService == null || _prefService == null) return;

            string printerName = item.Data.NomAffiche;
            string? serverName = _prefService.Current.PrintServerName;
            string targetPath = $@"\\{serverName}\{printerName}";

            if (!install)
            {
                var installedMap = GetInstalledPrintersMap();
                string nomCherche = printerName.ToLower();
                var match = installedMap.FirstOrDefault(kvp =>
                    kvp.Key == nomCherche ||
                    kvp.Key.EndsWith($"\\{nomCherche}") ||
                    kvp.Key.Contains(nomCherche));

                if (!string.IsNullOrEmpty(match.Value)) targetPath = match.Value;
            }
            else
            {
                if (string.IsNullOrWhiteSpace(serverName))
                {
                    MessageBox.Show("Serveur non configuré (voir Options).", "Erreur", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
            }

            bool success = install
                ? await _ipcService.InstallPrinterAsync(printerName, targetPath)
                : await _ipcService.UninstallPrinterAsync(printerName, targetPath);

            if (success)
            {
                item.IsInstalled = install;
                if (!install)
                {
                    item.IsFavorite = false;
                    OnFavoriChanged(item);
                }
                RafraichirEtatInstallation();
            }
            else
            {
                MessageBox.Show($"Action échouée sur : {targetPath}", "Erreur Service", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public class ImprimanteUiItem : INotifyPropertyChanged
    {
        private readonly Action<ImprimanteUiItem>? _onFavoriChanged;
        private readonly Action<ImprimanteUiItem, bool>? _onInstallStatusChanged;
        public Imprimante Data { get; }
        private bool _isFavorite;
        public bool IsFavorite { get => _isFavorite; set { if (_isFavorite != value) { _isFavorite = value; OnPropertyChanged(); _onFavoriChanged?.Invoke(this); } } }
        private bool _isInstalled;
        public bool IsInstalled { get => _isInstalled; set { if (_isInstalled != value) { _isInstalled = value; OnPropertyChanged(); } } }
        public ICommand InstallCommand { get; }
        public ICommand UninstallCommand { get; }
        public ImprimanteUiItem(Imprimante data, Action<ImprimanteUiItem>? onFav, Action<ImprimanteUiItem, bool>? onInst)
        {
            Data = data; _onFavoriChanged = onFav; _onInstallStatusChanged = onInst;
            InstallCommand = new RelayCommand(_ => _onInstallStatusChanged?.Invoke(this, true));
            UninstallCommand = new RelayCommand(_ => _onInstallStatusChanged?.Invoke(this, false));
        }
        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}