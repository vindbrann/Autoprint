using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Management;
using Autoprint.Shared;
using Autoprint.Shared.DTOs;
using Autoprint.Spooler;
using static Autoprint.Spooler.SpoolerNative;

namespace Autoprint.Server.Services
{
    [SupportedOSPlatform("windows")]
    public class WindowsPrintSpoolerService : IPrintSpoolerService
    {
        // Écritures : déléguées au service Windows AutoprintSpooler (mode « Service ») ou exécutées sur place (mode « InProcess »).
        // Lectures : faites ici, elles ne demandent aucun privilège.
        private readonly ISpoolerAdmin _admin;

        public WindowsPrintSpoolerService(ISpoolerAdmin admin)
        {
            _admin = admin;
        }

        public Task CreerPortTcp(string ipAddress) => _admin.CreatePortAsync(ipAddress);

        public Task CreerImprimante(string nom, string driverName, string ipAddress, bool enableDirectMode)
            => _admin.CreatePrinterAsync(nom, driverName, ipAddress, enableDirectMode);

        public Task ModifierImprimante(string nomActuel, string? comment, string? location, bool enableDirectMode, string? shareName, bool isShared, string? forcePortIp = null)
            => _admin.UpdatePrinterAsync(nomActuel, comment, location, enableDirectMode, shareName, isShared, forcePortIp);

        public Task RenommerImprimante(string ancienNom, string nouveauNom) => _admin.RenamePrinterAsync(ancienNom, nouveauNom);

        public Task SupprimerImprimante(string nom) => _admin.DeletePrinterAsync(nom);



        public Task<string?> RecupererNomImprimanteParIp(string ipAddress)
        {
            return Task.Run<string?>(() =>
            {
                string cleanIp = ipAddress.Trim();
                string targetPortPattern = $"IP_{cleanIp}";

                var allPrinters = GetAllPrintersNative();

                // Port exact : « IP_10.0.0.1 » ne doit pas correspondre à « IP_10.0.0.10 » (B66)
                var match = allPrinters.FirstOrDefault(p =>
                    string.Equals(p.pPortName, targetPortPattern, StringComparison.OrdinalIgnoreCase));

                return match.pPrinterName;
            });
        }

        public Task<List<KeyValuePair<string, string>>> ListerFilesLocales()
        {
            return Task.Run(() => GetAllPrintersNative()
                .Where(p => !string.IsNullOrEmpty(p.pPrinterName))
                .Select(p => new KeyValuePair<string, string>(p.pPrinterName, p.pPortName ?? ""))
                .ToList());
        }

        private List<PRINTER_INFO_2> GetAllPrintersNative()
        {
            try { return EnumererFilesNatives(); }
            catch { return new List<PRINTER_INFO_2>(); }
        }

        /// <summary>Files locales via EnumPrinters (niveau 2), sans WMI ni privilège. Lève une exception si Windows refuse la lecture.</summary>
        private static List<PRINTER_INFO_2> EnumererFilesNatives()
        {
            var results = new List<PRINTER_INFO_2>();
            IntPtr pPrinters = IntPtr.Zero;
            try
            {
                // Premier appel : taille nécessaire. Sans aucune file, il réussit avec 0 octet (parc réellement vide).
                if (!EnumPrinters(PRINTER_ENUM_LOCAL, null, 2, IntPtr.Zero, 0, out int bytesNeeded, out _))
                {
                    int err = Marshal.GetLastWin32Error();
                    if (err != ERROR_INSUFFICIENT_BUFFER) throw new System.ComponentModel.Win32Exception(err);
                }
                if (bytesNeeded <= 0) return results;

                pPrinters = Marshal.AllocHGlobal(bytesNeeded);
                if (!EnumPrinters(PRINTER_ENUM_LOCAL, null, 2, pPrinters, bytesNeeded, out _, out int countReturned))
                    throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());

                int stride = Marshal.SizeOf<PRINTER_INFO_2>();
                for (int i = 0; i < countReturned; i++)
                    results.Add(Marshal.PtrToStructure<PRINTER_INFO_2>(IntPtr.Add(pPrinters, i * stride)));
                return results;
            }
            finally
            {
                if (pPrinters != IntPtr.Zero) Marshal.FreeHGlobal(pPrinters);
            }
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

        public Task<List<DiscoveredPrinterDto>> ScanLocalPrintersAsync()
        {
            var results = new List<DiscoveredPrinterDto>();
            ManagementScope scope;
            try
            {
                var options = new ConnectionOptions { Impersonation = ImpersonationLevel.Impersonate, Timeout = TimeSpan.FromSeconds(10) };
                scope = new ManagementScope(@"\\.\root\cimv2", options);
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
            catch (UnauthorizedAccessException) { throw new Exception("Accès refusé au spouleur local (Erreur WMI)."); }
            catch (Exception ex) { throw new Exception($"Erreur WMI (spouleur local): {ex.Message}"); }
            return Task.FromResult(results);
        }

        public Task<List<WinPrinterInfo>> ListerFilesPourAudit()
        {
            // #41 : même lecture que la synchro (EnumPrinters), qui fonctionne sous le compte limité du pool IIS en mode Service.
            // L'ancienne lecture WMI y échouait en silence et renvoyait un parc vide : toutes les imprimantes passaient en erreur.
            return Task.Run(() => EnumererFilesNatives()
                .Where(p => !string.IsNullOrEmpty(p.pPrinterName))
                .Select(p => new WinPrinterInfo
                {
                    Name = p.pPrinterName,
                    PortName = p.pPortName ?? "",
                    IsDirect = (p.Attributes & PRINTER_ATTRIBUTE_DIRECT) == PRINTER_ATTRIBUTE_DIRECT
                })
                .ToList());
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