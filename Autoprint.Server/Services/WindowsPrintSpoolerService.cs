using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Management;
using Autoprint.Shared;
using Autoprint.Shared.DTOs;

namespace Autoprint.Server.Services
{
    [SupportedOSPlatform("windows")]
    public class WindowsPrintSpoolerService : IPrintSpoolerService
    {
        // --- API WIN32 (P/Invoke) ---
        [DllImport("winspool.Drv", EntryPoint = "OpenPrinterA", SetLastError = true, CharSet = CharSet.Ansi, ExactSpelling = true, CallingConvention = CallingConvention.StdCall)]
        private static extern bool OpenPrinter([MarshalAs(UnmanagedType.LPStr)] string szPrinter, out IntPtr hPrinter, ref PRINTER_DEFAULTS pd);

        [DllImport("winspool.Drv", EntryPoint = "ClosePrinter", SetLastError = true, ExactSpelling = true, CallingConvention = CallingConvention.StdCall)]
        private static extern bool ClosePrinter(IntPtr hPrinter);

        // API pour le Registre Imprimante (C'est la seule qui compte désormais)
        [DllImport("winspool.drv", EntryPoint = "SetPrinterDataExA", SetLastError = true, CharSet = CharSet.Ansi)]
        private static extern uint SetPrinterDataEx(IntPtr hPrinter, string pKeyName, string pValueName, uint Type, ref int pData, int cbData);

        [DllImport("winspool.drv", EntryPoint = "GetPrinterDataExA", SetLastError = true, CharSet = CharSet.Ansi)]
        private static extern uint GetPrinterDataEx(IntPtr hPrinter, string pKeyName, string pValueName, out uint pType, out int pData, int nndSize, out int pcbNeeded);

        [StructLayout(LayoutKind.Sequential)]
        private struct PRINTER_DEFAULTS
        {
            public IntPtr pDatatype;
            public IntPtr pDevMode;
            public int DesiredAccess;
        }

        // Droits d'accès
        private const int PRINTER_ACCESS_ADMINISTER = 0x00000004;
        private const int PRINTER_ACCESS_USE = 0x00000008;
        private const int STANDARD_RIGHTS_REQUIRED = 0x000F0000;
        private const int PRINTER_ALL_ACCESS = (STANDARD_RIGHTS_REQUIRED | PRINTER_ACCESS_ADMINISTER | PRINTER_ACCESS_USE);

        // Types Registre
        private const uint REG_DWORD = 4;
        private const int ERROR_SUCCESS = 0;

        // --- IMPLEMENTATION ---

        public Task CreerPortTcp(string ipAddress)
        {
            try
            {
                if (PortExists(ipAddress)) return Task.CompletedTask;
                var portClass = new ManagementClass("Win32_TCPIPPrinterPort");
                var newPort = portClass.CreateInstance();
                newPort["Name"] = "IP_" + ipAddress;
                newPort["Protocol"] = 1; // RAW
                newPort["HostAddress"] = ipAddress;
                newPort["PortNumber"] = 9100;
                newPort["SNMPEnabled"] = false;
                newPort.Put();
            }
            catch (Exception ex) { Console.WriteLine($"Erreur port {ipAddress}: {ex.Message}"); }
            return Task.CompletedTask;
        }

        public async Task CreerImprimante(string nom, string driverName, string ipAddress, bool enableBODP)
        {
            try
            {
                // Création WMI standard
                var printerClass = new ManagementClass("Win32_Printer");
                var newPrinter = printerClass.CreateInstance();
                newPrinter["Name"] = nom;
                newPrinter["DriverName"] = driverName;
                newPrinter["PortName"] = "IP_" + ipAddress;
                newPrinter["DeviceID"] = nom;
                newPrinter["Shared"] = true;
                newPrinter["ShareName"] = nom;
                newPrinter.Put();

                // Application Config
                await SetRenderingMode(nom, enableBODP);
            }
            catch (Exception ex) { throw new Exception($"Erreur création: {ex.Message}"); }
        }

        public Task SupprimerImprimante(string nom)
        {
            try
            {
                var query = new SelectQuery("Win32_Printer", $"Name = '{nom}'");
                using var searcher = new ManagementObjectSearcher(query);
                foreach (ManagementObject printer in searcher.Get()) printer.Delete();
            }
            catch { }
            return Task.CompletedTask;
        }

