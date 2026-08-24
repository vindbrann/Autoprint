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
        public List<PaperTrayResult> Trays { get; set; } = new List<PaperTrayResult>();
        public string? SerialNumber { get; set; }
        public bool HasSnmpProfile { get; set; } = true;
    }

    public class TonerLevelResult
    {
        public string Color { get; set; } = "Noir";
        public int CurrentLevel { get; set; }
        public int MaxCapacity { get; set; }
        public int? EstimatedDaysRemaining { get; set; }
        public string ColorHex { get; set; } = "#212529";
    }

    public class PaperTrayResult
    {
        public string Name { get; set; } = "Bac 1";
        public int CurrentLevel { get; set; }
        public int MaxCapacity { get; set; }
    }

    public class SnmpScanRequestDto
    {
        public string IpAddress { get; set; } = string.Empty;
        public int Port { get; set; } = 161;
        public string Community { get; set; } = "public";
        public int Version { get; set; } = 2; // 1 = v1, 2 = v2c
        public List<string> CustomOidBranches { get; set; } = new List<string>();
        public bool ScanEnterpriseMib { get; set; } = false;
        public List<SnmpProfileItem>? ItemsToTest { get; set; }
    }

    public class DiscoveredOidDto
    {
        public string Oid { get; set; } = string.Empty;
        public string? OidMaxCapacity { get; set; }
        public string Value { get; set; } = string.Empty;
        public string ValueType { get; set; } = string.Empty;
        public string SuggestedName { get; set; } = string.Empty;
        public SnmpItemCategory SuggestedCategory { get; set; } = SnmpItemCategory.Status;
        public bool IsRelevant { get; set; } = true;
    }

    public class SnmpTestProfileRequestDto
    {
        public string IpAddress { get; set; } = string.Empty;
        public int Port { get; set; } = 161;
        public string Community { get; set; } = "public";
        public int Version { get; set; } = 2;
        public List<SnmpProfileItem> Items { get; set; } = new();
    }

    public class SnmpTestProfileResultDto
    {
        public string Oid { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
        public bool Success { get; set; }
    }
}

