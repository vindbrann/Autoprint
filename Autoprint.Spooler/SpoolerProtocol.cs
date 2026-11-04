using System.IO.Pipes;
using System.Text.Json;

namespace Autoprint.Spooler
{
    /// <summary>
    /// Échange entre le site web et le service AutoprintSpooler : une requête JSON par connexion, sur un tube nommé local.
    /// Seules les 5 commandes de <see cref="ISpoolerAdmin"/> existent : aucun texte libre n'est exécuté.
    /// </summary>
    public static class SpoolerProtocol
    {
        public const string PipeName = "AutoprintSpooler";
        public const int MaxMessageLength = 16 * 1024;

        public const string CreatePort = "CreatePort";
        public const string CreatePrinter = "CreatePrinter";
        public const string UpdatePrinter = "UpdatePrinter";
        public const string RenamePrinter = "RenamePrinter";
        public const string DeletePrinter = "DeletePrinter";

        /// <summary>Exécute une requête reçue par le service. Ne lève pas d'exception : l'erreur est renvoyée au site.</summary>
        public static async Task<SpoolerResponse> HandleAsync(ISpoolerAdmin admin, SpoolerRequest? request)
        {
            if (request == null) return SpoolerResponse.Fail("Requête vide.");
            try
            {
                switch (request.Command)
                {
                    case CreatePort:
                        await admin.CreatePortAsync(request.Address ?? "");
                        break;
                    case CreatePrinter:
                        await admin.CreatePrinterAsync(request.Name ?? "", request.Driver ?? "", request.Address ?? "", request.DirectMode);
                        break;
                    case UpdatePrinter:
                        await admin.UpdatePrinterAsync(request.Name ?? "", request.Comment, request.Location, request.DirectMode, request.ShareName, request.IsShared, request.ForcePortIp);
                        break;
                    case RenamePrinter:
                        await admin.RenamePrinterAsync(request.Name ?? "", request.NewName ?? "");
                        break;
                    case DeletePrinter:
                        await admin.DeletePrinterAsync(request.Name ?? "");
                        break;
                    default:
                        return SpoolerResponse.Fail("Commande inconnue.");
                }
                return new SpoolerResponse { Success = true };
            }
            catch (Exception ex)
            {
                return SpoolerResponse.Fail(ex.Message);
            }
        }
    }

    public class SpoolerRequest
    {
        public string Command { get; set; } = "";
        public string? Name { get; set; }
        public string? NewName { get; set; }
        public string? Driver { get; set; }
        public string? Address { get; set; }
        public string? Comment { get; set; }
        public string? Location { get; set; }
        public string? ShareName { get; set; }
        public bool IsShared { get; set; }
        public bool DirectMode { get; set; }
        public string? ForcePortIp { get; set; }
    }

    public class SpoolerResponse
    {
        public bool Success { get; set; }
        public string? Error { get; set; }

        public static SpoolerResponse Fail(string error) => new() { Success = false, Error = error };
    }

    /// <summary>Côté site web : transmet chaque opération privilégiée au service AutoprintSpooler.</summary>
    public class PipeSpoolerAdmin : ISpoolerAdmin
    {
        private static readonly TimeSpan ConnectTimeout = TimeSpan.FromSeconds(5);
        private static readonly TimeSpan ReplyTimeout = TimeSpan.FromSeconds(90);

        public Task CreatePortAsync(string ipAddress) =>
            SendAsync(new SpoolerRequest { Command = SpoolerProtocol.CreatePort, Address = ipAddress });

        public Task CreatePrinterAsync(string name, string driverName, string ipAddress, bool enableDirectMode) =>
            SendAsync(new SpoolerRequest { Command = SpoolerProtocol.CreatePrinter, Name = name, Driver = driverName, Address = ipAddress, DirectMode = enableDirectMode });

        public Task UpdatePrinterAsync(string currentName, string? comment, string? location, bool enableDirectMode, string? shareName, bool isShared, string? forcePortIp) =>
            SendAsync(new SpoolerRequest { Command = SpoolerProtocol.UpdatePrinter, Name = currentName, Comment = comment, Location = location, DirectMode = enableDirectMode, ShareName = shareName, IsShared = isShared, ForcePortIp = forcePortIp });

        public Task RenamePrinterAsync(string oldName, string newName) =>
            SendAsync(new SpoolerRequest { Command = SpoolerProtocol.RenamePrinter, Name = oldName, NewName = newName });

        public Task DeletePrinterAsync(string name) =>
            SendAsync(new SpoolerRequest { Command = SpoolerProtocol.DeletePrinter, Name = name });

        private static async Task SendAsync(SpoolerRequest request)
        {
            using var cts = new CancellationTokenSource(ReplyTimeout);
            await using var pipe = new NamedPipeClientStream(".", SpoolerProtocol.PipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
            try
            {
                await pipe.ConnectAsync(ConnectTimeout, cts.Token);
            }
            catch (UnauthorizedAccessException ex)
            {
                throw new InvalidOperationException(
                    "Accès refusé par le service « AutoprintSpooler » : le site doit tourner sous le compte « IIS AppPool\\Autoprint » " +
                    "(pool IIS « Autoprint » en identité ApplicationPoolIdentity).", ex);
            }
            catch (Exception ex) when (ex is TimeoutException or OperationCanceledException or IOException)
            {
                throw new InvalidOperationException(
                    "Service Windows « AutoprintSpooler » injoignable : vérifiez qu'il est démarré sur le serveur (services.msc). " +
                    "Les modifications des files d'impression ne peuvent pas être appliquées sans lui.", ex);
            }

            using var reader = new StreamReader(pipe, leaveOpen: true);
            await using var writer = new StreamWriter(pipe, leaveOpen: true) { AutoFlush = true };

            await writer.WriteLineAsync(JsonSerializer.Serialize(request).AsMemory(), cts.Token);
            var line = await reader.ReadLineAsync(cts.Token);
            var response = line == null ? null : JsonSerializer.Deserialize<SpoolerResponse>(line);

            if (response == null) throw new InvalidOperationException("Réponse vide du service AutoprintSpooler.");
            if (!response.Success) throw new InvalidOperationException(response.Error ?? "Erreur inconnue du service AutoprintSpooler.");
        }
    }
}
