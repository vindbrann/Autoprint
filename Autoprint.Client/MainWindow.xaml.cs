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
            this.PreviewKeyDown += MainWindow_PreviewKeyDown;
        }

        private void MainWindow_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            // Si l'utilisateur fait Alt+F4, on masque la fenêtre dans la barre des tâches
            if (e.Key == Key.System && e.SystemKey == Key.F4)
            {
                e.Handled = true;
                (DataContext as ViewModels.MainWindowViewModel)?.OptionsViewModel?.ResetAdminPanel();
                this.Hide();
            }
        }

        private void Header_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                this.DragMove();
            }
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            // Autorise la fermeture propre lors d'une demande système ou installeur (WM_CLOSE)
            base.OnClosing(e);
            Application.Current.Shutdown();
        }

        private void BtnFermer_Click(object sender, RoutedEventArgs e)
        {
            (DataContext as ViewModels.MainWindowViewModel)?.OptionsViewModel?.ResetAdminPanel();
            this.Hide();
        }
        private async void BtnActualiser_Click(object sender, RoutedEventArgs e)
        {
            if (Application.Current is App myApp)
            {
                Mouse.OverrideCursor = Cursors.Wait;

                await myApp.RafraichirDonneesAsync();

                Mouse.OverrideCursor = null; 
            }
        }
    }
}