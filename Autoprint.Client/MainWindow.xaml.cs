using System.Reflection;
using System.Windows;
using System.Windows.Input;

namespace Autoprint.Client
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        // 1. GESTION DU DÉPLACEMENT (Drag & Drop de la fenêtre)
        private void Header_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                this.DragMove();
            }
        }

        // Gestion de la fermeture (Cacher au lieu de tuer)
        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            e.Cancel = true;
            this.Hide();
            base.OnClosing(e);
        }

        private void BtnFermer_Click(object sender, RoutedEventArgs e)
        {
            this.Hide();
        }
        private async void BtnActualiser_Click(object sender, RoutedEventArgs e)
        {
            // On récupère l'instance courante de l'application
            if (Application.Current is App myApp)
            {
                // On change le curseur pour montrer que ça charge
                Mouse.OverrideCursor = Cursors.Wait;

                await myApp.RafraichirDonneesAsync();

                Mouse.OverrideCursor = null; // Retour à la normale
            }
        }
    }
}