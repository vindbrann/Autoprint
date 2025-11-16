namespace Autoprint.Server.Services
{
    public interface IDriverService
    {
        Task<bool> InstallerPiloteAsync(string cheminInf);
        Task<bool> DesinstallerPiloteAsync(string nomInfOem);
    }
}