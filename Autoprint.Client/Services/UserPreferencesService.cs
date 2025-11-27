using Autoprint.Client.Models;
using System;
using System.IO;
using System.Text.Json;

namespace Autoprint.Client.Services
{
    public class UserPreferencesService
    {
        private readonly string _configPath;
        private UserPreferences _currentPreferences;

        // C'est cette propriété "Current" qui manquait et causait l'erreur CS1061
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
        }

        private UserPreferences LoadPreferences()
        {
            if (!File.Exists(_configPath))
            {
                return new UserPreferences();
            }

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