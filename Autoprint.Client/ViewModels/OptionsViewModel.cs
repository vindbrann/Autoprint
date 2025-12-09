using Autoprint.Client.Services;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Autoprint.Client.ViewModels
{
    public class OptionsViewModel : INotifyPropertyChanged
    {
        private readonly UserPreferencesService _prefService;

        public OptionsViewModel(UserPreferencesService prefService)
        {
            _prefService = prefService;
        }

        public bool EnableNotifications
        {
            get => _prefService.Current.EnableNotifications;
            set
            {
                if (_prefService.Current.EnableNotifications != value)
                {
                    _prefService.Current.EnableNotifications = value;
                    OnPropertyChanged();
                    _prefService.Save();
                }
            }
        }

        public bool AutoSwitchDefaultPrinter
        {
            get => _prefService.Current.AutoSwitchDefaultPrinter;
            set
            {
                if (_prefService.Current.AutoSwitchDefaultPrinter != value)
                {
                    _prefService.Current.AutoSwitchDefaultPrinter = value;
                    OnPropertyChanged();
                    _prefService.Save();
                }
            }
        }

        public bool StartWithWindows
        {
            get => _prefService.IsWindowsStartupEnabled();
            set
            {
                if (_prefService.IsWindowsStartupEnabled() != value)
                {
                    _prefService.SetWindowsStartup(value);
                    OnPropertyChanged();
                }
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}