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
        private const string PIPE_NAME = "AutoprintPipe";

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
                    var pipeSecurity = new PipeSecurity();
                    pipeSecurity.AddAccessRule(new PipeAccessRule(
                        new SecurityIdentifier(WellKnownSidType.AuthenticatedUserSid, null),
                        PipeAccessRights.ReadWrite,
                        AccessControlType.Allow));

                    await using var serverStream = NamedPipeServerStreamAcl.Create(
                        PIPE_NAME,
                        PipeDirection.InOut,
                        maxNumberOfServerInstances: NamedPipeServerStream.MaxAllowedServerInstances,
                        transmissionMode: PipeTransmissionMode.Byte,
                        options: PipeOptions.Asynchronous,
                        inBufferSize: 0,
                        outBufferSize: 0,
                        pipeSecurity: pipeSecurity);

                    await serverStream.WaitForConnectionAsync(cancellationToken);
                    _logger.LogInformation("⚡ Connexion IPC établie.");

                    using (var reader = new StreamReader(serverStream, leaveOpen: true))
                    using (var writer = new StreamWriter(serverStream, leaveOpen: true) { AutoFlush = true })
                    {
                        string? jsonLine = await reader.ReadLineAsync(cancellationToken);

                        if (!string.IsNullOrEmpty(jsonLine))
                        {
                            var request = JsonSerializer.Deserialize<IpcRequest>(jsonLine);

                            if (request != null)
                            {
                                _logger.LogInformation("📨 Reçu : Action={Action}, Driver={Driver}", request.Action, request.DriverModelName);

                                bool result = false;
                                string msg = "";

                                switch (request.Action)
                                {
                                    case "INSTALL_DRIVER":
                                        if (string.IsNullOrEmpty(request.DriverModelName))
                                        {
                                            msg = "Erreur : Nom du pilote manquant.";
                                            _logger.LogWarning("⚠️ Reçu INSTALL_DRIVER sans nom de modèle !");
                                        }
                                        else
                                        {
                                            result = _engine.InstallDriverOnly(request.DriverModelName, request.UncPath);
                                            msg = result ? "Pilote installé (ou déjà présent)." : "Échec installation pilote (Check SMB/Intune).";
                                        }
                                        break;

                                    default:
                                        msg = $"Action '{request.Action}' non supportée par le Service.";
                                        _logger.LogWarning("⚠️ Action inconnue ou obsolète reçue.");
                                        break;
                                }

                                var response = new IpcResponse { Success = result, Message = msg };
                                await writer.WriteLineAsync(JsonSerializer.Serialize(response));
                            }
                        }
                    }
                }
                catch (OperationCanceledException) { break; }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "❌ Erreur critique IPC");
                }
            }
        }
    }
}