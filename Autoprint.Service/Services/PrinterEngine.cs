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

         public bool InstallDriverOnly(string driverModel, string serverShare)
        {
            _logger.LogInformation("🔧 Moteur : Installation du pilote '{Driver}' depuis '{Source}'", driverModel, serverShare);
            return RunPrintUiCommand($"/ia /m \"{driverModel}\" /n \"{serverShare}\"");
        }

        private bool RunPrintUiCommand(string arguments)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "rundll32.exe",
                    Arguments = $"printui.dll,PrintUIEntry {arguments} /q",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };

                using var process = Process.Start(psi);
                if (process == null) return false;
                bool exited = process.WaitForExit(60000);

                if (!exited)
                {
                    _logger.LogWarning("⚠️ Timeout Driver Install");
                    try { process.Kill(); } catch { }
                    return false;
                }

                if (process.ExitCode == 0) return true;

                string error = process.StandardError.ReadToEnd();
                _logger.LogError("❌ Erreur Driver (Code {C}): {E}", process.ExitCode, error);
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Crash Moteur");
                return false;
            }
        }
    }
}