        public async Task ModifierImprimante(string nomActuel, string? comment, string? location, bool enableBODP)
        {
            try
            {
                var query = new SelectQuery("Win32_Printer", $"Name = '{nomActuel}'");
                using var searcher = new ManagementObjectSearcher(query);
                foreach (ManagementObject printer in searcher.Get())
                {
                    bool changed = false;
                    if (comment != null && printer["Comment"]?.ToString() != comment) { printer["Comment"] = comment; changed = true; }
                    if (location != null && printer["Location"]?.ToString() != location) { printer["Location"] = location; changed = true; }
                    if (changed) printer.Put();
                }

                await SetRenderingMode(nomActuel, enableBODP);
            }
            catch (Exception ex) { throw new Exception($"Erreur modif: {ex.Message}"); }
        }

        // --- CŒUR DU SYSTÈME : Registre uniquement ---
        private Task SetRenderingMode(string printerName, bool useClientSideRendering)
        {
            IntPtr hPrinter = IntPtr.Zero;
            try
            {
                // Ouverture Admin requise pour SetPrinterDataEx
                PRINTER_DEFAULTS defaults = new PRINTER_DEFAULTS
                {
                    pDatatype = IntPtr.Zero,
                    pDevMode = IntPtr.Zero,
                    DesiredAccess = PRINTER_ALL_ACCESS
                };

                if (!OpenPrinter(printerName, out hPrinter, ref defaults))
                {
                    Console.WriteLine($"[Win32] Impossible d'ouvrir '{printerName}' en Admin. Code: {Marshal.GetLastWin32Error()}");
                    return Task.CompletedTask;
                }

                // Basé sur vos tests V3 et V4 : Seule cette clé compte.
                int data = useClientSideRendering ? 1 : 0;

                // Ecriture dans HKLM\SYSTEM\CurrentControlSet\Control\Print\Printers\<Name>\PrinterDriverData
                uint result = SetPrinterDataEx(hPrinter, "PrinterDriverData", "EnableBranchOfficePrinting", REG_DWORD, ref data, 4);

                if (result != ERROR_SUCCESS)
                {
                    Console.WriteLine($"[Win32] Echec SetPrinterDataEx. Code erreur: {result}");
                }
            }
            catch (Exception ex) { Console.WriteLine($"[Win32 Exception] {ex.Message}"); }
            finally { if (hPrinter != IntPtr.Zero) ClosePrinter(hPrinter); }

            return Task.CompletedTask;
        }

        // --- VERIFICATEUR ---
        public Task<bool> VerifierModeFiliale(string nomImprimante, bool modeAttendu)
        {
            IntPtr hPrinter = IntPtr.Zero;
            try
            {
                // Lecture seule suffit
                PRINTER_DEFAULTS defaults = new PRINTER_DEFAULTS
                {
                    pDatatype = IntPtr.Zero,
                    pDevMode = IntPtr.Zero,
                    DesiredAccess = PRINTER_ACCESS_USE
                };

                if (!OpenPrinter(nomImprimante, out hPrinter, ref defaults)) return Task.FromResult(false);

                string keyName = "PrinterDriverData";
                string valueName = "EnableBranchOfficePrinting";
                uint type;
                int data;
                int needed;

                uint result = GetPrinterDataEx(hPrinter, keyName, valueName, out type, out data, 4, out needed);

                if (result == ERROR_SUCCESS)
                {
                    // La clé existe, on vérifie la valeur (1 = Activé, 0 = Désactivé)
                    bool isEnabled = (data == 1);
                    return Task.FromResult(isEnabled == modeAttendu);
                }
                else
                {
                    // La clé n'existe pas (cas par défaut)
                    // Par défaut, le mode filiale est désactivé (0).
                    // Donc si la clé est absente et qu'on attendait "Faux", c'est bon.
                    // Si on attendait "Vrai", c'est pas bon.
                    return Task.FromResult(modeAttendu == false);
                }
            }
            catch { return Task.FromResult(false); }
            finally { if (hPrinter != IntPtr.Zero) ClosePrinter(hPrinter); }
        }

        // ... Reste inchangé ...
        public Task<string?> RecupererNomImprimanteParIp(string ipAddress)
        {
            try
            {
                string portName = $"IP_{ipAddress}";
                var query = new SelectQuery("Win32_Printer", $"PortName = '{portName}'");
                using var searcher = new ManagementObjectSearcher(query);
                foreach (ManagementObject printer in searcher.Get()) return Task.FromResult<string?>(printer["Name"]?.ToString());
            }
            catch { }
            return Task.FromResult<string?>(null);
        }

        public Task RenommerImprimante(string ancienNom, string nouveauNom)
        {
            try
            {
                var query = new SelectQuery("Win32_Printer", $"Name = '{ancienNom}'");
                using var searcher = new ManagementObjectSearcher(query);
                foreach (ManagementObject printer in searcher.Get())
                {
                    var paramsCim = printer.GetMethodParameters("RenamePrinter");
                    paramsCim["NewPrinterName"] = nouveauNom;
                    printer.InvokeMethod("RenamePrinter", paramsCim, null);
                    return Task.CompletedTask;
                }
                throw new Exception($"Imprimante '{ancienNom}' introuvable.");
            }
            catch (Exception ex) { throw new Exception($"Erreur renommage : {ex.Message}"); }
        }

