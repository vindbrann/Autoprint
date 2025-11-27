using System.Diagnostics;

namespace Autoprint.Service.Services
{
    public class PrinterEngine
    {
        private readonly ILogger<PrinterEngine> _logger;

        public PrinterEngine(ILogger<PrinterEngine> logger)
        {
            _logger = logger;
        }

        public bool InstallNetworkPrinter(string uncPath)
        {
            _logger.LogInformation("🔧 Moteur : Tentative d'installation globale de {Path}", uncPath);

            // /ga = Global Add (Pour la machine)
            // /n = Nom réseau
            return RunPrintUiCommand($"/ga /n \"{uncPath}\"");
        }

        public bool UninstallNetworkPrinter(string uncPath)
        {
            _logger.LogInformation("🗑️ Moteur : Tentative de suppression globale de {Path}", uncPath);

            // /gd = Global Delete
            // /n = Nom réseau
            return RunPrintUiCommand($"/gd /n \"{uncPath}\"");
        }

        private bool RunPrintUiCommand(string arguments)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "rundll32.exe",
                    Arguments = $"printui.dll,PrintUIEntry {arguments} /q", // /q = Quiet (pas de popup)
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };

                using var process = Process.Start(psi);
                if (process == null) return false;

                // On attend que Windows finisse le travail (timeout 30s)
                bool exited = process.WaitForExit(30000);

                if (!exited)
                {
                    _logger.LogWarning("⚠️ Timeout lors de l'exécution de la commande printui.");
                    process.Kill();
                    return false;
                }

                // Code 0 = Succès
                if (process.ExitCode == 0)
                {
                    _logger.LogInformation("✅ Commande exécutée avec succès.");
                    return true;
                }
                else
                {
                    string error = process.StandardError.ReadToEnd();
                    _logger.LogError("❌ Échec commande (Code {Code}): {Error}", process.ExitCode, error);
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Exception critique dans PrinterEngine");
                return false;
            }
        }
    }
}