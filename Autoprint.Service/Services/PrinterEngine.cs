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

            // /ga = Global Add (Installation pour la machine, accessible par tous les utilisateurs)
            // /n = Nom réseau (UNC)
            return RunPrintUiCommand($"/ga /n \"{uncPath}\"");
        }

        public bool UninstallNetworkPrinter(string uncPath)
        {
            _logger.LogInformation("🗑️ Moteur : Tentative de suppression globale de {Path}", uncPath);

            // /gd = Global Delete (Suppression globale)
            // /n = Nom réseau (UNC)
            return RunPrintUiCommand($"/gd /n \"{uncPath}\"");
        }

        private bool RunPrintUiCommand(string arguments)
        {
            try
            {
                // On prépare la commande système
                // rundll32 printui.dll,PrintUIEntry /ga /n "\\Serveur\Imprimante" /q
                var psi = new ProcessStartInfo
                {
                    FileName = "rundll32.exe",
                    Arguments = $"printui.dll,PrintUIEntry {arguments} /q", // /q = Quiet (Silencieux, pas de popup d'erreur UI)
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true // Important pour capturer l'erreur Windows si échec
                };

                using var process = Process.Start(psi);
                if (process == null) return false;

                // On laisse 30 secondes à Windows pour installer le pilote et monter l'imprimante
                bool exited = process.WaitForExit(30000);

                if (!exited)
                {
                    _logger.LogWarning("⚠️ Timeout : La commande printui a mis trop de temps.");
                    try { process.Kill(); } catch { }
                    return false;
                }

                // Code 0 = Succès absolu
                if (process.ExitCode == 0)
                {
                    _logger.LogInformation("✅ Commande Windows exécutée avec succès.");
                    return true;
                }
                else
                {
                    // Si échec, on lit pourquoi (ex: "Accès refusé", "Imprimante introuvable")
                    string error = process.StandardError.ReadToEnd();
                    _logger.LogError("❌ Échec commande Windows (Code {Code}): {Error}", process.ExitCode, error);
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Exception critique dans le moteur d'impression.");
                return false;
            }
        }
    }
}