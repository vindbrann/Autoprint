using Autoprint.Shared;
using Autoprint.Shared.DTOs;

namespace Autoprint.Server.Services
{
    public interface IPrintSpoolerService
    {
        Task CreerPortTcp(string ipAddress);
        // Ajout du paramètre enableBODP
        Task CreerImprimante(string nom, string driverName, string ipAddress, bool enableBODP);
        Task SupprimerImprimante(string nom);
        // Ajout du paramètre enableBODP
        Task ModifierImprimante(string nomActuel, string? comment, string? location, bool enableBODP);
        Task<string?> RecupererNomImprimanteParIp(string ipAddress);
        Task RenommerImprimante(string ancienNom, string nouveauNom);
        Task<List<DiscoveredPrinterDto>> ScanPrintersAsync(string targetHost, string? username, string? password);
        Task<List<Pilote>> GetInstalledDriversAsync();
        Task<bool> VerifierModeFiliale(string nomImprimante, bool modeAttendu);
    }
}