using Autoprint.Client.Models;
using Autoprint.Client.Services;
using Autoprint.Shared;
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

        private string _codeLieuActuel = string.Empty;
        private bool _isUpdatingFavorites = false;

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

        public void ChargerDonnees(string nomLieu, string codeLieu, List<Imprimante> toutesLesImprimantes, string ipLocale)
        {
            TitreLieu = $"📍 Lieu : {nomLieu}";
            _codeLieuActuel = codeLieu;

            Imprimantes.Clear();

            string? favoriActuel = null;
            if (_prefService != null && !string.IsNullOrEmpty(codeLieu))
            {
                _prefService.Current.PreferredPrinters.TryGetValue(codeLieu, out favoriActuel);
            }

            // On récupère le dictionnaire : Clé=NomSimple, Valeur=VraiCheminUNC
            var installedMap = GetInstalledPrintersMap();

            foreach (var imp in toutesLesImprimantes)
            {
                var item = new ImprimanteUiItem(imp, OnFavoriChanged, OnInstallStatusChanged);
                string nomApp = imp.NomAffiche.ToLower();

                // Vérification intelligente
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

        // --- LOGIQUE DE RECHERCHE ---

        // Retourne un Dictionnaire { "nom_imprimante" : "\\serveur\nom_imprimante" }
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
                        // On stocke le chemin complet s'il existe, sinon le nom simple
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
            // 1. Correspondance exacte
            if (map.ContainsKey(nomApp)) return true;

            // 2. Correspondance partielle (si le nom Windows finit par le nom App)
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
            if (_ipcService == null || _prefService == null) return;

            string printerName = item.Data.NomAffiche;
            string? serverName = _prefService.Current.PrintServerName;

            // Chemin par défaut (construit) pour l'installation
            string targetPath = $@"\\{serverName}\{printerName}";

            // POUR LA DÉSINSTALLATION : ON CHERCHE LE VRAI CHEMIN
            if (!install)
            {
                var installedMap = GetInstalledPrintersMap();
                string nomCherche = printerName.ToLower();

                // On cherche le vrai chemin UNC connu de Windows
                var match = installedMap.FirstOrDefault(kvp =>
                    kvp.Key == nomCherche ||
                    kvp.Key.EndsWith($"\\{nomCherche}") ||
                    kvp.Key.Contains(nomCherche));

                if (!string.IsNullOrEmpty(match.Value))
                {
                    targetPath = match.Value; // On utilise le vrai chemin trouvé !
                }
            }
            else
            {
                // Pour l'installation, on vérifie quand même la config
                if (string.IsNullOrWhiteSpace(serverName))
                {
                    MessageBox.Show("Serveur non configuré (voir Options).", "Erreur", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
            }

            bool success = false;

            if (install)
                success = await _ipcService.InstallPrinterAsync(printerName, targetPath);
            else
                success = await _ipcService.UninstallPrinterAsync(printerName, targetPath);

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