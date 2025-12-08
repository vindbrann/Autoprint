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
        private const int TIMEOUT_MS = 3000;
        public async Task<bool> SendRequestAsync(IpcRequest request)
        {
            try
            {
                using var clientStream = new NamedPipeClientStream(".", PIPE_NAME, PipeDirection.InOut);
                await clientStream.ConnectAsync(TIMEOUT_MS);
                using (var writer = new StreamWriter(clientStream, leaveOpen: true) { AutoFlush = true })
                using (var reader = new StreamReader(clientStream, leaveOpen: true))
                {
                    string jsonRequest = JsonSerializer.Serialize(request);
                    await writer.WriteLineAsync(jsonRequest);

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
                return false;
            }
        }
    }
}