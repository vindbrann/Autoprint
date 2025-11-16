using System.Diagnostics;

namespace Autoprint.Server.Services
{
    public class DriverService : IDriverService
    {
        public async Task<bool> InstallerPiloteAsync(string cheminInf)
        {
            // Simule l'installation via PnPUtil (commande Windows)
            // On utilise "pnputil /add-driver file.inf /install"
            return await RunPnpUtil($"/add-driver \"{cheminInf}\" /install");
        }

        public async Task<bool> DesinstallerPiloteAsync(string nomInfOem)
        {
            // Pour l'instant on simule juste le succès pour ne pas bloquer le dev
            return true;
        }

        private async Task<bool> RunPnpUtil(string arguments)
        {
            try
            {
                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "pnputil.exe",
                        Arguments = arguments,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                };

                process.Start();
                await process.WaitForExitAsync();
                return process.ExitCode == 0;
            }
            catch
            {
                return false;
            }
        }
    }
}