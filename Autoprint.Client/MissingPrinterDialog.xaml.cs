using System.Windows;

namespace Autoprint.Client
{
    public enum MissingPrinterAction { None, Reinstall, Forget }

    public partial class MissingPrinterDialog : Window
    {
        public MissingPrinterAction UserChoice { get; private set; } = MissingPrinterAction.None;

        public MissingPrinterDialog(string printerName)
        {
            InitializeComponent();
            TxtMessage.Text = $"L'imprimante favorite '{printerName}' n'est pas installée sur ce poste.\n\nVoulez-vous la réinstaller maintenant ou l'oublier ?";
        }

        private void BtnOublier_Click(object sender, RoutedEventArgs e)
        {
            UserChoice = MissingPrinterAction.Forget;
            this.Close();
        }

        private void BtnReinstaller_Click(object sender, RoutedEventArgs e)
        {
            UserChoice = MissingPrinterAction.Reinstall;
            this.Close();
        }
        private void Border_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (e.ButtonState == System.Windows.Input.MouseButtonState.Pressed)
                this.DragMove();
        }

    }
}