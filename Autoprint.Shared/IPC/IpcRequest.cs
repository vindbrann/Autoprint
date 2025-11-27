namespace Autoprint.Shared.IPC
{
    // C'est l'enveloppe du message que le Client enverra au Service
    public class IpcRequest
    {
        public string Action { get; set; } = string.Empty; // "INSTALL", "UNINSTALL"
        public string PrinterName { get; set; } = string.Empty; // Nom affiché (ex: "Canon Accueil")
        public string UncPath { get; set; } = string.Empty;     // Chemin réseau (ex: "\\SRV-PRINT\Canon_01")
    }

    // C'est la réponse que le Service renverra
    public class IpcResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
    }
}