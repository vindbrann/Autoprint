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

        private void Header_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                this.DragMove();
            }
        }

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
            if (Application.Current is App myApp)
            {
                Mouse.OverrideCursor = Cursors.Wait;

                await myApp.RafraichirDonneesAsync();

                Mouse.OverrideCursor = null; 
            }
        }
    }
}