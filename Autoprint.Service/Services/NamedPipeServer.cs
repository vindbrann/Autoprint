using Autoprint.Shared.IPC;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using System.Security.AccessControl;
using System.Security.Principal;

namespace Autoprint.Service.Services
{
    public class NamedPipeServer
    {
        private readonly ILogger<NamedPipeServer> _logger;
        private readonly PrinterEngine _engine;
        private const string PIPE_NAME = "AutoprintPipe"; // Le nom du tuyau

        public NamedPipeServer(ILogger<NamedPipeServer> logger, PrinterEngine engine)
        {
            _logger = logger;
            _engine = engine;
        }

        public async Task StartListeningAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("🎧 Serveur IPC : En attente de connexions sur {PipeName}...", PIPE_NAME);

            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    // 1. Création du Tuyau Sécurisé
                    // On autorise tout le monde (Authenticated Users) à écrire dedans
                    // Sinon le Client (User) ne pourrait pas parler au Service (System)
                    var pipeSecurity = new PipeSecurity();
                    pipeSecurity.AddAccessRule(new PipeAccessRule(new SecurityIdentifier(WellKnownSidType.AuthenticatedUserSid, null), PipeAccessRights.ReadWrite, AccessControlType.Allow));

                    await using var serverStream = NamedPipeServerStreamAcl.Create(
                        PIPE_NAME,
                        PipeDirection.InOut,
                        maxNumberOfServerInstances: NamedPipeServerStream.MaxAllowedServerInstances,
                        transmissionMode: PipeTransmissionMode.Byte,
                        options: PipeOptions.Asynchronous,
                        inBufferSize: 0,
                        outBufferSize: 0,
                        pipeSecurity: pipeSecurity);

                    // 2. Attente d'une connexion (Bloquant jusqu'à ce qu'un client arrive)
                    await serverStream.WaitForConnectionAsync(cancellationToken);

                    _logger.LogInformation("⚡ Connexion entrante reçue !");

                    // 3. Lecture du message
                    using (var reader = new StreamReader(serverStream, leaveOpen: true))
                    using (var writer = new StreamWriter(serverStream, leaveOpen: true) { AutoFlush = true })
                    {
                        string? jsonLine = await reader.ReadLineAsync(cancellationToken);

                        if (!string.IsNullOrEmpty(jsonLine))
                        {
                            var request = JsonSerializer.Deserialize<IpcRequest>(jsonLine);

                            if (request != null)
                            {
                                _logger.LogInformation("📨 Ordre reçu : {Action} -> {Imprimante}", request.Action, request.UncPath);

                                bool result = false;
                                string msg = "";

                                // ROUTAGE DES COMMANDES
                                switch (request.Action)
                                {
                                    case "INSTALL":
                                        result = _engine.InstallNetworkPrinter(request.UncPath);
                                        msg = result ? "Imprimante installée globalement." : "Échec de l'installation.";
                                        break;

                                    case "UNINSTALL":
                                        result = _engine.UninstallNetworkPrinter(request.UncPath);
                                        msg = result ? "Imprimante supprimée globalement." : "Échec de la suppression.";
                                        break;

                                    default:
                                        msg = "Action inconnue.";
                                        break;
                                }

                                var response = new IpcResponse { Success = result, Message = msg };
                                await writer.WriteLineAsync(JsonSerializer.Serialize(response));
                            }
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    break; // Arrêt normal
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "❌ Erreur critique IPC");
                }
            }
        }
    }
}