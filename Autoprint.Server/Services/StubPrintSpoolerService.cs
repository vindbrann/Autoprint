using Autoprint.Shared.DTOs;

namespace Autoprint.Server.Services
{
    public class StubPrintSpoolerService : IPrintSpoolerService
    {
        // Gestion (Ne fait rien)
        public Task CreerPortTcp(string ipAddress) => Task.CompletedTask;
        public Task CreerImprimante(string nom, string driverName, string ipAddress) => Task.CompletedTask;
        public Task SupprimerImprimante(string nom) => Task.CompletedTask;

        public Task<string?> RecupererNomImprimanteParIp(string ipAddress) => Task.FromResult<string?>(null);
        public Task RenommerImprimante(string ancienNom, string nouveauNom) => Task.CompletedTask;

        public Task ModifierImprimante(string nomActuel, string? nouveauCommentaire, string? nouveauLieu)
        {
            return Task.CompletedTask;
        }
        // Scan (Renvoie vide)
        public Task<List<DiscoveredPrinterDto>> ScanPrintersAsync(string targetHost, string? username, string? password)
        {
            return Task.FromResult(new List<DiscoveredPrinterDto>());
        }

        public Task<List<DiscoveredDriverDto>> ScanLocalDriversAsync()
        {
            return Task.FromResult(new List<DiscoveredDriverDto>());
        }
    }
}