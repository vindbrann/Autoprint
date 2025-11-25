using Autoprint.Shared;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;

namespace Autoprint.Client.ViewModels
{
    public class MainWindowViewModel : INotifyPropertyChanged
    {
        // Données affichées
        private string _titreLieu = "Chargement...";
        public string TitreLieu
        {
            get => _titreLieu;
            set { _titreLieu = value; OnPropertyChanged(); }
        }

        private string _appVersion = "v1.0.0";
        public string AppVersion
        {
            get => _appVersion;
            set { _appVersion = value; OnPropertyChanged(); }
        }

        // La liste des imprimantes (ObservableCollection met à jour l'UI automatiquement)
        public ObservableCollection<Imprimante> Imprimantes { get; set; } = new ObservableCollection<Imprimante>();

        // Constructeur
        public MainWindowViewModel()
        {
            // Données de démo pour le designer (visibles dans Visual Studio quand tu design)
            if (DesignerProperties.GetIsInDesignMode(new System.Windows.DependencyObject()))
            {
                TitreLieu = " Démo - Siège Social";
                Imprimantes.Add(new Imprimante { NomAffiche = "RICOH Accueil", AdresseIp = "192.168.1.10" });
                Imprimantes.Add(new Imprimante { NomAffiche = "HP Compta", AdresseIp = "192.168.1.12" });
            }
        }

        // Méthode pour charger les vraies données
        public void ChargerDonnees(string nomLieu, System.Collections.Generic.List<Imprimante> toutesLesImprimantes, string ipLocale)
        {
            TitreLieu = $" Vous êtes ici : {nomLieu}";

            Imprimantes.Clear();

            // LOGIQUE MÉTIER : On filtre les imprimantes du lieu
            // Si le lieu est "Inconnu", on n'affiche rien ou tout (à décider)
            // Ici, on affiche celles qui ont le même nom de lieu, ou on pourrait filtrer par ID de lieu si on l'avait passé.
            // Pour l'instant, on affiche tout ce qu'on a reçu du DataService filtré.

            // Note : Dans App.xaml.cs, on fera le filtrage avant d'appeler cette méthode.
            foreach (var imp in toutesLesImprimantes)
            {
                Imprimantes.Add(imp);
            }
        }

        // Implémentation standard MVVM pour rafraîchir l'interface
        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}