using System.Collections.Generic;
using System.Threading.Tasks;
using Autoprint.Shared.DTOs;
using Autoprint.Shared;

namespace Autoprint.Server.Services
{
    public interface ISnmpService
    {
        Task<PrinterDiagnosticResult> GetPrinterDiagnosticAsync(string ipAddress, int port, SnmpCredentials credentials, SnmpProfile? profile = null, int lowThresholdPercent = MonitoringSettings.DefaultLowThresholdPercent);
        Task<PrinterDiagnosticResult> DetectPrinterAsync(string ipAddress, int port, SnmpCredentials credentials, int lowThresholdPercent = MonitoringSettings.DefaultLowThresholdPercent);
        Task<List<DiscoveredOidDto>> ScanPrinterOidsAsync(SnmpScanRequestDto request, SnmpCredentials credentials);
        Task<List<SnmpTestProfileResultDto>> TestProfileItemsAsync(SnmpTestProfileRequestDto request, SnmpCredentials credentials);
    }
}
