namespace Autoprint.Shared
{
    public class PrinterScanResult
    {
        public string IpAddress { get; set; } = string.Empty;
        public string Hostname { get; set; } = string.Empty;
        public string SnmpModel { get; set; } = string.Empty;
        public bool IsRegistered { get; set; } = false;
    }
}