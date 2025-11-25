using Microsoft.Win32; // Pour le Registre Windows
using System;

namespace Autoprint.Client.Services
{
    public class ConfigurationService
    {
        public string ApiKey { get; private set; } = string.Empty;

        public void Initialize(string[] args)
        {
            string? argKey = GetArgValue(args, "--api-key");
            if (!string.IsNullOrEmpty(argKey))
            {
                ApiKey = argKey;
                return;
            }

            string? regKey = GetRegistryValue();
            if (!string.IsNullOrEmpty(regKey))
            {
                ApiKey = regKey;
                return;
            }

            ApiKey = string.Empty;
        }

        private string? GetArgValue(string[] args, string name)
        {
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == name && i + 1 < args.Length) return args[i + 1];
            }
            return null;
        }

        private string? GetRegistryValue()
        {
            try
            {
                using (RegistryKey? key = Registry.CurrentUser.OpenSubKey(@"Software\Autoprint"))
                {
                    return key?.GetValue("AgentApiKey") as string;
                }
            }
            catch { return null; }
        }
    }
}