        private bool PortExists(string ip)
        {
            try { using var searcher = new ManagementObjectSearcher($"SELECT * FROM Win32_TCPIPPrinterPort WHERE HostAddress = '{ip}'"); return searcher.Get().Count > 0; } catch { return false; }
        }

        public Task<List<DiscoveredPrinterDto>> ScanPrintersAsync(string targetHost, string? username, string? password)
        {
            var results = new List<DiscoveredPrinterDto>();
            ManagementScope scope;
            try
            {
                var options = new ConnectionOptions { Impersonation = ImpersonationLevel.Impersonate, Authentication = AuthenticationLevel.PacketPrivacy, Timeout = TimeSpan.FromSeconds(10) };
                if (!string.IsNullOrWhiteSpace(username) && !string.IsNullOrWhiteSpace(password)) { options.Username = username; options.Password = password; }
                string path = $@"\\{targetHost}\root\cimv2";
                scope = new ManagementScope(path, options);
                scope.Connect();

                var portMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                try
                {
                    using var searcherPorts = new ManagementObjectSearcher(scope, new ObjectQuery("SELECT Name, HostAddress FROM Win32_TCPIPPrinterPort"));
                    foreach (ManagementObject port in searcherPorts.Get())
                    {
                        string pName = GetWmiValue(port, "Name");
                        string pIp = GetWmiValue(port, "HostAddress");
                        if (!string.IsNullOrEmpty(pName) && !string.IsNullOrEmpty(pIp) && !portMap.ContainsKey(pName)) portMap.Add(pName, pIp);
                    }
                }
                catch { }

                using var searcher = new ManagementObjectSearcher(scope, new ObjectQuery("SELECT * FROM Win32_Printer WHERE Local = TRUE"));
                foreach (ManagementObject printer in searcher.Get())
                {
                    string rawPortName = GetWmiValue(printer, "PortName");
                    string resolvedIp = portMap.ContainsKey(rawPortName) ? portMap[rawPortName] : rawPortName;
                    if (rawPortName.StartsWith("WSD-", StringComparison.OrdinalIgnoreCase)) resolvedIp = "";

                    results.Add(new DiscoveredPrinterDto
                    {
                        Name = GetWmiValue(printer, "Name"),
                        DriverName = GetWmiValue(printer, "DriverName"),
                        PortName = resolvedIp,
                        IsShared = (bool)(printer["Shared"] ?? false),
                        ShareName = GetWmiValue(printer, "ShareName")
                    });
                }
            }
            catch (UnauthorizedAccessException) { throw new Exception("Accès refusé au serveur distant (Erreur WMI)."); }
            catch (Exception ex) { throw new Exception($"Erreur WMI ({targetHost}): {ex.Message}"); }
            return Task.FromResult(results);
        }

        public Task<List<Pilote>> GetInstalledDriversAsync()
        {
            var results = new List<Pilote>();
            try
            {
                var options = new ConnectionOptions { Impersonation = ImpersonationLevel.Impersonate, EnablePrivileges = true };
                var scope = new ManagementScope(@"\\.\root\cimv2", options);
                scope.Connect();
                using var searcher = new ManagementObjectSearcher(scope, new ObjectQuery("SELECT Name, Version FROM Win32_PrinterDriver"));
                foreach (ManagementObject driver in searcher.Get())
                {
                    string rawName = GetWmiValue(driver, "Name");
                    if (string.IsNullOrWhiteSpace(rawName)) continue;
                    string cleanName = rawName.Split(',')[0].Trim();
                    if (cleanName.Contains("Microsoft") || cleanName.Contains("Fax")) continue;
                    string rawEnv = GetWmiValue(driver, "Version");
                    string displayType = rawEnv switch { "3" => "Type 3 (Legacy)", "4" => "Type 4 (V4)", "Windows x64" => "Windows 64-bit", "Windows NT x86" => "Windows 32-bit", _ => rawEnv };
                    results.Add(new Pilote { Nom = cleanName, Version = displayType, EstInstalle = true });
                }
            }
            catch (Exception ex) { Console.WriteLine("Erreur WMI Drivers: " + ex.Message); }
            return Task.FromResult(results);
        }

        private string GetWmiValue(ManagementBaseObject obj, string propertyName)
        {
            try { return obj[propertyName]?.ToString() ?? ""; } catch { return ""; }
        }
    }
}