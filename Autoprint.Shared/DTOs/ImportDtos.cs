using System.ComponentModel.DataAnnotations;

namespace Autoprint.Shared.DTOs
{
    public class RemoteScanRequestDto
    {
        [Required]
        public string TargetHost { get; set; } = "localhost";
        public string? Username { get; set; }
        public string? Password { get; set; }
    }

    public class DiscoveredPrinterDto
    {
        public string Name { get; set; } = "";
        public string ShareName { get; set; } = "";
        public string DriverName { get; set; } = "";
        public string PortName { get; set; } = "";
        public bool IsShared { get; set; }
        public bool ExistsInDb { get; set; }
        public int? ExistingId { get; set; }
    }

    public class DiscoveredDriverDto
    {
        public string Name { get; set; } = "";
        public string DriverVersion { get; set; } = "";
        public string SyncStatus { get; set; } = "New";
    }
}