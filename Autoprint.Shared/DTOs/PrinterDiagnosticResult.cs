using System.Collections.Generic;

namespace Autoprint.Shared.DTOs
{
    public class PrinterDiagnosticResult
    {
        public bool PingSuccess { get; set; }
        public long PingRoundtripTimeMs { get; set; }
        public string Status { get; set; } = "Inconnu";
        public List<string> Alerts { get; set; } = new List<string>();
        public long UptimeSeconds { get; set; }
        public long PageCounter { get; set; }
        public List<TonerLevelResult> Toners { get; set; } = new List<TonerLevelResult>();
    }

    public class TonerLevelResult
    {
        public string Color { get; set; } = "Noir";
        public int CurrentLevel { get; set; }
        public int MaxCapacity { get; set; }
        public int? EstimatedDaysRemaining { get; set; }
    }
}
