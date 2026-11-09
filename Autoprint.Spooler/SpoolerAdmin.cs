using System.Management;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using static Autoprint.Spooler.SpoolerNative;

namespace Autoprint.Spooler
{
    /// <summary>
    /// Implémentation des opérations privilégiées sur le spouleur local (code déplacé de WindowsPrintSpoolerService).
    /// Chaque paramètre est revalidé ici : c'est ce code qui s'exécute en SYSTEM.
    /// </summary>
    [SupportedOSPlatform("windows")]
    public class SpoolerAdmin : ISpoolerAdmin
    {
        public Task CreatePortAsync(string ipAddress)
        {
            string cleanIp = SpoolerValidation.RequireHostAddress(ipAddress);
            return Task.Run(() =>
            {
                string targetPortName = $"IP_{cleanIp}";
                if (PortExists(targetPortName)) return;

                var portClass = new ManagementClass("Win32_TCPIPPrinterPort");
                var newPort = portClass.CreateInstance();

                newPort["Name"] = targetPortName;
                newPort["Protocol"] = 1; // TCP
                newPort["HostAddress"] = cleanIp;
                newPort["PortNumber"] = 9100;
                newPort["SNMPEnabled"] = false;

                try { newPort.Put(); }
                catch (Exception ex)
                {
                    // Avant : échec ignoré, la création de l'imprimante échouait ensuite avec un message obscur (backlog B3)
                    if (!PortExists(targetPortName)) throw new Exception($"Création du port {targetPortName} impossible : {ex.Message}");
                }
            });
        }

        public async Task CreatePrinterAsync(string name, string driverName, string ipAddress, bool enableDirectMode)
        {
            SpoolerValidation.RequirePrinterName(name);
            SpoolerValidation.OptionalShareName(name);
            string cleanIp = SpoolerValidation.RequireHostAddress(ipAddress);
            // Seul un pilote déjà installé sur le serveur peut être utilisé : aucune installation de pilote n'est possible par ce biais
            if (!DriverIsInstalled(driverName)) throw new ArgumentException($"Pilote '{driverName}' non installé sur le serveur.");

            try
            {
                var printerClass = new ManagementClass("Win32_Printer");
                var newPrinter = printerClass.CreateInstance();
                newPrinter["Name"] = name;
                newPrinter["DriverName"] = driverName;
                newPrinter["PortName"] = "IP_" + cleanIp;
                newPrinter["DeviceID"] = name;
                newPrinter["Shared"] = true;
                newPrinter["ShareName"] = name;
                newPrinter.Put();

                await SetDirectPrintingMode(name, enableDirectMode);
            }
            catch (Exception ex) { throw new Exception($"Erreur création: {ex.Message}"); }
        }

        public async Task UpdatePrinterAsync(string currentName, string? comment, string? location, bool enableDirectMode, string? shareName, bool isShared, string? forcePortIp)
        {
            SpoolerValidation.RequirePrinterName(currentName);
            SpoolerValidation.OptionalText(comment, "Commentaire", 1024);
            SpoolerValidation.OptionalText(location, "Localisation");
            SpoolerValidation.OptionalShareName(shareName);
            if (!string.IsNullOrEmpty(forcePortIp)) forcePortIp = SpoolerValidation.RequireHostAddress(forcePortIp);

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

                if (!OpenPrinter(currentName, out hPrinter, ref defaults))
                {
                    throw new Exception($"Impossible d'ouvrir l'imprimante '{currentName}' pour modification. Code: {Marshal.GetLastWin32Error()}");
                }

                GetPrinter(hPrinter, 2, IntPtr.Zero, 0, out int needed);
                if (needed == 0) throw new Exception("Erreur lecture taille buffer.");

                pPrinterInfo = Marshal.AllocHGlobal(needed);
                if (!GetPrinter(hPrinter, 2, pPrinterInfo, needed, out _))
                {
                    throw new Exception($"Erreur GetPrinter. Code: {Marshal.GetLastWin32Error()}");
                }

                PRINTER_INFO_2 info = Marshal.PtrToStructure<PRINTER_INFO_2>(pPrinterInfo);

                info.pSecurityDescriptor = IntPtr.Zero;

                bool changed = false;

                if (comment != null && info.pComment != comment)
                {
                    info.pComment = comment;
                    changed = true;
                }

                if (location != null && info.pLocation != location)
                {
                    info.pLocation = location;
                    changed = true;
                }

                if (shareName != null && info.pShareName != shareName)
                {
                    info.pShareName = shareName;
                    changed = true;
                }

                if (isShared)
                {
                    if ((info.Attributes & PRINTER_ATTRIBUTE_SHARED) != PRINTER_ATTRIBUTE_SHARED)
                    {
                        info.Attributes |= PRINTER_ATTRIBUTE_SHARED;
                        changed = true;
                    }
                }
                else
                {
                    if ((info.Attributes & PRINTER_ATTRIBUTE_SHARED) == PRINTER_ATTRIBUTE_SHARED)
                    {
                        info.Attributes &= ~PRINTER_ATTRIBUTE_SHARED;
                        changed = true;
                    }
                }

                if (!string.IsNullOrEmpty(forcePortIp))
                {
                    string targetPort = $"IP_{forcePortIp}";
                    if (!string.Equals(info.pPortName, targetPort, StringComparison.OrdinalIgnoreCase))
                    {
                        info.pPortName = targetPort;
                        changed = true;
                    }
                }

                if (changed)
                {
                    Marshal.StructureToPtr(info, pPrinterInfo, false);
                    if (!SetPrinter(hPrinter, 2, pPrinterInfo, 0))
                    {
                        throw new Exception($"Echec SetPrinter (Update). Code: {Marshal.GetLastWin32Error()}");
                    }
                }

                await SetDirectPrintingMode(currentName, enableDirectMode);
            }
            finally
            {
                if (pPrinterInfo != IntPtr.Zero) Marshal.FreeHGlobal(pPrinterInfo);
                if (hPrinter != IntPtr.Zero) ClosePrinter(hPrinter);
            }
        }

