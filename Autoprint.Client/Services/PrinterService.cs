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

        // Connexion à une imprimante partagée sans aucune fenêtre Windows (pas de « Ajout de l'imprimante » qui volerait le focus
        // et refermerait le panneau) : si Windows aurait dû demander quelque chose, la connexion échoue et le panneau l'indique.
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct PRINTER_CONNECTION_INFO_1
        {
            public uint dwFlags;
            public string? pszDriverName;
        }

        private const uint PRINTER_CONNECTION_NO_UI = 0x00000040;

        [DllImport("winspool.drv", CharSet = CharSet.Unicode, SetLastError = true, EntryPoint = "AddPrinterConnection2W")]
        private static extern bool AddPrinterConnection2(IntPtr hWnd, string pszName, uint dwLevel, ref PRINTER_CONNECTION_INFO_1 pConnectionInfo);

        [DllImport("winspool.drv", CharSet = CharSet.Unicode, SetLastError = true, EntryPoint = "DeletePrinterConnectionW")]
        private static extern bool DeletePrinterConnection(string pName);

        public static Task<bool> InstallAsync(string uncPath) => Task.Run(() =>
        {
            if (!IsSafeName(uncPath)) return false;
            var info = new PRINTER_CONNECTION_INFO_1 { dwFlags = PRINTER_CONNECTION_NO_UI, pszDriverName = null };
            if (AddPrinterConnection2(IntPtr.Zero, uncPath, 1, ref info)) return true;
            Debug.WriteLine($"[SPOULEUR] Connexion à {uncPath} impossible (erreur Windows {Marshal.GetLastWin32Error()})");
            return false;
        });

        public static Task<bool> RemoveAsync(string fullName) => Task.Run(() =>
        {
            if (!IsSafeName(fullName)) return false;
            if (DeletePrinterConnection(fullName)) return true;
            Debug.WriteLine($"[SPOULEUR] Retrait de {fullName} impossible (erreur Windows {Marshal.GetLastWin32Error()})");
            return false;
        });

        private static bool IsSafeName(string name)
        {
            if (!string.IsNullOrWhiteSpace(name) && name.StartsWith(@"\\") && name.IndexOfAny(ForbiddenChars) < 0) return true;
            Debug.WriteLine($"[SECURITY] Nom d'imprimante rejeté : {name}");
            return false;
        }

        public static bool SetDefault(string fullName)
        {
            try { return SetDefaultPrinter(fullName); }
            catch (Exception ex) { Debug.WriteLine($"Erreur SetDefaultPrinter : {ex.Message}"); return false; }
        }

    }
}
