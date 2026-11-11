using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Autoprint.Client.ViewModels;

namespace Autoprint.Client
{
    /// <summary>Panneau près de l'horloge : s'ouvre au clic sur l'icône, se range dès qu'on clique ailleurs.</summary>
    public partial class MainWindow : Window
    {
        private const double Gap = 12;
        private DateTime _hiddenAt = DateTime.MinValue;

        public MainWindow()
        {
            InitializeComponent();
            PreviewKeyDown += OnPreviewKeyDown;
            Deactivated += (_, _) => HidePanel();
            SizeChanged += (_, _) => PlaceNearClock();
            DataContextChanged += (_, e) =>
            {
                if (e.OldValue is INotifyPropertyChanged o) o.PropertyChanged -= OnVmChanged;
                if (e.NewValue is INotifyPropertyChanged n) n.PropertyChanged += OnVmChanged;
            };
        }

        private MainWindowViewModel? Vm => DataContext as MainWindowViewModel;

        /// <summary>Clic sur l'icône : ouvre, ou referme si le panneau vient d'être rangé par ce même clic.</summary>
        public void TogglePanel()
        {
            if (IsVisible) { HidePanel(); return; }
            if ((DateTime.Now - _hiddenAt).TotalMilliseconds < 400) return;
            ShowPanel();
        }

        public void ShowPanel()
        {
            var area = SystemParameters.WorkArea;
            MaxHeight = Math.Min(640, area.Height - 2 * Gap);
            if (!IsVisible) Show();
            PlaceNearClock();
            Activate();
            Focus();
        }

        public void HidePanel()
        {
            if (!IsVisible) return;
            _hiddenAt = DateTime.Now;
            Hide();
            Vm?.OnPanelHidden();
        }

        private void PlaceNearClock()
        {
            var area = SystemParameters.WorkArea;
            Left = area.Right - ActualWidth - Gap;
            Top = area.Bottom - ActualHeight - Gap;
        }

        private void OnVmChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (Vm == null) return;
            switch (e.PropertyName)
            {
                // Un volet a besoin de place même si le panneau est court
                case nameof(MainWindowViewModel.IsSettingsOpen):
                case nameof(MainWindowViewModel.IsElsewhereOpen):
                    MinHeight = Vm.IsSettingsOpen || Vm.IsElsewhereOpen ? Math.Min(460, MaxHeight) : 120;
                    if (Vm.IsSiteStep) FocusLater(SiteSearch);
                    break;
                case nameof(MainWindowViewModel.IsAddOpen):
                    if (Vm.IsAddOpen) FocusLater(AddSearch);
                    break;
                case nameof(MainWindowViewModel.PickedSite):
                    if (Vm.IsPrinterStep && Vm.ShowPrinterFilter) FocusLater(PrinterSearch);
                    else if (Vm.IsSiteStep) FocusLater(SiteSearch);
                    break;
            }
        }

        private void FocusLater(Control target)
            => Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(() => { target.Focus(); Keyboard.Focus(target); }));

        private void OnPreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                e.Handled = true;
                if (Vm?.HandleEscape() != true) HidePanel();
            }
            else if (e.Key == Key.System && e.SystemKey == Key.F4)
            {
                e.Handled = true;
                HidePanel();
            }
        }

        // Croix des champs de recherche : vide le champ et y laisse le curseur
        private void ClearSearch_Click(object sender, RoutedEventArgs e)
        {
            DependencyObject? d = sender as DependencyObject;
            while (d != null && d is not TextBox) d = VisualTreeHelper.GetParent(d);
            if (d is TextBox box)
            {
                box.Clear();
                box.Focus();
            }
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            // Autorise la fermeture propre lors d'une demande système ou de l'installeur (WM_CLOSE)
            base.OnClosing(e);
            Application.Current.Shutdown();
        }

        private void BtnFermer_Click(object sender, RoutedEventArgs e) => HidePanel();
    }
}
