using Autoprint.Shared.IPC;
using System;
using System.IO;
using System.IO.Pipes;
using System.Text.Json;
using System.Threading.Tasks;

namespace Autoprint.Client.Services
{
    public class IpcService
    {
        private const string PIPE_NAME = "AutoprintPipe";
        private const int TIMEOUT_MS = 3000; // 3 secondes max pour se connecter

        public async Task<bool> InstallPrinterAsync(string printerName, string uncPath)
        {
            var request = new IpcRequest
            {
                Action = "INSTALL",
                PrinterName = printerName,
                UncPath = uncPath
            };
            return await SendRequestAsync(request);
        }

        public async Task<bool> UninstallPrinterAsync(string printerName, string uncPath)
        {
            var request = new IpcRequest
            {
                Action = "UNINSTALL",
                PrinterName = printerName,
                UncPath = uncPath
            };
            return await SendRequestAsync(request);
        }

        private async Task<bool> SendRequestAsync(IpcRequest request)
        {
            try
            {
                // "." signifie machine locale
                using var clientStream = new NamedPipeClientStream(".", PIPE_NAME, PipeDirection.InOut);

                // On essaie de se connecter au Service Windows
                await clientStream.ConnectAsync(TIMEOUT_MS);

                // On envoie la demande
                using (var writer = new StreamWriter(clientStream, leaveOpen: true) { AutoFlush = true })
                using (var reader = new StreamReader(clientStream, leaveOpen: true))
                {
                    string jsonRequest = JsonSerializer.Serialize(request);
                    await writer.WriteLineAsync(jsonRequest);

                    // On attend la réponse
                    string? jsonResponse = await reader.ReadLineAsync();

                    if (!string.IsNullOrEmpty(jsonResponse))
                    {
                        var response = JsonSerializer.Deserialize<IpcResponse>(jsonResponse);
                        return response?.Success ?? false;
                    }
                }

                return false;
            }
            catch (Exception)
            {
                // Le service n'est probablement pas démarré ou pas installé
                return false;
            }
        }
    }
}