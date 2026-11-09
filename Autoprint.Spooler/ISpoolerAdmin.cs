namespace Autoprint.Spooler
{
    /// <summary>
    /// Les seules opérations du serveur qui exigent des droits d'administrateur Windows (audit #8).
    /// Exécutées soit directement (mode « InProcess », développement / anciennes installations),
    /// soit par le service Windows AutoprintSpooler (mode « Service », le site web n'a alors aucun privilège).
    /// </summary>
    public interface ISpoolerAdmin
    {
        Task CreatePortAsync(string ipAddress);
        Task CreatePrinterAsync(string name, string driverName, string ipAddress, bool enableDirectMode);
        Task UpdatePrinterAsync(string currentName, string? comment, string? location, bool enableDirectMode, string? shareName, bool isShared, string? forcePortIp);
        Task RenamePrinterAsync(string oldName, string newName);
        Task DeletePrinterAsync(string name);
    }
}
