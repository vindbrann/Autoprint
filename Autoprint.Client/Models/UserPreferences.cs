using System.Collections.Generic;

namespace Autoprint.Client.Models
{
    public class UserPreferences
    {
        public bool EnableNotifications { get; set; } = true;
        public bool AutoSwitchDefaultPrinter { get; set; } = true;
        public bool HasInitializedStartup { get; set; } = false;
        public string? LastDetectedLocationCode { get; set; }
        public bool RunAtStartup { get; set; } = true;
        public Dictionary<string, string> PreferredPrinters { get; set; } = new Dictionary<string, string>();
        public bool HasCreatedDesktopShortcut { get; set; } = false;
    }
}