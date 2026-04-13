using Autoprint.Shared;
using Autoprint.Shared.DTOs;

namespace Autoprint.Server.Services
{
    public interface IPrintSpoolerService
    {
        Task CreerPortTcp(string ipAddress);
        Task CreerImprimante(string nom, string driverName, string ipAddress, bool enableDirectMode);
        Task SupprimerImprimante(string nom);
        Task ModifierImprimante(string nomActuel, string? comment, string? location, bool enableDirectMode, string? shareName, bool isShared, string? forcePortIp = null);
        Task<string?> RecupererNomImprimanteParIp(string ipAddress);
        Task RenommerImprimante(string ancienNom, string nouveauNom);
        Task<List<DiscoveredPrinterDto>> ScanPrintersAsync(string targetHost, string? username, string? password);
        Task<List<Pilote>> GetInstalledDriversAsync();
        Task<bool> VerifierModeDirect(string nomImprimante, bool modeAttendu);
        Task<ServerAuditSnapshot> GetServerSnapshotAsync();
    }

    public class ServerAuditSnapshot
    {
        public Dictionary<string, string> PortsByIp { get; set; } = new();
        public Dictionary<string, WinPrinterInfo> PrintersByPort { get; set; } = new();
    }

    public class WinPrinterInfo
    {
        public string Name { get; set; } = "";
        public bool IsDirect { get; set; }
        public string PortName { get; set; } = "";
    }
}