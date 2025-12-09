using Autoprint.Client.Models;
using Microsoft.Win32;
using System;
using System.IO;
using System.Text.Json;

namespace Autoprint.Client.Services
{
    public class UserPreferencesService
    {
        private readonly string _configPath;
        private UserPreferences _currentPreferences;

        public UserPreferences Current => _currentPreferences;

        public UserPreferencesService()
        {
            string documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            string appFolder = Path.Combine(documentsPath, "Autoprint");

            if (!Directory.Exists(appFolder))
            {
                Directory.CreateDirectory(appFolder);
            }

            _configPath = Path.Combine(appFolder, "user-settings.json");

            _currentPreferences = LoadPreferences();

            if (!File.Exists(_configPath) &&
               (!string.IsNullOrEmpty(_currentPreferences.PrintServerName) || !string.IsNullOrEmpty(_currentPreferences.AgentApiKey)))
            {
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
                catch
                {
                    return new UserPreferences();
                }
            }

            var registryPrefs = new UserPreferences();
            try
            {
                using (RegistryKey? key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Autoprint"))
                {
                    if (key != null)
                    {
                        object? server = key.GetValue("PRINTSERVER");
                        object? apiKey = key.GetValue("APIKEY");

                        if (server != null) registryPrefs.PrintServerName = server.ToString();
                        if (apiKey != null) registryPrefs.AgentApiKey = apiKey.ToString();
                    }
                }
            }
            catch (Exception)
            {
            }

            return registryPrefs;
        }

        public void Save()
        {
            try
            {
                var options = new JsonSerializerOptions { WriteIndented = true };
                string json = JsonSerializer.Serialize(_currentPreferences, options);
                File.WriteAllText(_configPath, json);
            }
            catch { /* Ignorer pour l'instant */ }
        }

        public void SetPreferredPrinter(string locationCode, string printerName)
        {
            if (_currentPreferences.PreferredPrinters.ContainsKey(locationCode))
            {
                _currentPreferences.PreferredPrinters[locationCode] = printerName;
            }
            else
            {
                _currentPreferences.PreferredPrinters.Add(locationCode, printerName);
            }
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