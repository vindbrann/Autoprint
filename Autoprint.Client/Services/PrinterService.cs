using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Printing;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace Autoprint.Client.Services
{
    /// <summary>Accès au spouleur de Windows : imprimantes réseau installées, ajout, retrait, imprimante par défaut.</summary>
    public static class PrinterService
    {
        [DllImport("winspool.drv", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern bool SetDefaultPrinter(string name);

        private static readonly char[] ForbiddenChars = { '&', '|', ';', '\r', '\n', '`', '$', '"' };

        /// <summary>Nom du serveur d'impression sans schéma ni barre finale (« impression.mondomaine.fr »).</summary>
        public static string CleanServerName(string? serverInput)
        {
            if (string.IsNullOrWhiteSpace(serverInput)) return "";
            return serverInput.Replace("https://", "", StringComparison.OrdinalIgnoreCase)
                              .Replace("http://", "", StringComparison.OrdinalIgnoreCase)
                              .Trim('/');
        }

        public static string UncPath(string server, string printerName) => $@"\\{CleanServerName(server)}\{printerName.Replace("\"", "")}";

        /// <summary>Imprimantes réseau installées pour l'utilisateur : nom court (minuscules) vers nom complet (\\serveur\imprimante).</summary>
        public static Dictionary<string, string> GetNetworkQueues()
        {
            var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                using var printServer = new LocalPrintServer();
                var queues = printServer.GetPrintQueues(new[] { EnumeratedPrintQueueTypes.Local, EnumeratedPrintQueueTypes.Connections });
                foreach (var q in queues)
                {
                    string fullName = q.FullName.Trim();
                    string port = q.QueuePort?.Name ?? "";
                    if (!fullName.StartsWith(@"\\") && !port.StartsWith(@"\\")) continue;

                    string shortName = q.Name.Trim();
                    if (shortName.Contains('\\')) shortName = Path.GetFileName(shortName);
                    if (!string.IsNullOrEmpty(shortName)) map[shortName] = fullName;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Erreur lecture spouleur : {ex.Message}");
            }
            return map;
        }

        public static Task<bool> InstallAsync(string uncPath) => RunPrintUiAsync("/in", uncPath);

        public static Task<bool> RemoveAsync(string fullName) => RunPrintUiAsync("/dn", fullName);

        public static bool SetDefault(string fullName)
        {
            try { return SetDefaultPrinter(fullName); }
            catch (Exception ex) { Debug.WriteLine($"Erreur SetDefaultPrinter : {ex.Message}"); return false; }
        }

        // printui.dll en mode silencieux, sans shell : le nom passe en argument séparé (pas d'injection possible)
        private static async Task<bool> RunPrintUiAsync(string action, string target)
        {
            if (string.IsNullOrWhiteSpace(target) || target.IndexOfAny(ForbiddenChars) >= 0)
            {
                Debug.WriteLine($"[SECURITY] Nom d'imprimante rejeté : {target}");
                return false;
            }

            try
            {
                var psi = new ProcessStartInfo
                {
                    // Chemin absolu : évite qu'un rundll32.exe placé dans le dossier courant soit exécuté à la place
                    FileName = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "rundll32.exe"),
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                psi.ArgumentList.Add("printui.dll,PrintUIEntry");
                psi.ArgumentList.Add(action);
                psi.ArgumentList.Add("/q");
                psi.ArgumentList.Add("/n");
                psi.ArgumentList.Add(target);

                using var process = Process.Start(psi);
                if (process == null) return false;

                var exited = process.WaitForExitAsync();
                if (await Task.WhenAny(exited, Task.Delay(TimeSpan.FromSeconds(60))) != exited)
                {
                    try { process.Kill(); } catch { }
                    return false;
                }
                return process.ExitCode == 0;
            }
            catch (Win32Exception ex)
            {
                Debug.WriteLine($"[PRINTUI] {ex.Message}");
                return false;
            }
        }
    }
}
