using Autoprint.Client.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Printing;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace Autoprint.Client.ViewModels
{
    public class ManagePrintersViewModel : INotifyPropertyChanged
    {
        [DllImport("winspool.drv", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern bool OpenPrinter(string pPrinterName, out IntPtr phPrinter, ref PRINTER_DEFAULTS pDefault);

        [DllImport("winspool.drv", SetLastError = true)]
        private static extern bool ClosePrinter(IntPtr hPrinter);

        [StructLayout(LayoutKind.Sequential)]
        private struct PRINTER_DEFAULTS
        {
            public IntPtr pDatatype;
            public IntPtr pDevMode;
            public int DesiredAccess;
        }

        private const int PRINTER_ACCESS_ADMINISTER = 0x00000004;

        private readonly UserPreferencesService _prefService;
        private bool _isBusy;
        private string _statusMessage = "";
        private bool _hasNoPrinters;
        private bool _showLocalPrinters;

        private static readonly HashSet<string> SystemPrintersBlacklist = new(StringComparer.OrdinalIgnoreCase)
        {
            "microsoft print to pdf",
            "microsoft xps document writer",
            "onenote",
            "onenote for windows 10",
            "fax",
            "root print queue",
            "send to onenote"
        };

        public event Action? OnPrinterDeleted;

        public ObservableCollection<InstalledPrinterUiItem> InstalledPrinters { get; set; } = new ObservableCollection<InstalledPrinterUiItem>();

        public bool IsBusy
        {
            get => _isBusy;
            set { _isBusy = value; OnPropertyChanged(); CommandManager.InvalidateRequerySuggested(); }
        }

        public string StatusMessage
        {
            get => _statusMessage;
            set { _statusMessage = value; OnPropertyChanged(); }
        }

        public bool HasNoPrinters
        {
            get => _hasNoPrinters;
            set { _hasNoPrinters = value; OnPropertyChanged(); }
        }

        public bool ShowLocalPrinters
        {
            get => _showLocalPrinters;
            set
            {
                if (_showLocalPrinters != value)
                {
                    _showLocalPrinters = value;
                    OnPropertyChanged();
                    RefreshPrinters();
                }
            }
        }

        public bool CanClean => !IsBusy && InstalledPrinters.Any(p => !p.IsFavorite && p.IsNetwork);

        public int NetworkPrintersCount => InstalledPrinters.Count(p => p.IsNetwork);
        public int LocalPrintersCount => InstalledPrinters.Count(p => p.IsLocal);

        public ICommand CleanCommand { get; }

        public ManagePrintersViewModel(UserPreferencesService prefService)
        {
            _prefService = prefService;
            CleanCommand = new RelayCommand(async _ => await CleanNonFavoritesAsync(), _ => CanClean);
            RefreshPrinters();
        }

        public void RefreshPrinters()
        {
            InstalledPrinters.Clear();

            var favoritePrinters = _prefService.Current.PreferredPrinters.Values
                .Select(n => n.ToLower().Trim())
                .ToHashSet();

            try
            {
                using (var printServer = new LocalPrintServer())
                {
                    var queues = printServer.GetPrintQueues(new[] {
                        EnumeratedPrintQueueTypes.Local,
                        EnumeratedPrintQueueTypes.Connections
                    });

                    foreach (var queue in queues)
                    {
                        string name = queue.Name.Trim();
                        string nameLower = name.ToLower();
                        string port = queue.QueuePort?.Name.ToLower() ?? "";

                        // Filtrage strict des imprimantes système ou virtuelles
                        if (SystemPrintersBlacklist.Contains(nameLower) ||
                            nameLower.Contains("pdf") || nameLower.Contains("xps") ||
                            nameLower.Contains("onenote") || nameLower.Contains("fax") ||
                            nameLower.Contains("root") ||
                            port.StartsWith("lpt") || port.StartsWith("com") ||
                            port.Contains("file:") || port.Contains("prompt:"))
                        {
                            continue;
                        }

                        bool isNetwork = queue.FullName.StartsWith(@"\\") || port.StartsWith(@"\\");

                        if (isNetwork)
                        {
                            bool isFav = favoritePrinters.Contains(nameLower);
                            InstalledPrinters.Add(new InstalledPrinterUiItem(
                                name: name,
                                fullName: queue.FullName,
                                isFavorite: isFav,
                                isNetwork: true,
                                requiresElevation: false,
                                deleteCallback: OnDeleteRequested,
                                toggleCallback: OnToggleFavoriteRequested
                            ));
                        }
                        else if (ShowLocalPrinters)
                        {
                            bool requiresElevation = !CheckIfUserCanAdministerPrinter(name);
                            bool isFav = favoritePrinters.Contains(nameLower);

                            InstalledPrinters.Add(new InstalledPrinterUiItem(
                                name: name,
                                fullName: queue.FullName,
                                isFavorite: isFav,
                                isNetwork: false,
                                requiresElevation: requiresElevation,
                                deleteCallback: OnDeleteRequested,
                                toggleCallback: OnToggleFavoriteRequested
                            ));
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                StatusMessage = "Erreur lecture spouleur.";
                Debug.WriteLine($"Erreur scan spouleur : {ex.Message}");
            }

            UpdateState();
        }

        private static bool CheckIfUserCanAdministerPrinter(string printerName)
        {
            IntPtr hPrinter = IntPtr.Zero;
            try
            {
                var defaults = new PRINTER_DEFAULTS
                {
                    pDatatype = IntPtr.Zero,
                    pDevMode = IntPtr.Zero,
                    DesiredAccess = PRINTER_ACCESS_ADMINISTER
                };

                if (OpenPrinter(printerName, out hPrinter, ref defaults))
                {
                    return true;
                }
                return false;
            }
            catch
            {
                return false;
            }
            finally
            {
                if (hPrinter != IntPtr.Zero)
                {
                    ClosePrinter(hPrinter);
                }
            }
        }

        private async void OnDeleteRequested(InstalledPrinterUiItem item)
        {
            if (IsBusy) return;

            string confirmMessage = $"Voulez-vous supprimer l'imprimante '{item.Name}' ?";
            if (item.RequiresElevation)
            {
                confirmMessage = $"Cette imprimante locale a été installée pour le système.\n\nUne autorisation administrateur (UAC) va vous être demandée.\n\nContinuer ?";
            }

            if (MessageBox.Show(confirmMessage, "Confirmation de suppression", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            {
                return;
            }

            IsBusy = true;
            StatusMessage = $"Suppression de {item.Name}...";

            bool success = await DeletePrinterInternalAsync(item.Name, item.FullName, item.IsNetwork, item.RequiresElevation);

            if (success)
            {
                RemoveFromPreferences(item.Name);
                InstalledPrinters.Remove(item);
                StatusMessage = $"{item.Name} supprimée avec succès.";
                OnPrinterDeleted?.Invoke();
            }
            else
            {
                if (StatusMessage != "Suppression annulée.")
                {
                    MessageBox.Show($"Windows n'a pas pu supprimer l'imprimante '{item.Name}'.", "Erreur", MessageBoxButton.OK, MessageBoxImage.Error);
                    StatusMessage = "Échec de la suppression.";
                }
            }

            IsBusy = false;
            UpdateState();
        }

        private void OnToggleFavoriteRequested(InstalledPrinterUiItem item)
        {
            if (item.IsFavorite)
            {
                RemoveFromPreferences(item.Name);
            }
            else
            {
                if (Application.Current is App myApp)
                {
                    bool success = myApp.SetDefaultPrinterSafe(item.Name);

                    if (success)
                    {
                        _prefService.SetPreferredPrinter("Manual", item.Name);
                    }
                    else
                    {
                        MessageBox.Show("Impossible de définir cette imprimante par défaut.", "Erreur", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }
                }
            }

            RefreshPrinters();
        }

        private void RemoveFromPreferences(string printerName)
        {
            try
            {
                var entry = _prefService.Current.PreferredPrinters
                    .FirstOrDefault(x => x.Value.Equals(printerName, StringComparison.OrdinalIgnoreCase));

                if (!string.IsNullOrEmpty(entry.Key))
                {
                    _prefService.RemovePreferredPrinter(entry.Key);
                    Debug.WriteLine($"[CLEAN] Favori supprimé pour le lieu : {entry.Key}");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[CLEAN] Erreur nettoyage favori : {ex.Message}");
            }
        }

        private async Task CleanNonFavoritesAsync()
        {
            // Règle d'or : Seules les imprimantes réseau non-favorites sont ciblées
            var toRemove = InstalledPrinters
                .Where(p => !p.IsFavorite && p.IsNetwork)
                .ToList();

            if (toRemove.Count == 0) return;

            if (MessageBox.Show($"Voulez-vous supprimer {toRemove.Count} imprimantes non-favorites ?\n\n(Les imprimantes locales et les favoris sont protégés)", "Nettoyage", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
                return;

            IsBusy = true;
            int count = 0;
            int errors = 0;

            foreach (var printer in toRemove)
            {
                count++;
                StatusMessage = $"Nettoyage ({count}/{toRemove.Count}) : {printer.Name}...";

                if (await DeletePrinterInternalAsync(printer.Name, printer.FullName, printer.IsNetwork, printer.RequiresElevation))
                {
                    InstalledPrinters.Remove(printer);
                    OnPrinterDeleted?.Invoke();
                }
                else
                {
                    errors++;
                }

                await Task.Delay(200);
            }

            IsBusy = false;
            StatusMessage = errors > 0 ? $"Terminé avec {errors} erreur(s)." : "Nettoyage terminé.";
            UpdateState();

            await Task.Delay(3000);
            if (!IsBusy) StatusMessage = "";
        }

        private void UpdateState()
        {
            HasNoPrinters = InstalledPrinters.Count == 0;
            OnPropertyChanged(nameof(CanClean));
            OnPropertyChanged(nameof(NetworkPrintersCount));
            OnPropertyChanged(nameof(LocalPrintersCount));
            CommandManager.InvalidateRequerySuggested();
        }

        private async Task<bool> DeletePrinterInternalAsync(string printerName, string printerFullName, bool isNetwork, bool requiresElevation)
        {
            try
            {
                // Validation de sécurité anti-injection : aucun métacaractère shell
                if (printerName.IndexOfAny(new[] { '&', '|', ';', '\r', '\n', '`', '$', '"' }) >= 0)
                {
                    Debug.WriteLine($"[SECURITY] Nom d'imprimante suspect rejeté : {printerName}");
                    return false;
                }

                string rundllPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "rundll32.exe");
                if (!File.Exists(rundllPath))
                {
                    rundllPath = "rundll32.exe";
                }

                string actionFlag = isNetwork ? "/dn" : "/dl";
                string targetParam = isNetwork ? printerFullName : printerName;

                var psi = new ProcessStartInfo
                {
                    FileName = rundllPath,
                    UseShellExecute = requiresElevation,
                    CreateNoWindow = !requiresElevation
                };

                if (requiresElevation)
                {
                    psi.Verb = "runas";
                    psi.Arguments = $"printui.dll,PrintUIEntry {actionFlag} /q /n \"{targetParam}\"";
                }
                else
                {
                    psi.ArgumentList.Add("printui.dll,PrintUIEntry");
                    psi.ArgumentList.Add(actionFlag);
                    psi.ArgumentList.Add("/q");
                    psi.ArgumentList.Add("/n");
                    psi.ArgumentList.Add(targetParam);
                }

                var tcs = new TaskCompletionSource<bool>();
                Process? process = null;

                try
                {
                    process = Process.Start(psi);
                }
                catch (Win32Exception ex) when (ex.NativeErrorCode == 1223)
                {
                    // 1223 = ERROR_CANCELLED (l'utilisateur a refusé ou annulé le prompt UAC)
                    Debug.WriteLine("[UAC] Élévation annulée par l'utilisateur.");
                    StatusMessage = "Suppression annulée.";
                    return false;
                }

                if (process == null) return false;

                process.EnableRaisingEvents = true;
                process.Exited += (sender, args) =>
                {
                    tcs.TrySetResult(process.ExitCode == 0);
                    process.Dispose();
                };

                var completedTask = await Task.WhenAny(tcs.Task, Task.Delay(15000));
                if (completedTask == tcs.Task)
                {
                    return await tcs.Task;
                }
                else
                {
                    try { process.Kill(); } catch { }
                    return false;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[DELETE] Erreur : {ex.Message}");
                return false;
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public class InstalledPrinterUiItem
    {
        public string Name { get; }
        public string FullName { get; }
        public bool IsFavorite { get; }
        public bool IsNetwork { get; }
        public bool IsLocal => !IsNetwork;
        public bool RequiresElevation { get; }

        public string IconKind => IsFavorite ? "Star" : "Printer";
        public string IconColor => IsFavorite ? "#FFC107" : "{DynamicResource MaterialDesignBody}";
        public double IconOpacity => IsFavorite ? 1.0 : 0.6;

        public string TypeBadgeText => IsNetwork ? "RÉSEAU" : (RequiresElevation ? "LOCALE (IT)" : "LOCALE");
        public string SubtitleText => IsNetwork ? FullName : (RequiresElevation ? "Installée pour la machine (Droits admin requis)" : "Installée pour l'utilisateur");

        public string DeleteIconKind => RequiresElevation ? "ShieldLock" : "DeleteOutline";
        public string DeleteTooltip => RequiresElevation
            ? "Supprimer cette imprimante locale (Nécessite des droits Administrateur)"
            : (IsNetwork ? "Désinstaller cette imprimante réseau" : "Supprimer cette imprimante locale");

        public ICommand DeleteCommand { get; }
        public ICommand ToggleFavoriteCommand { get; }

        public InstalledPrinterUiItem(
            string name,
            string fullName,
            bool isFavorite,
            bool isNetwork,
            bool requiresElevation,
            Action<InstalledPrinterUiItem> deleteCallback,
            Action<InstalledPrinterUiItem> toggleCallback)
        {
            Name = name;
            FullName = fullName;
            IsFavorite = isFavorite;
            IsNetwork = isNetwork;
            RequiresElevation = requiresElevation;
            DeleteCommand = new RelayCommand(_ => deleteCallback(this));
            ToggleFavoriteCommand = new RelayCommand(_ => toggleCallback(this));
        }
    }
}