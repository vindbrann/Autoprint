namespace Autoprint.Shared.IPC
{
    public class IpcRequest
    {
        public string Action { get; set; } = string.Empty;
        public string PrinterName { get; set; } = string.Empty;
        public string UncPath { get; set; } = string.Empty;
        public string DriverModelName { get; set; } = string.Empty;
    }

    public class IpcResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
    }
}