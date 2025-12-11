using Autoprint.Client.Services;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Printing;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace Autoprint.Client.ViewModels
{
    public class ManagePrintersViewModel : INotifyPropertyChanged
    {
        private readonly UserPreferencesService _prefService;
        private bool _isBusy;
        private string _statusMessage = "";
        private bool _hasNoPrinters;

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

        public bool CanClean => !IsBusy && InstalledPrinters.Any(p => !p.IsFavorite);

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
                        string name = queue.Name.ToLower();
                        string port = queue.QueuePort?.Name.ToLower() ?? "";

                        if (name.Contains("pdf") || name.Contains("xps") || name.Contains("onenote") ||
                            name.Contains("fax") || name.Contains("root") ||
                            port.StartsWith("usb") || port.StartsWith("lpt") || port.StartsWith("com") ||
                            port.Contains("file:") || port.Contains("prompt:"))
                        {
                            continue;
                        }

                        bool isNetwork = port.StartsWith("\\") ||
                                         port.StartsWith("ip_") ||
                                         port.StartsWith("wsd") ||
                                         queue.FullName.StartsWith("\\\\");

                        if (isNetwork)
                        {
                            bool isFav = favoritePrinters.Contains(queue.Name.ToLower().Trim());
                            InstalledPrinters.Add(new InstalledPrinterUiItem(queue.Name, queue.FullName, isFav, OnDeleteRequested));
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                StatusMessage = "Erreur lecture spouleur.";
                Debug.WriteLine($"Erreur scan : {ex.Message}");
            }

            UpdateState();
        }

        private async void OnDeleteRequested(InstalledPrinterUiItem item)
        {
            if (IsBusy) return;

            if (MessageBox.Show($"Voulez-vous désinstaller {item.Name} ?", "Confirmation", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                IsBusy = true;
                StatusMessage = $"Suppression de {item.Name}...";

                bool success = await UnmapPrinterAsync(item.FullName);

                if (success)
                {
                    RemoveFromPreferences(item.Name);

                    InstalledPrinters.Remove(item);
                }
                else
                {
                    MessageBox.Show("Windows n'a pas pu supprimer la connexion.", "Erreur", MessageBoxButton.OK, MessageBoxImage.Error);
                }

                IsBusy = false;
                StatusMessage = "";
                UpdateState();
            }
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
            var toRemove = InstalledPrinters.Where(p => !p.IsFavorite).ToList();

            if (toRemove.Count == 0) return;

            if (MessageBox.Show($"Voulez-vous supprimer {toRemove.Count} imprimantes non-favorites ?", "Grand Nettoyage", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
                return;

            IsBusy = true;
            int count = 0;
            int errors = 0;

            foreach (var printer in toRemove)
            {
                count++;
                StatusMessage = $"Nettoyage ({count}/{toRemove.Count}) : {printer.Name}...";

                if (await UnmapPrinterAsync(printer.FullName))
                {
                    InstalledPrinters.Remove(printer);
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
            CommandManager.InvalidateRequerySuggested();
        }

        private async Task<bool> UnmapPrinterAsync(string printerName)
        {
            try
            {
                var tcs = new TaskCompletionSource<bool>();

                var psi = new ProcessStartInfo
                {
                    FileName = "rundll32.exe",
                    Arguments = $"printui.dll,PrintUIEntry /dn /q /n \"{printerName}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                var process = new Process { StartInfo = psi, EnableRaisingEvents = true };

                process.Exited += (sender, args) =>
                {
                    tcs.TrySetResult(process.ExitCode == 0);
                    process.Dispose();
                };

                if (!process.Start()) return false;

                var completedTask = await Task.WhenAny(tcs.Task, Task.Delay(10000));

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
            catch
            {
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

        public string IconKind => IsFavorite ? "Star" : "NetworkOutline";
        public string IconColor => IsFavorite ? "#FFC107" : "{DynamicResource MaterialDesignBody}";
        public double IconOpacity => IsFavorite ? 1.0 : 0.5;

        public ICommand DeleteCommand { get; }

        public InstalledPrinterUiItem(string name, string fullName, bool isFavorite, Action<InstalledPrinterUiItem> deleteCallback)
        {
            Name = name;
            FullName = fullName;
            IsFavorite = isFavorite;
            DeleteCommand = new RelayCommand(_ => deleteCallback(this));
        }
    }
}