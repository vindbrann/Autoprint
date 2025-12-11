using Autoprint.Client.Services;
using Autoprint.Client.ViewModels;
using System.Windows;
using System.Windows.Input;

namespace Autoprint.Client
{
    public partial class ManagePrintersWindow : Window
    {
        public ManagePrintersWindow(UserPreferencesService prefService)
        {
            InitializeComponent();
            DataContext = new ManagePrintersViewModel(prefService);
        }

        private void Header_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left) this.DragMove();
        }

        private void BtnFermer_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}