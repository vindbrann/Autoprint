using System.Windows;
using System.Windows.Input;

namespace Autoprint.Client
{
    public partial class OptionsWindow : Window
    {
        public OptionsWindow()
        {
            InitializeComponent();
        }

        private void Header_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left) this.DragMove();
        }

        private void BtnFermer_Click(object sender, RoutedEventArgs e)
        {
            this.Hide(); // On cache seulement, on ne détruit pas la fenêtre
        }
    }
}