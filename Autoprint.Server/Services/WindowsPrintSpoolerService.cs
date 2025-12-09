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
        private const int PRINTER_ATTRIBUTE_QUEUED = 0x00000001;
        private const int PRINTER_ATTRIBUTE_DIRECT = 0x00000002;

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
        private struct PRINTER_INFO_2
        {
            [MarshalAs(UnmanagedType.LPStr)] public string pServerName;
            [MarshalAs(UnmanagedType.LPStr)] public string pPrinterName;
            [MarshalAs(UnmanagedType.LPStr)] public string pShareName;
            [MarshalAs(UnmanagedType.LPStr)] public string pPortName;
            [MarshalAs(UnmanagedType.LPStr)] public string pDriverName;
            [MarshalAs(UnmanagedType.LPStr)] public string pComment;
            [MarshalAs(UnmanagedType.LPStr)] public string pLocation;
            public IntPtr pDevMode;
            [MarshalAs(UnmanagedType.LPStr)] public string pSepFile;
            [MarshalAs(UnmanagedType.LPStr)] public string pPrintProcessor;
            [MarshalAs(UnmanagedType.LPStr)] public string pDatatype;
            [MarshalAs(UnmanagedType.LPStr)] public string pParameters;
            public IntPtr pSecurityDescriptor;
            public uint Attributes; // Cible de la modification
            public uint Priority;
            public uint DefaultPriority;
            public uint StartTime;
            public uint UntilTime;
            public uint Status;
            public uint cJobs;
            public uint AveragePPM;
        }

        [DllImport("winspool.Drv", EntryPoint = "OpenPrinterA", SetLastError = true, CharSet = CharSet.Ansi, ExactSpelling = true, CallingConvention = CallingConvention.StdCall)]
        private static extern bool OpenPrinter([MarshalAs(UnmanagedType.LPStr)] string szPrinter, out IntPtr hPrinter, ref PRINTER_DEFAULTS pd);

        [DllImport("winspool.Drv", EntryPoint = "ClosePrinter", SetLastError = true, ExactSpelling = true, CallingConvention = CallingConvention.StdCall)]
        private static extern bool ClosePrinter(IntPtr hPrinter);

        [DllImport("winspool.drv", CharSet = CharSet.Ansi, SetLastError = true)]
        private static extern bool GetPrinter(IntPtr hPrinter, int Level, IntPtr pPrinter, int cbBuf, out int pcbNeeded);

        [DllImport("winspool.drv", CharSet = CharSet.Ansi, SetLastError = true)]
        private static extern bool SetPrinter(IntPtr hPrinter, int Level, IntPtr pPrinter, int Command);

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

        private const int PRINTER_ACCESS_ADMINISTER = 0x00000004;
        private const int PRINTER_ACCESS_USE = 0x00000008;
        private const int STANDARD_RIGHTS_REQUIRED = 0x000F0000;
        private const int PRINTER_ALL_ACCESS = (STANDARD_RIGHTS_REQUIRED | PRINTER_ACCESS_ADMINISTER | PRINTER_ACCESS_USE);

        private const uint REG_DWORD = 4;
        private const int ERROR_SUCCESS = 0;

        public Task CreerPortTcp(string ipAddress)
        {
            try
            {
                if (PortExists(ipAddress)) return Task.CompletedTask;
                var portClass = new ManagementClass("Win32_TCPIPPrinterPort");
                var newPort = portClass.CreateInstance();
                newPort["Name"] = "IP_" + ipAddress;
                newPort["Protocol"] = 1;
                newPort["HostAddress"] = ipAddress;
                newPort["PortNumber"] = 9100;
                newPort["SNMPEnabled"] = false;
                newPort.Put();
            }
            catch (Exception ex) { Console.WriteLine($"Erreur port {ipAddress}: {ex.Message}"); }
            return Task.CompletedTask;
        }

        public async Task CreerImprimante(string nom, string driverName, string ipAddress, bool enableDirectMode)
        {
            try
            {
                var printerClass = new ManagementClass("Win32_Printer");
                var newPrinter = printerClass.CreateInstance();
                newPrinter["Name"] = nom;
                newPrinter["DriverName"] = driverName;
                newPrinter["PortName"] = "IP_" + ipAddress;
                newPrinter["DeviceID"] = nom;
                newPrinter["Shared"] = true;
                newPrinter["ShareName"] = nom;
                newPrinter.Put();

                await SetDirectPrintingMode(nom, enableDirectMode);
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

        public async Task ModifierImprimante(string nomActuel, string? comment, string? location, bool enableDirectMode)
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

                await SetDirectPrintingMode(nomActuel, enableDirectMode);
            }
            catch (Exception ex) { throw new Exception($"Erreur modif: {ex.Message}"); }
        }


        // --- VERSION PRODUCTION (CORRECTIF SÉCURITÉ APPLIQUÉ) ---
        private Task SetDirectPrintingMode(string printerName, bool enableDirect)
        {
            IntPtr hPrinter = IntPtr.Zero;
            IntPtr pPrinterInfo = IntPtr.Zero;

            try
            {
                PRINTER_DEFAULTS defaults = new PRINTER_DEFAULTS
                {
                    pDatatype = IntPtr.Zero,
                    pDevMode = IntPtr.Zero,
                    DesiredAccess = PRINTER_ALL_ACCESS
                };

                if (!OpenPrinter(printerName, out hPrinter, ref defaults))
                {
                    // On loggue juste, pour ne pas crasher si l'admin a supprimé l'imprimante entre temps
                    Console.WriteLine($"[Win32 Error] Impossible d'ouvrir '{printerName}'. Code: {Marshal.GetLastWin32Error()}");
                    return Task.CompletedTask;
                }

                // 1. REGISTRE (Classique)
                int regData = enableDirect ? 1 : 0;
                SetPrinterDataEx(hPrinter, "PrinterDriverData", "EnableBranchOfficePrinting", REG_DWORD, ref regData, 4);

                // 2. ATTRIBUTS (Le correctif est ici)
                GetPrinter(hPrinter, 2, IntPtr.Zero, 0, out int needed);
                if (needed > 0)
                {
                    pPrinterInfo = Marshal.AllocHGlobal(needed);
                    if (GetPrinter(hPrinter, 2, pPrinterInfo, needed, out _))
                    {
                        PRINTER_INFO_2 info = Marshal.PtrToStructure<PRINTER_INFO_2>(pPrinterInfo);
                        uint oldAttributes = info.Attributes;

                        // --- LE FIX CRITIQUE ---
                        // On force le pointeur de sécurité à NULL.
                        // Cela dit à SetPrinter : "Ignore la sécurité, garde celle existante".
                        // C'est ce qui débloque l'écriture des attributs.
                        info.pSecurityDescriptor = IntPtr.Zero;
                        // -----------------------

                        if (enableDirect)
                        {
                            // Active DIRECT, Désactive QUEUED
                            info.Attributes |= (uint)PRINTER_ATTRIBUTE_DIRECT;
                            info.Attributes &= ~(uint)PRINTER_ATTRIBUTE_QUEUED;
                        }
                        else
                        {
                            // Retour standard
                            info.Attributes &= ~(uint)PRINTER_ATTRIBUTE_DIRECT;
                            info.Attributes |= (uint)PRINTER_ATTRIBUTE_QUEUED;
                        }

                        // On applique si changement détecté
                        if (info.Attributes != oldAttributes)
                        {
                            Marshal.StructureToPtr(info, pPrinterInfo, false);

                            // On tente l'écriture
                            if (!SetPrinter(hPrinter, 2, pPrinterInfo, 0))
                            {
                                int err = Marshal.GetLastWin32Error();
                                // Ici on peut lever une exception car c'est une vraie erreur technique
                                throw new Exception($"Echec écriture attributs Win32. Code erreur: {err}");
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // On remonte l'exception pour qu'elle apparaisse dans les logs d'Audit
                throw new Exception($"Erreur configuration Mode Direct : {ex.Message}");
            }
            finally
            {
                if (pPrinterInfo != IntPtr.Zero) Marshal.FreeHGlobal(pPrinterInfo);
                if (hPrinter != IntPtr.Zero) ClosePrinter(hPrinter);
            }

            return Task.CompletedTask;
        }

        public Task<bool> VerifierModeDirect(string nomImprimante, bool modeAttendu)
        {
            IntPtr hPrinter = IntPtr.Zero;
            IntPtr pPrinterInfo = IntPtr.Zero;
            try
            {
                PRINTER_DEFAULTS defaults = new PRINTER_DEFAULTS
                {
                    pDatatype = IntPtr.Zero,
                    pDevMode = IntPtr.Zero,
                    DesiredAccess = PRINTER_ACCESS_USE
                };

                if (!OpenPrinter(nomImprimante, out hPrinter, ref defaults)) return Task.FromResult(false);

                bool regEnabled = false;
                if (GetPrinterDataEx(hPrinter, "PrinterDriverData", "EnableBranchOfficePrinting", out _, out int data, 4, out _) == ERROR_SUCCESS)
                {
                    regEnabled = (data == 1);
                }

                bool attrEnabled = false;
                GetPrinter(hPrinter, 2, IntPtr.Zero, 0, out int needed);
                if (needed > 0)
                {
                    pPrinterInfo = Marshal.AllocHGlobal(needed);
                    if (GetPrinter(hPrinter, 2, pPrinterInfo, needed, out _))
                    {
                        PRINTER_INFO_2 info = Marshal.PtrToStructure<PRINTER_INFO_2>(pPrinterInfo);
                        bool isDirect = (info.Attributes & PRINTER_ATTRIBUTE_DIRECT) == PRINTER_ATTRIBUTE_DIRECT;
                        bool isNotQueued = (info.Attributes & PRINTER_ATTRIBUTE_QUEUED) != PRINTER_ATTRIBUTE_QUEUED;
                        attrEnabled = isDirect && isNotQueued;
                    }
                }

                if (modeAttendu) return Task.FromResult(regEnabled || attrEnabled);
                else return Task.FromResult(!regEnabled && !attrEnabled);
            }
            catch { return Task.FromResult(false); }
            finally
            {
                if (pPrinterInfo != IntPtr.Zero) Marshal.FreeHGlobal(pPrinterInfo);
                if (hPrinter != IntPtr.Zero) ClosePrinter(hPrinter);
            }
        }

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