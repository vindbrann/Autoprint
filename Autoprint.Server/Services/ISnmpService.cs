using System.Collections.Generic;
using System.Threading.Tasks;
using Autoprint.Shared.DTOs;
using Autoprint.Shared;

namespace Autoprint.Server.Services
{
    public interface ISnmpService
    {
        Task<PrinterDiagnosticResult> GetPrinterDiagnosticAsync(string ipAddress, int port, string community, int version, SnmpProfile? profile = null);
        Task<List<DiscoveredOidDto>> ScanPrinterOidsAsync(SnmpScanRequestDto request);
        Task<List<SnmpTestProfileResultDto>> TestProfileItemsAsync(SnmpTestProfileRequestDto request);
    }
}
