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
        private readonly ConfigurationService? _configService;

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
            set
            {
                if (_isOffline != value)
                {
                    _isOffline = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(CanInstall));
                    Application.Current.Dispatcher.Invoke(CommandManager.InvalidateRequerySuggested);
                }
            }
        }

        private bool _isSmbAvailable = true;
        public bool IsSmbAvailable
        {
            get => _isSmbAvailable;
            set
            {
                if (_isSmbAvailable != value)
                {
                    _isSmbAvailable = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(CanInstall));
                    Application.Current.Dispatcher.Invoke(CommandManager.InvalidateRequerySuggested);
                }
            }
        }

        public bool CanInstall => !IsOffline && IsSmbAvailable;

        public ObservableCollection<ImprimanteUiItem> Imprimantes { get; set; } = new ObservableCollection<ImprimanteUiItem>();

        public MainWindowViewModel() { }

        public MainWindowViewModel(UserPreferencesService prefService, IpcService ipcService, ConfigurationService configService)
        {
            _prefService = prefService;
            _ipcService = ipcService;
            _configService = configService;
        }

        public void ChargerDonnees(string nomLieu, string codeLieu, List<Imprimante> toutesLesImprimantes, string ipLocale, bool isOffline)
        {
            _isUpdatingFavorites = true;

            try
            {
                IsOffline = isOffline;
                TitreLieu = nomLieu;

                string serverName = _configService?.PrintServerName ?? "";

                if (!isOffline && !string.IsNullOrEmpty(serverName))
                {
                    _ = CheckSmbAccessAsync(serverName);
                }

                var assembly = System.Reflection.Assembly.GetEntryAssembly();
                var infoAttr = assembly?.GetCustomAttributes(typeof(System.Reflection.AssemblyInformationalVersionAttribute), false).FirstOrDefault() as System.Reflection.AssemblyInformationalVersionAttribute;

                if (infoAttr != null)
                {
                    string versionRaw = infoAttr.InformationalVersion;
                    if (versionRaw.Contains("+")) versionRaw = versionRaw.Split('+')[0];
                    AppVersion = $"v{versionRaw}";
                }

                _codeLieuActuel = codeLieu;
                Imprimantes.Clear();

                string? favoriActuel = null;
                if (_prefService != null && !string.IsNullOrEmpty(codeLieu))
                {
                    if (_prefService.Current.PreferredPrinters != null)
                        _prefService.Current.PreferredPrinters.TryGetValue(codeLieu, out favoriActuel);
                }

                if (toutesLesImprimantes == null) toutesLesImprimantes = new List<Imprimante>();

                if (favoriActuel != null)
                {
                    bool favoriExisteEncore = toutesLesImprimantes.Any(p => p.NomAffiche == favoriActuel);
                    if (!favoriExisteEncore)
                    {
                        Debug.WriteLine($"[Cleanup] Le favori '{favoriActuel}' n'existe plus pour ce lieu. Suppression silencieuse.");
                        _prefService?.RemovePreferredPrinter(codeLieu);
                        favoriActuel = null;
                    }
                }

                var installedMap = GetInstalledPrintersMap();

                foreach (var imp in toutesLesImprimantes)
                {
                    var item = new ImprimanteUiItem(imp, this, OnFavoriChanged, OnInstallStatusChanged);
                    string nomApp = imp.NomAffiche;

                    if (IsPrinterInstalled(nomApp, installedMap)) item.IsInstalled = true;
                    if (favoriActuel != null && imp.NomAffiche == favoriActuel) item.IsFavorite = true;

                    Imprimantes.Add(item);
                }
            }
            finally
            {
                _isUpdatingFavorites = false;
            }
        }

        private string CleanServerName(string? serverInput)
        {
            if (string.IsNullOrWhiteSpace(serverInput)) return "";
            return serverInput.Replace("https://", "", StringComparison.OrdinalIgnoreCase)
                              .Replace("http://", "", StringComparison.OrdinalIgnoreCase)
                              .Trim('/');
        }

        private async Task CheckSmbAccessAsync(string serverName)
        {
            try
            {
                await Task.Run(() =>
                {
                    string cleanServer = CleanServerName(serverName);
                    string testPath = $@"\\{cleanServer}\print$";
                    bool accessOk = Directory.Exists(testPath);

                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        IsSmbAvailable = accessOk;
                        CommandManager.InvalidateRequerySuggested();
                    });
                });
            }
            catch
            {
                Application.Current.Dispatcher.Invoke(() => IsSmbAvailable = false);
            }
        }

        public void RafraichirEtatInstallation()
        {
            var installedMap = GetInstalledPrintersMap();
            foreach (var item in Imprimantes)
            {
                string nomApp = item.Data.NomAffiche;
                bool estInstallee = IsPrinterInstalled(nomApp, installedMap);
                if (item.IsInstalled != estInstallee) item.IsInstalled = estInstallee;
            }
        }

        private Dictionary<string, List<string>> GetInstalledPrintersMap()
        {
            var map = new Dictionary<string, List<string>>();
            try
            {
                using (var printServer = new LocalPrintServer())
                {
                    var queues = printServer.GetPrintQueues(new[] { EnumeratedPrintQueueTypes.Local, EnumeratedPrintQueueTypes.Connections });

                    foreach (var q in queues)
                    {
                        string fullPath = q.FullName.ToLower().Trim();
                        string rawName = q.Name.ToLower().Trim();
                        string shortName = rawName;

                        if (rawName.Contains("\\"))
                        {
                            shortName = Path.GetFileName(rawName);
                        }

                        if (!string.IsNullOrEmpty(shortName))
                        {
                            if (!map.ContainsKey(shortName)) map.Add(shortName, new List<string>());
                            map[shortName].Add(fullPath);
                        }
                    }
                }
            }
            catch { }
            return map;
        }

        private bool IsPrinterInstalled(string nomImprimante, Dictionary<string, List<string>> map)
        {
            if (string.IsNullOrEmpty(nomImprimante)) return false;
            string nomCherche = nomImprimante.ToLower().Trim();
            return map.ContainsKey(nomCherche);
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

                    if (_prefService.Current.AutoSwitchDefaultPrinter)
                    {
                        if (Application.Current is App myApp)
                        {
                            myApp.SetDefaultPrinterSafe(itemClicked.Data.NomAffiche);
                        }
                    }
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
            if (item.IsInstalling) return;

            if (install && !CanInstall)
            {
                MessageBox.Show("Le serveur d'impression est inaccessible (SMB).\nVérifiez votre connexion.", "Erreur Réseau", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (_ipcService == null || _configService == null) return;

            string printerName = item.Data.NomAffiche;
            string driverName = item.Data.Modele?.Pilote?.Nom ?? "INCONNU";
            string? rawServer = _configService.PrintServerName;

            if (string.IsNullOrWhiteSpace(rawServer))
            {
                MessageBox.Show("Erreur critique : Le nom du serveur d'impression est vide...", "Erreur", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            if (install)
            {
                try
                {
                    item.IsInstalling = true;

                    await Task.Delay(50);

                    bool success = await InstallerImprimanteDirectementAsync(printerName, driverName, rawServer);

                    if (success)
                    {
                        item.IsInstalled = true;
                    }
                }
                finally
                {
                    item.IsInstalling = false;
                }
            }
            else
            {
                string serverName = CleanServerName(rawServer);
                string uncPath = $@"\\{serverName}\{printerName}";

                if (await Task.Run(() => RunUserCommand($"printui.dll,PrintUIEntry /dn /q /n \"{uncPath}\"")))
                {
                    item.IsInstalled = false;

                    if (_prefService != null && !string.IsNullOrEmpty(_codeLieuActuel))
                    {
                        if (_prefService.Current.PreferredPrinters.TryGetValue(_codeLieuActuel, out string? favImprimante))
                        {
                            if (favImprimante == item.Data.NomAffiche)
                            {
                                _prefService.RemovePreferredPrinter(_codeLieuActuel);
                            }
                        }
                    }
                    item.IsFavorite = false;
                    Application.Current.Dispatcher.Invoke(RafraichirEtatInstallation);
                }
            }
        }

        public async Task<bool> InstallerImprimanteDirectementAsync(string printerName, string driverModel, string serverUrl)
        {
            if (_ipcService == null) return false;

            string cleanServer = CleanServerName(serverUrl);
            if (string.IsNullOrWhiteSpace(cleanServer)) return false;

            string uncPath = $@"\\{cleanServer}\{printerName}";

            var request = new IpcRequest
            {
                Action = "INSTALL_DRIVER",
                PrinterName = printerName,
                DriverModelName = driverModel,
                UncPath = uncPath
            };

            try { await _ipcService.SendRequestAsync(request); } catch { }

            bool mapSuccess = await Task.Run(() => RunUserCommand($"printui.dll,PrintUIEntry /in /n \"{uncPath}\""));

            if (!mapSuccess)
            {
                MessageBox.Show($"Echec connexion Windows à :\n{uncPath}", "Erreur", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            else
            {
                Application.Current.Dispatcher.Invoke(RafraichirEtatInstallation);
            }

            return mapSuccess;
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

        private bool _isInstalling;
        public bool IsInstalling
        {
            get => _isInstalling;
            set
            {
                if (_isInstalling != value)
                {
                    _isInstalling = value;
                    OnPropertyChanged();
                    Application.Current.Dispatcher.Invoke(CommandManager.InvalidateRequerySuggested);
                }
            }
        }

        public ICommand InstallCommand { get; }
        public ICommand UninstallCommand { get; }

        public ImprimanteUiItem(Imprimante data, MainWindowViewModel parent, Action<ImprimanteUiItem>? onFav, Action<ImprimanteUiItem, bool>? onInst)
        {
            Data = data;
            _parent = parent;
            _onFavoriChanged = onFav;
            _onInstallStatusChanged = onInst;

            InstallCommand = new RelayCommand(
                            _ => _onInstallStatusChanged?.Invoke(this, true),
                            _ => _parent.CanInstall
                        );

            UninstallCommand = new RelayCommand(_ => _onInstallStatusChanged?.Invoke(this, false));
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}