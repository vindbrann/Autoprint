using Autoprint.Shared.DTOs;

namespace Autoprint.Server.Services
{
    public interface IPrintSpoolerService
    {
        // --- METHODES DE GESTION (Actions) ---
        Task CreerPortTcp(string ipAddress);
        Task CreerImprimante(string nom, string driverName, string ipAddress);
        Task SupprimerImprimante(string nom);

        // --- METHODES DE SCAN (Inventaire) ---
        Task<List<DiscoveredPrinterDto>> ScanPrintersAsync(string targetHost, string? username, string? password);
        Task<List<DiscoveredDriverDto>> ScanLocalDriversAsync();
    }
}