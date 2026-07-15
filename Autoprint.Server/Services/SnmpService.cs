using System;
using System.Collections.Generic;
using System.Net;
using System.Net.NetworkInformation;
using System.Threading.Tasks;
using Autoprint.Shared.DTOs;
using Lextm.SharpSnmpLib;
using Lextm.SharpSnmpLib.Messaging;

namespace Autoprint.Server.Services
{
    public class SnmpService : ISnmpService
    {
        public async Task<PrinterDiagnosticResult> GetPrinterDiagnosticAsync(string ipAddress, int port, string community, int version)
        {
            var result = new PrinterDiagnosticResult();

            // 1. Test Ping
            try
            {
                using var ping = new Ping();
                var reply = await ping.SendPingAsync(ipAddress, 1000); // 1s timeout
                result.PingSuccess = reply.Status == IPStatus.Success;
                result.PingRoundtripTimeMs = reply.RoundtripTime;
            }
            catch (Exception)
            {
                result.PingSuccess = false;
            }

            if (!result.PingSuccess)
            {
                result.Status = "Éteint / Hors Ligne";
                return result;
            }

            // 2. Requête SNMP
            try
            {
                if (!IPAddress.TryParse(ipAddress, out var ip))
                {
                    var addresses = await Dns.GetHostAddressesAsync(ipAddress);
                    if (addresses.Length > 0) ip = addresses[0];
                    else throw new Exception("Impossible de résoudre le nom d'hôte");
                }

                var endpoint = new IPEndPoint(ip, port);
                var communityBytes = new OctetString(community);
                var versionCode = version == 1 ? VersionCode.V1 : VersionCode.V2;

                // Récupération des infos globales (Status, Page counter, Uptime, Ecran)
                var globalOids = new List<Variable>
                {
                    new Variable(new ObjectIdentifier("1.3.6.1.2.1.25.3.5.1.1.1")), // hrPrinterStatus
                    new Variable(new ObjectIdentifier("1.3.6.1.2.1.43.10.2.1.4.1.1")), // prtMarkerLifeCount
                    new Variable(new ObjectIdentifier("1.3.6.1.2.1.1.3.0")), // sysUpTime
                    new Variable(new ObjectIdentifier("1.3.6.1.2.1.43.16.5.1.2.1.1")), // prtConsoleDisplayBufferText Line 1
                    new Variable(new ObjectIdentifier("1.3.6.1.2.1.43.16.5.1.2.1.2"))  // prtConsoleDisplayBufferText Line 2
                };

                try
                {
                    var response = Messenger.Get(versionCode, endpoint, communityBytes, globalOids, 2000); // 2s timeout
                    if (response != null)
                    {
                        // Parse status
                        var statusVar = response[0];
                        if (statusVar.Data is Integer32 statusInt)
                        {
                            result.Status = MapSnmpStatus(statusInt.ToInt32());
                        }

                        // Parse page counter
                        var pageVar = response[1];
                        if (pageVar.Data is Integer32 pageInt)
                        {
                            result.PageCounter = pageInt.ToInt32();
                        }
                        else if (pageVar.Data is Counter32 pageCounter)
                        {
                            result.PageCounter = pageCounter.ToUInt32();
                        }

                        // Parse uptime
                        var uptimeVar = response[2];
                        if (uptimeVar.Data is TimeTicks uptimeTicks)
                        {
                            result.UptimeSeconds = uptimeTicks.ToUInt32() / 100; // centièmes de seconde -> secondes
                        }

                        // Parse console text (Affichage écran imprimante)
                        var line1Var = response[3];
                        var line2Var = response[4];
                        var consoleLines = new List<string>();
                        if (line1Var.Data is OctetString line1Str && !string.IsNullOrWhiteSpace(line1Str.ToString()))
                        {
                            consoleLines.Add(line1Str.ToString().Trim());
                        }
                        if (line2Var.Data is OctetString line2Str && !string.IsNullOrWhiteSpace(line2Str.ToString()))
                        {
                            consoleLines.Add(line2Str.ToString().Trim());
                        }

                        if (consoleLines.Count > 0)
                        {
                            string consoleText = string.Join(" | ", consoleLines);
                            result.Alerts.Add($"Écran : {consoleText}");
                        }
                    }
                }
                catch (Exception)
                {
                    result.Status = "En Ligne (SNMP partiel)";
                }

                // Récupération des consommables (via SNMP Walk sur la table Supplies)
                var supplyDescs = new List<Variable>();
                var supplyMaxes = new List<Variable>();
                var supplyLevels = new List<Variable>();

                try
                {
                    Messenger.Walk(versionCode, endpoint, communityBytes, new ObjectIdentifier("1.3.6.1.2.1.43.11.1.1.6"), supplyDescs, 2000, WalkMode.WithinSubtree);
                    Messenger.Walk(versionCode, endpoint, communityBytes, new ObjectIdentifier("1.3.6.1.2.1.43.11.1.1.8"), supplyMaxes, 2000, WalkMode.WithinSubtree);
                    Messenger.Walk(versionCode, endpoint, communityBytes, new ObjectIdentifier("1.3.6.1.2.1.43.11.1.1.9"), supplyLevels, 2000, WalkMode.WithinSubtree);

                    var tonersMap = new Dictionary<string, TonerLevelResult>();

                    foreach (var v in supplyDescs)
                    {
                        var index = GetOidIndex(v.Id);
                        if (!string.IsNullOrEmpty(index) && v.Data is OctetString descStr)
                        {
                            var desc = descStr.ToString().ToLower();
                            string friendlyColor = MapColorName(desc);

                            tonersMap[index] = new TonerLevelResult
                            {
                                Color = friendlyColor,
                                CurrentLevel = -1,
                                MaxCapacity = 100
                            };
                        }
                    }

                    foreach (var v in supplyMaxes)
                    {
                        var index = GetOidIndex(v.Id);
                        if (!string.IsNullOrEmpty(index) && tonersMap.TryGetValue(index, out var toner))
                        {
                            if (v.Data is Integer32 maxInt)
                            {
                                toner.MaxCapacity = maxInt.ToInt32();
                            }
                        }
                    }

                    foreach (var v in supplyLevels)
                    {
                        var index = GetOidIndex(v.Id);
                        if (!string.IsNullOrEmpty(index) && tonersMap.TryGetValue(index, out var toner))
                        {
                            if (v.Data is Integer32 levelInt)
                            {
                                int rawLevel = levelInt.ToInt32();
                                if (rawLevel >= 0 && toner.MaxCapacity > 0)
                                {
                                    toner.CurrentLevel = (int)Math.Round((double)rawLevel / toner.MaxCapacity * 100);
                                    if (toner.CurrentLevel > 100) toner.CurrentLevel = 100;
                                }
                                else if (rawLevel == -3)
                                {
                                    // -3 = OK (Niveau suffisant)
                                    toner.CurrentLevel = 50; 
                                }
                                else if (rawLevel == -2)
                                {
                                    // -2 = Consommable très bas / vide
                                    toner.CurrentLevel = 5;
                                }
                                else
                                {
                                    toner.CurrentLevel = -1;
                                }
                            }
                        }
                    }

                    foreach (var toner in tonersMap.Values)
                    {
                        result.Toners.Add(toner);
                    }
                }
                catch (Exception)
                {
                    // Pas de blocage si la table des consommables échoue
                }
            }
            catch (Exception ex)
            {
                result.Status = $"Erreur diagnostic : {ex.Message}";
            }

            return result;
        }

        private string MapSnmpStatus(int statusCode)
        {
            return statusCode switch
            {
                1 => "Autre / Alerte",
                2 => "Inconnu",
                3 => "Prêt",
                4 => "Impression en cours",
                5 => "Préchauffage",
                _ => "Inconnu"
            };
        }

        private string GetOidIndex(ObjectIdentifier oid)
        {
            var parts = oid.ToString().Split('.');
            if (parts.Length > 0)
            {
                return parts[parts.Length - 1];
            }
            return string.Empty;
        }

        private string MapColorName(string desc)
        {
            if (desc.Contains("black") || desc.Contains("noir") || desc.Contains("k ")) return "Noir";
            if (desc.Contains("cyan") || desc.Contains("c ")) return "Cyan";
            if (desc.Contains("magenta") || desc.Contains("m ")) return "Magenta";
            if (desc.Contains("yellow") || desc.Contains("jaune") || desc.Contains("y ")) return "Jaune";

            return System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(desc);
        }
    }
}
