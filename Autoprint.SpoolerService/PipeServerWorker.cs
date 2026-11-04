using System.IO.Pipes;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using System.Text.Json;
using Autoprint.Spooler;

namespace Autoprint.SpoolerService
{
    /// <summary>
    /// Écoute le tube nommé local « AutoprintSpooler ».
    /// Sécurité : ACL limitée à l'identité du pool IIS (et SYSTEM), accès réseau refusé, taille des messages bornée,
    /// commandes exécutées une par une.
    /// </summary>
    public class PipeServerWorker : BackgroundService
    {
        private const int MaxInstances = 4;

        private readonly ISpoolerAdmin _admin;
        private readonly ILogger<PipeServerWorker> _logger;
        private readonly string _allowedClient;
        private readonly SemaphoreSlim _serialize = new(1, 1);
        private bool _warnedMissingClient;

        public PipeServerWorker(ISpoolerAdmin admin, ILogger<PipeServerWorker> logger, IConfiguration configuration)
        {
            _admin = admin;
            _logger = logger;
            // Identité du pool IIS du site Autoprint (compte automatique « ApplicationPoolIdentity »)
            _allowedClient = configuration["Spooler:AllowedClient"] ?? @"IIS AppPool\Autoprint";
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Service AutoprintSpooler démarré, client autorisé : {Client}", _allowedClient);

            while (!stoppingToken.IsCancellationRequested)
            {
                NamedPipeServerStream? server = null;
                try
                {
                    var security = BuildPipeSecurity(out bool clientResolved);
                    server = NamedPipeServerStreamAcl.Create(
                        SpoolerProtocol.PipeName,
                        PipeDirection.InOut,
                        MaxInstances,
                        PipeTransmissionMode.Byte,
                        PipeOptions.Asynchronous,
                        inBufferSize: 0,
                        outBufferSize: 0,
                        pipeSecurity: security);

                    if (clientResolved)
                    {
                        await server.WaitForConnectionAsync(stoppingToken);
                    }
                    else
                    {
                        // Pool IIS pas encore créé (installation en cours) : on recrée le tube toutes les 30 s pour intégrer son identité dès qu'elle existe
                        using var wait = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
                        wait.CancelAfter(TimeSpan.FromSeconds(30));
                        try { await server.WaitForConnectionAsync(wait.Token); }
                        catch (OperationCanceledException) when (!stoppingToken.IsCancellationRequested) { continue; }
                    }

                    var connection = server;
                    server = null;
                    _ = Task.Run(() => HandleConnectionAsync(connection, stoppingToken), stoppingToken);
                }
                catch (OperationCanceledException) { break; }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Erreur du tube AutoprintSpooler");
                    await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
                }
                finally
                {
                    server?.Dispose();
                }
            }
        }

        private PipeSecurity BuildPipeSecurity(out bool clientResolved)
        {
            clientResolved = false;
            var security = new PipeSecurity();

            // Aucun accès depuis le réseau (\\serveur\pipe\AutoprintSpooler)
            security.AddAccessRule(new PipeAccessRule(new SecurityIdentifier(WellKnownSidType.NetworkSid, null), PipeAccessRights.FullControl, AccessControlType.Deny));

            security.AddAccessRule(new PipeAccessRule(new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null), PipeAccessRights.FullControl, AccessControlType.Allow));

            try
            {
                var clientSid = (SecurityIdentifier)new NTAccount(_allowedClient).Translate(typeof(SecurityIdentifier));
                security.AddAccessRule(new PipeAccessRule(clientSid, PipeAccessRights.ReadWrite, AccessControlType.Allow));
                clientResolved = true;
            }
            catch (IdentityNotMappedException)
            {
                // Pool IIS pas encore créé : seul SYSTEM peut se connecter en attendant
                if (!_warnedMissingClient)
                {
                    _logger.LogWarning("Identité {Client} introuvable : seules les connexions SYSTEM sont acceptées pour l'instant.", _allowedClient);
                    _warnedMissingClient = true;
                }
            }

            return security;
        }

        private async Task HandleConnectionAsync(NamedPipeServerStream pipe, CancellationToken stoppingToken)
        {
            await using (pipe)
            {
                try
                {
                    using var cts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
                    cts.CancelAfter(TimeSpan.FromSeconds(30));

                    var line = await ReadLimitedLineAsync(pipe, SpoolerProtocol.MaxMessageLength, cts.Token);
                    if (line == null)
                    {
                        // Message trop long ou incomplet : connexion fermée sans réponse (aucune attente sur un client qui ne lit pas)
                        _logger.LogWarning("Message invalide ou trop long reçu sur le tube AutoprintSpooler : connexion fermée.");
                        return;
                    }

                    SpoolerRequest? request = null;
                    try { request = JsonSerializer.Deserialize<SpoolerRequest>(line); }
                    catch (JsonException) { }

                    // Les opérations du spouleur sont exécutées une par une
                    SpoolerResponse response;
                    await _serialize.WaitAsync(stoppingToken);
                    try { response = await SpoolerProtocol.HandleAsync(_admin, request); }
                    finally { _serialize.Release(); }

                    // Délai de réponse borné, indépendamment de la durée de l'opération
                    using var replyCts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
                    replyCts.CancelAfter(TimeSpan.FromSeconds(10));

                    // Commande et nom journalisés pour l'audit (pas de donnée sensible dans ces champs)
                    _logger.LogInformation("Commande {Command} « {Name} » : {Result}",
                        request?.Command ?? "?", request?.Name ?? request?.Address ?? "", response.Success ? "OK" : response.Error);

                    var bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(response) + "\n");
                    await pipe.WriteAsync(bytes, replyCts.Token);
                    await pipe.FlushAsync(replyCts.Token);
                }
                catch (Exception ex) when (ex is IOException or OperationCanceledException)
                {
                    _logger.LogWarning("Connexion AutoprintSpooler interrompue : {Message}", ex.Message);
                }
            }
        }

        // Lecture d'une ligne avec taille maximale : un client ne peut pas saturer la mémoire du service
        private static async Task<string?> ReadLimitedLineAsync(Stream stream, int maxBytes, CancellationToken token)
        {
            var buffer = new MemoryStream();
            var one = new byte[1];
            while (buffer.Length <= maxBytes)
            {
                int read = await stream.ReadAsync(one, token);
                if (read == 0) return null;
                if (one[0] == (byte)'\n') return Encoding.UTF8.GetString(buffer.ToArray()).TrimEnd('\r');
                buffer.WriteByte(one[0]);
            }
            return null;
        }
    }
}
