using Autoprint.Shared;
using Autoprint.Shared.DTOs;

namespace Autoprint.Server.Services
{
    public interface IPrintSpoolerService
    {
        Task CreerPortTcp(string ipAddress);
        Task CreerImprimante(string nom, string driverName, string ipAddress);
        Task SupprimerImprimante(string nom);
        Task ModifierImprimante(string nomActuel, string? nouveauCommentaire, string? nouveauLieu);
        Task<string?> RecupererNomImprimanteParIp(string ipAddress);
        Task RenommerImprimante(string ancienNom, string nouveauNom);
        Task<List<DiscoveredPrinterDto>> ScanPrintersAsync(string targetHost, string? username, string? password);
        Task<List<Pilote>> GetInstalledDriversAsync();
    }
}