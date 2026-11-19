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
        /// <summary>Files locales : nom → port (lecture seule, sans privilège).</summary>
        Task<List<KeyValuePair<string, string>>> ListerFilesLocales();
        Task RenommerImprimante(string ancienNom, string nouveauNom);
        Task<List<DiscoveredPrinterDto>> ScanLocalPrintersAsync();
        Task<List<Pilote>> GetInstalledDriversAsync();
        Task<bool> VerifierModeDirect(string nomImprimante, bool modeAttendu);
        /// <summary>Files locales pour l'audit : nom, port(s), mode direct. Lève une exception si la lecture échoue (jamais de liste vide par défaut).</summary>
        Task<List<WinPrinterInfo>> ListerFilesPourAudit();
    }

    public class WinPrinterInfo
    {
        public string Name { get; set; } = "";
        public bool IsDirect { get; set; }
        public string PortName { get; set; } = "";
    }
}