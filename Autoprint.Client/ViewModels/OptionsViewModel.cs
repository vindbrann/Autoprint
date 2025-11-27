using Autoprint.Client.Services;
using Microsoft.Win32;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Autoprint.Client.ViewModels
{
    public class OptionsViewModel : INotifyPropertyChanged
    {
        private readonly UserPreferencesService _prefService;
        private const string REGISTRY_KEY_PATH = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
        private const string APP_NAME = "AutoprintClient";

        public OptionsViewModel(UserPreferencesService prefService)
        {
            _prefService = prefService;

            // Si c'est la première fois qu'on lance l'app (pas de clé registre), 
            // on l'active par défaut comme tu l'as demandé.
            if (!IsStartupKeyPresent())
            {
                SetStartup(true);
            }
        }

        // --- Switch 1 : Notifications ---
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

        // --- Switch 2 : Auto-Switch Imprimante ---
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

        // --- Switch 3 : Démarrer avec Windows (Nouveau) ---
        // Cette propriété ne tape pas dans le JSON, mais directement dans le Registre Windows
        public bool StartWithWindows
        {
            get => IsStartupKeyPresent();
            set
            {
                if (IsStartupKeyPresent() != value)
                {
                    SetStartup(value);
                    OnPropertyChanged();
                }
            }
        }

        // --- Helpers Registre ---
        private bool IsStartupKeyPresent()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(REGISTRY_KEY_PATH, false);
                return key?.GetValue(APP_NAME) != null;
            }
            catch { return false; }
        }

        private void SetStartup(bool enable)
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(REGISTRY_KEY_PATH, true);
                if (key == null) return;

                if (enable)
                {
                    // On récupère le chemin de l'exécutable actuel
                    string? exePath = Process.GetCurrentProcess().MainModule?.FileName;
                    if (exePath != null)
                    {
                        // On ajoute "/minimized" en argument pour ton besoin futur si nécessaire, 
                        // ou juste le path pour lancer normalement.
                        key.SetValue(APP_NAME, $"\"{exePath}\"");
                    }
                }
                else
                {
                    key.DeleteValue(APP_NAME, false);
                }
            }
            catch (Exception)
            {
                // Gestion erreur droits accès (rare en HKCU)
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}