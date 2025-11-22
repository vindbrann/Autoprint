using System.Management;
using System.Runtime.Versioning;
using Autoprint.Shared.DTOs;

namespace Autoprint.Server.Services
{
    [SupportedOSPlatform("windows")]
    public class WindowsPrintSpoolerService : IPrintSpoolerService
    {
        // ============================================================
        // 1. GESTION (ACTIONS SYSTÈME)
        // ============================================================

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
            catch (Exception ex)
            {
                Console.WriteLine($"Erreur création port {ipAddress}: {ex.Message}");
            }
            return Task.CompletedTask;
        }

        public Task CreerImprimante(string nom, string driverName, string ipAddress)
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
            }
            catch (Exception ex)
            {
                throw new Exception($"Erreur création imprimante Windows: {ex.Message}");
            }
            return Task.CompletedTask;
        }

        public Task SupprimerImprimante(string nom)
        {
            try
            {
                var query = new SelectQuery("Win32_Printer", $"Name = '{nom}'");
                using var searcher = new ManagementObjectSearcher(query);
                foreach (ManagementObject printer in searcher.Get())
                {
                    printer.Delete();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erreur suppression {nom}: {ex.Message}");
            }
            return Task.CompletedTask;
        }

        public Task ModifierImprimante(string nomActuel, string? comment, string? location)
        {
            try
            {
                var query = new SelectQuery("Win32_Printer", $"Name = '{nomActuel}'");
                using var searcher = new ManagementObjectSearcher(query);

                foreach (ManagementObject printer in searcher.Get())
                {
                    bool changed = false;

                    // Mapping : Le "Commentaire" Windows reçoit ta "Localisation" (Détails)
                    if (comment != null && printer["Comment"]?.ToString() != comment)
                    {
                        printer["Comment"] = comment;
                        changed = true;
                    }

                    // Mapping : La "Location" Windows reçoit ton "Emplacement" (Bâtiment)
                    if (location != null && printer["Location"]?.ToString() != location)
                    {
                        printer["Location"] = location;
                        changed = true;
                    }

                    if (changed) printer.Put();
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Erreur modif Windows: {ex.Message}");
            }
            return Task.CompletedTask;
        }

        // --- NOUVELLES MÉTHODES INTELLIGENTES ---

        public Task<string?> RecupererNomImprimanteParIp(string ipAddress)
        {
            try
            {
                // On cherche l'imprimante qui utilise le port "IP_xxx"
                string portName = $"IP_{ipAddress}";
                // Note: WQL est sensible aux 'quotes'
                var query = new SelectQuery("Win32_Printer", $"PortName = '{portName}'");
                using var searcher = new ManagementObjectSearcher(query);

                foreach (ManagementObject printer in searcher.Get())
                {
                    return Task.FromResult<string?>(printer["Name"]?.ToString());
                }
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
                    // La méthode RenamePrinter() est disponible sur Win32_Printer
                    var paramsCim = printer.GetMethodParameters("RenamePrinter");
                    paramsCim["NewPrinterName"] = nouveauNom;
                    printer.InvokeMethod("RenamePrinter", paramsCim, null);
                    return Task.CompletedTask;
                }
                throw new Exception($"Imprimante '{ancienNom}' introuvable pour renommage.");
            }
            catch (Exception ex)
            {
                throw new Exception($"Erreur renommage : {ex.Message}");
            }
        }

        private bool PortExists(string ip)
        {
            try
            {
                using var searcher = new ManagementObjectSearcher($"SELECT * FROM Win32_TCPIPPrinterPort WHERE HostAddress = '{ip}'");
                return searcher.Get().Count > 0;
            }
            catch { return false; }
        }


        // ============================================================
        // 2. SCAN IMPRIMANTES (AVEC RÉSOLUTION D'IP)
        // ============================================================

        public Task<List<DiscoveredPrinterDto>> ScanPrintersAsync(string targetHost, string? username, string? password)
        {
            var results = new List<DiscoveredPrinterDto>();
            ManagementScope scope;

            try
            {
                var options = new ConnectionOptions
                {
                    Impersonation = ImpersonationLevel.Impersonate,
                    Authentication = AuthenticationLevel.PacketPrivacy,
                    Timeout = TimeSpan.FromSeconds(10)
                };

                if (!string.IsNullOrWhiteSpace(username) && !string.IsNullOrWhiteSpace(password))
                {
                    options.Username = username;
                    options.Password = password;
                }

                string path = $@"\\{targetHost}\root\cimv2";
                scope = new ManagementScope(path, options);
                scope.Connect();

                // Pré-chargement des Ports
                var portMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                try
                {
                    var queryPorts = new ObjectQuery("SELECT Name, HostAddress FROM Win32_TCPIPPrinterPort");
                    using var searcherPorts = new ManagementObjectSearcher(scope, queryPorts);
                    foreach (ManagementObject port in searcherPorts.Get())
                    {
                        string pName = GetWmiValue(port, "Name");
                        string pIp = GetWmiValue(port, "HostAddress");

                        if (!string.IsNullOrEmpty(pName) && !string.IsNullOrEmpty(pIp) && !portMap.ContainsKey(pName))
                        {
                            portMap.Add(pName, pIp);
                        }
                    }
                }
                catch { }

                var query = new ObjectQuery("SELECT * FROM Win32_Printer WHERE Local = TRUE");
                using var searcher = new ManagementObjectSearcher(scope, query);

                foreach (ManagementObject printer in searcher.Get())
                {
                    string rawPortName = GetWmiValue(printer, "PortName");
                    string resolvedIp = rawPortName;

                    if (portMap.ContainsKey(rawPortName))
                    {
                        resolvedIp = portMap[rawPortName];
                    }
                    else if (rawPortName.StartsWith("WSD-", StringComparison.OrdinalIgnoreCase))
                    {
                        resolvedIp = "";
                    }

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
            catch (UnauthorizedAccessException)
            {
                throw new Exception("Accès refusé au serveur distant (Erreur WMI).");
            }
            catch (Exception ex)
            {
                throw new Exception($"Erreur WMI ({targetHost}): {ex.Message}");
            }

            return Task.FromResult(results);
        }

        // ============================================================
        // 3. SCAN PILOTES
        // ============================================================

        public Task<List<DiscoveredDriverDto>> ScanLocalDriversAsync()
        {
            var results = new List<DiscoveredDriverDto>();

            try
            {
                var options = new ConnectionOptions
                {
                    Impersonation = ImpersonationLevel.Impersonate,
                    EnablePrivileges = true
                };

                var scope = new ManagementScope(@"\\.\root\cimv2", options);
                scope.Connect();

                var query = new ObjectQuery("SELECT * FROM Win32_PrinterDriver");
                using var searcher = new ManagementObjectSearcher(scope, query);

                foreach (ManagementObject driver in searcher.Get())
                {
                    string rawName = GetWmiValue(driver, "Name");
                    string cleanName = rawName.Split(',')[0];

                    string version = GetWmiValue(driver, "DriverVersion");
                    if (string.IsNullOrEmpty(version))
                    {
                        version = GetWmiValue(driver, "Version");
                    }

                    results.Add(new DiscoveredDriverDto
                    {
                        Name = cleanName,
                        DriverVersion = version
                    });
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Erreur WMI Drivers: " + ex.Message);
            }

            return Task.FromResult(results);
        }

        private string GetWmiValue(ManagementBaseObject obj, string propertyName)
        {
            try
            {
                return obj[propertyName]?.ToString() ?? "";
            }
            catch
            {
                return "";
            }
        }
    }
}