        public Task RenamePrinterAsync(string oldName, string newName)
        {
            SpoolerValidation.RequirePrinterName(oldName, "Ancien nom");
            SpoolerValidation.RequirePrinterName(newName, "Nouveau nom");
            try
            {
                var query = new SelectQuery("Win32_Printer", $"Name = '{SpoolerValidation.EscapeWql(oldName)}'");
                using var searcher = new ManagementObjectSearcher(query);
                foreach (ManagementObject printer in searcher.Get())
                {
                    var paramsCim = printer.GetMethodParameters("RenamePrinter");
                    paramsCim["NewPrinterName"] = newName;
                    printer.InvokeMethod("RenamePrinter", paramsCim, null);
                    return Task.CompletedTask;
                }
                throw new Exception($"Imprimante '{oldName}' introuvable.");
            }
            catch (Exception ex) { throw new Exception($"Erreur renommage : {ex.Message}"); }
        }

        public Task DeletePrinterAsync(string name)
        {
            SpoolerValidation.RequirePrinterName(name);
            try
            {
                var query = new SelectQuery("Win32_Printer", $"Name = '{SpoolerValidation.EscapeWql(name)}'");
                using var searcher = new ManagementObjectSearcher(query);
                foreach (ManagementObject printer in searcher.Get()) printer.Delete();
            }
            catch (Exception ex)
            {
                // Avant : échec ignoré, la synchronisation croyait la file supprimée (backlog B3)
                throw new Exception($"Suppression de '{name}' impossible : {ex.Message}");
            }
            return Task.CompletedTask;
        }

        private static bool PortExists(string portName)
        {
            try
            {
                var query = new SelectQuery("Win32_TCPIPPrinterPort", $"Name = '{SpoolerValidation.EscapeWql(portName)}'");
                using var searcher = new ManagementObjectSearcher(query);
                return searcher.Get().Count > 0;
            }
            catch { return false; }
        }

        private static bool DriverIsInstalled(string? driverName)
        {
            if (string.IsNullOrWhiteSpace(driverName) || driverName.Any(char.IsControl)) return false;
            try
            {
                using var searcher = new ManagementObjectSearcher("SELECT Name FROM Win32_PrinterDriver");
                foreach (ManagementObject driver in searcher.Get())
                {
                    // Format WMI : « Nom du pilote,Version,Environnement »
                    var fullName = driver["Name"]?.ToString() ?? "";
                    var shortName = fullName.Split(',')[0].Trim();
                    if (string.Equals(shortName, driverName.Trim(), StringComparison.OrdinalIgnoreCase)) return true;
                }
            }
            catch { }
            return false;
        }

        private static Task SetDirectPrintingMode(string printerName, bool enableDirect)
        {
            IntPtr hPrinter = IntPtr.Zero;
            IntPtr pPrinterInfo = IntPtr.Zero;

            try
            {
                PRINTER_DEFAULTS defaults = new PRINTER_DEFAULTS
                {
                    pDatatype = IntPtr.Zero,
                    pDevMode = IntPtr.Zero,
                    DesiredAccess = PRINTER_ACCESS_ADMINISTER | PRINTER_ACCESS_USE
                };

                if (!OpenPrinter(printerName, out hPrinter, ref defaults))
                {
                    int err = Marshal.GetLastWin32Error();
                    throw new Exception($"[Win32] Impossible d'ouvrir '{printerName}'. Code: {err}");
                }

                int regData = enableDirect ? 1 : 0;
                SetPrinterDataEx(hPrinter, "PrinterDriverData", "EnableBranchOfficePrinting", REG_DWORD, ref regData, 4);

                GetPrinter(hPrinter, 2, IntPtr.Zero, 0, out int needed);
                if (needed > 0)
                {
                    pPrinterInfo = Marshal.AllocHGlobal(needed);
                    if (GetPrinter(hPrinter, 2, pPrinterInfo, needed, out _))
                    {
                        PRINTER_INFO_2 info = Marshal.PtrToStructure<PRINTER_INFO_2>(pPrinterInfo);
                        uint oldAttributes = info.Attributes;

                        info.pSecurityDescriptor = IntPtr.Zero;

                        if (enableDirect)
                        {
                            info.Attributes |= (uint)PRINTER_ATTRIBUTE_DIRECT;
                            info.Attributes &= ~(uint)PRINTER_ATTRIBUTE_QUEUED;
                        }
                        else
                        {
                            info.Attributes &= ~(uint)PRINTER_ATTRIBUTE_DIRECT;
                            info.Attributes |= (uint)PRINTER_ATTRIBUTE_QUEUED;
                        }

                        if (info.Attributes != oldAttributes)
                        {
                            Marshal.StructureToPtr(info, pPrinterInfo, false);
                            if (!SetPrinter(hPrinter, 2, pPrinterInfo, 0))
                            {
                                throw new Exception($"Echec écriture attributs Win32. Code: {Marshal.GetLastWin32Error()}");
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Erreur Config Mode Direct : {ex.Message}");
            }
            finally
            {
                if (pPrinterInfo != IntPtr.Zero) Marshal.FreeHGlobal(pPrinterInfo);
                if (hPrinter != IntPtr.Zero) ClosePrinter(hPrinter);
            }

            return Task.CompletedTask;
        }
    }
}
