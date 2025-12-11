using System.Windows;
using System.Windows.Input;
using Autoprint.Client.Services;
using Autoprint.Client.ViewModels;
namespace Autoprint.Client
{
    public partial class OptionsWindow : Window
    {
        public OptionsWindow(UserPreferencesService prefService)
        {
            InitializeComponent();

            this.DataContext = new OptionsViewModel(prefService);
        }

        private void Header_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left) this.DragMove();
        }

        private void BtnFermer_Click(object sender, RoutedEventArgs e)
        {
            this.Hide(); 
        }
    }
}