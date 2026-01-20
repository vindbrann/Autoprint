using Autoprint.Client.Models;
using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.IO;
using System.Text.Json;

namespace Autoprint.Client.Services
{
    public class UserPreferencesService
    {
        private readonly string _configPath;
        private UserPreferences _currentPreferences;
        private const string REGISTRY_RUN_KEY = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
        private const string APP_NAME = "AutoprintClient";

        public UserPreferences Current => _currentPreferences;

        public UserPreferencesService()
        {
            string documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            string appFolder = Path.Combine(documentsPath, "Autoprint");

            if (!Directory.Exists(appFolder)) Directory.CreateDirectory(appFolder);

            _configPath = Path.Combine(appFolder, "user-settings.json");
            _currentPreferences = LoadPreferences();

            if (!_currentPreferences.HasInitializedStartup)
            {
                SetWindowsStartup(true);
                _currentPreferences.HasInitializedStartup = true;
                Save();
            }
        }

        private UserPreferences LoadPreferences()
        {
            if (File.Exists(_configPath))
            {
                try
                {
                    string json = File.ReadAllText(_configPath);
                    var prefs = JsonSerializer.Deserialize<UserPreferences>(json);
                    return prefs ?? new UserPreferences();
                }
                catch { return new UserPreferences(); }
            }

            return new UserPreferences();
        }

        public void Save()
        {
            try
            {
                var options = new JsonSerializerOptions { WriteIndented = true };
                string json = JsonSerializer.Serialize(_currentPreferences, options);
                File.WriteAllText(_configPath, json);
            }
            catch { }
        }

        public bool IsWindowsStartupEnabled()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(REGISTRY_RUN_KEY, false);
                return key?.GetValue(APP_NAME) != null;
            }
            catch { return false; }
        }

        public void SetWindowsStartup(bool enable)
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(REGISTRY_RUN_KEY, true);
                if (key == null) return;

                if (enable)
                {
                    string? exePath = Process.GetCurrentProcess().MainModule?.FileName;
                    if (exePath != null) key.SetValue(APP_NAME, $"\"{exePath}\"");
                }
                else
                {
                    if (key.GetValue(APP_NAME) != null) key.DeleteValue(APP_NAME);
                }
            }
            catch { }
        }

        public void SetPreferredPrinter(string locationCode, string printerName)
        {
            if (_currentPreferences.PreferredPrinters.ContainsKey(locationCode))
                _currentPreferences.PreferredPrinters[locationCode] = printerName;
            else
                _currentPreferences.PreferredPrinters.Add(locationCode, printerName);
            Save();
        }

        public void RemovePreferredPrinter(string locationCode)
        {
            if (_currentPreferences.PreferredPrinters.ContainsKey(locationCode))
            {
                _currentPreferences.PreferredPrinters.Remove(locationCode);
                Save();
            }
        }
    }
}