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
        private readonly string _sentinelPath;
        private UserPreferences _currentPreferences;

        private const string REGISTRY_RUN_KEY = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
        private const string APP_NAME = "AutoprintClient";

        public UserPreferences Current => _currentPreferences;

        public UserPreferencesService()
        {
            string documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            string appRoamingFolder = Path.Combine(documentsPath, "Autoprint");

            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string appLocalFolder = Path.Combine(localAppData, "Autoprint");

            if (!Directory.Exists(appRoamingFolder)) Directory.CreateDirectory(appRoamingFolder);
            if (!Directory.Exists(appLocalFolder)) Directory.CreateDirectory(appLocalFolder);

            _configPath = Path.Combine(appRoamingFolder, "user-settings.json");
            _sentinelPath = Path.Combine(appLocalFolder, ".install-token");

            _currentPreferences = LoadPreferences();

            InitializeStartupLogic();
        }

        private void InitializeStartupLogic()
        {
            bool isMachineFirstRun = !File.Exists(_sentinelPath);

            if (isMachineFirstRun)
            {
                Debug.WriteLine("[Startup] Nouvelle machine détectée. Activation du démarrage auto.");

                _currentPreferences.RunAtStartup = true;

                SetWindowsStartup(true);

                try { File.Create(_sentinelPath).Close(); } catch { }

                Save();
            }
            else
            {

                Debug.WriteLine($"[Startup] Machine connue. Sync Registre selon JSON : {_currentPreferences.RunAtStartup}");
                SetWindowsStartup(_currentPreferences.RunAtStartup);
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
            catch (Exception ex)
            {
                Debug.WriteLine($"[Config] Erreur sauvegarde : {ex.Message}");
            }
        }

        public void ToggleStartup(bool enable)
        {
            _currentPreferences.RunAtStartup = enable;
            SetWindowsStartup(enable);
            Save();
        }

        private void SetWindowsStartup(bool enable)
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(REGISTRY_RUN_KEY, true);
                if (key == null) return;

                if (enable)
                {
                    string? exePath = Process.GetCurrentProcess().MainModule?.FileName;
                    if (exePath != null)
                    {
                        key.SetValue(APP_NAME, $"\"{exePath}\"");
                    }
                }
                else
                {
                    if (key.GetValue(APP_NAME) != null)
                        key.DeleteValue(APP_NAME);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Registry] Erreur accès HKCU : {ex.Message}");
            }
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