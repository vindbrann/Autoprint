using System;
using System.IO;

namespace Autoprint.Client.Services
{
    public class PathService
    {
        public string UserSettingsPath { get; private set; } = string.Empty;

        public string LocalCachePath { get; private set; } = string.Empty;

        public void Initialize(string[] commandLineArgs)
        {
            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            LocalCachePath = Path.Combine(localAppData, "Autoprint");

            if (!Directory.Exists(LocalCachePath))
                Directory.CreateDirectory(LocalCachePath);


            string documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            UserSettingsPath = Path.Combine(documents, "Autoprint");

            for (int i = 0; i < commandLineArgs.Length; i++)
            {
                if (commandLineArgs[i] == "--config-path" && i + 1 < commandLineArgs.Length)
                {
                    string customPath = commandLineArgs[i + 1];
                    if (!string.IsNullOrWhiteSpace(customPath))
                    {
                        UserSettingsPath = customPath;
                    }
                }
            }

            if (!Directory.Exists(UserSettingsPath))
                Directory.CreateDirectory(UserSettingsPath);
        }
    }
}