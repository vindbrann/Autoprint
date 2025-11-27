using Autoprint.Client.ViewModels;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Printing;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using System.Windows;

namespace Autoprint.Client.ViewModels
{
    public class ManagePrintersViewModel : INotifyPropertyChanged
    {
        public ObservableCollection<InstalledPrinterUiItem> InstalledPrinters { get; set; } = new ObservableCollection<InstalledPrinterUiItem>();

        // NOUVEAU : Une simple case à cocher pour dire à la vue "La liste est vide"
        private bool _hasNoPrinters;
        public bool HasNoPrinters
        {
            get => _hasNoPrinters;
            set { _hasNoPrinters = value; OnPropertyChanged(); }
        }

        public ManagePrintersViewModel()
        {
            RefreshPrinters();
        }

        public void RefreshPrinters()
        {
            InstalledPrinters.Clear();

            try
            {
                using (var printServer = new LocalPrintServer())
                {
                    // 1. On récupère TOUT (pour ne pas rater les "Global Add" qui apparaissent parfois en Local)
                    var queues = printServer.GetPrintQueues(new[] {
                EnumeratedPrintQueueTypes.Local,
                EnumeratedPrintQueueTypes.Connections
            });

                    foreach (var queue in queues)
                    {
                        string name = queue.Name.ToLower();
                        string port = queue.QueuePort?.Name.ToLower() ?? "";

                        // 2. FILTRE D'EXCLUSION (Les "Déchets")
                        if (name.Contains("pdf") ||
                            name.Contains("xps") ||
                            name.Contains("onenote") ||
                            name.Contains("fax") ||
                            name.Contains("root")) // "Root Print Queue" apparait parfois
                        {
                            continue;
                        }

                        // 3. FILTRE DE PORT (La sécurité)
                        // On exclut tout ce qui est clairement local physique ou virtuel
                        if (port.StartsWith("usb") ||
                            port.StartsWith("lpt") ||
                            port.StartsWith("com") ||
                            port.Contains("file:") ||
                            port.Contains("prompt:"))
                        {
                            continue;
                        }

                        // 4. VALIDATION RÉSEAU
                        // On garde si c'est :
                        // - Une connexion UNC (commence par \\)
                        // - Un port IP (commence par IP_ ou contient une adresse IP)
                        // - Un port WSD (commence par WSD-)
                        // - Ou si le nom complet est un chemin UNC
                        bool isNetwork = port.StartsWith("\\") ||
                                         port.StartsWith("ip_") ||
                                         port.StartsWith("wsd") ||
                                         queue.FullName.StartsWith("\\\\");

                        if (isNetwork)
                        {
                            InstalledPrinters.Add(new InstalledPrinterUiItem(queue.Name, queue.FullName, OnDeleteRequested));
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Erreur scan : {ex.Message}");
            }

            UpdateEmptyState();
        }

        private void OnDeleteRequested(InstalledPrinterUiItem item)
        {
            if (MessageBox.Show($"Supprimer {item.Name} ?", "Confirmation", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
            {
                if (UnmapPrinter(item.FullName))
                {
                    InstalledPrinters.Remove(item);
                    UpdateEmptyState(); // On vérifie si la liste est devenue vide
                }
                else
                {
                    MessageBox.Show("Impossible de supprimer l'imprimante.", "Erreur", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        // Petite méthode utilitaire pour mettre à jour le booléen
        private void UpdateEmptyState()
        {
            HasNoPrinters = InstalledPrinters.Count == 0;
        }

        private bool UnmapPrinter(string printerName)
        {
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo
                {
                    FileName = "rundll32.exe",
                    Arguments = $"printui.dll,PrintUIEntry /dn /q /n \"{printerName}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                var process = Process.Start(psi);
                process?.WaitForExit();
                System.Threading.Thread.Sleep(500);
                return true;
            }
            catch { return false; }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public class InstalledPrinterUiItem
    {
        public string Name { get; }
        public string FullName { get; }
        public ICommand DeleteCommand { get; }

        public InstalledPrinterUiItem(string name, string fullName, Action<InstalledPrinterUiItem> deleteCallback)
        {
            Name = name;
            FullName = fullName;
            DeleteCommand = new RelayCommand(_ => deleteCallback(this));
        }
    }
}