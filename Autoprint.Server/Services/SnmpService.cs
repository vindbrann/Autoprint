using System;
using System.Collections.Generic;
using System.Net;
using System.Net.NetworkInformation;
using System.Threading.Tasks;
using Autoprint.Shared.DTOs;
using Autoprint.Shared;
using Lextm.SharpSnmpLib;
using Lextm.SharpSnmpLib.Messaging;

namespace Autoprint.Server.Services
{
    public class SnmpService : ISnmpService
    {
                public async Task<PrinterDiagnosticResult> GetPrinterDiagnosticAsync(string ipAddress, int port, string community, int version, SnmpProfile? profile = null)
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
                result.Status = "�teint / Hors Ligne";
                return result;
            }

            // 2. Requ�te SNMP
            try
            {
                if (!IPAddress.TryParse(ipAddress, out var ip))
                {
                    var addresses = await Dns.GetHostAddressesAsync(ipAddress);
                    if (addresses.Length > 0) ip = addresses[0];
                    else throw new Exception("Impossible de r�soudre le nom d'h�te");
                }

                var endpoint = new IPEndPoint(ip, port);
                var communityBytes = new OctetString(community);
                var versionCode = version == 1 ? VersionCode.V1 : VersionCode.V2;

                // R�cup�ration des infos globales (Status, Page counter, Uptime, Ecran)
                string oidPage = (profile != null && !string.IsNullOrEmpty(profile.OidPageCounter)) ? profile.OidPageCounter : "1.3.6.1.2.1.43.10.2.1.4.1.1";
                var globalOids = new List<Variable>
                {
                    new Variable(new ObjectIdentifier("1.3.6.1.2.1.25.3.5.1.1.1")), // hrPrinterStatus
                    new Variable(new ObjectIdentifier(oidPage)), // prtMarkerLifeCount ou OID custom
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
                            result.UptimeSeconds = uptimeTicks.ToUInt32() / 100;
                        }

                        // Parse console text
                        var line1Var = response[3];
                        var line2Var = response[4];
                        var consoleLines = new List<string>();
                        if (line1Var.Data is OctetString line1Str)
                        {
                            var line1Decoded = DecodeOctetString(line1Str);
                            if (!string.IsNullOrWhiteSpace(line1Decoded)) consoleLines.Add(line1Decoded);
                        }
                        if (line2Var.Data is OctetString line2Str)
                        {
                            var line2Decoded = DecodeOctetString(line2Str);
                            if (!string.IsNullOrWhiteSpace(line2Decoded)) consoleLines.Add(line2Decoded);
                        }

                        if (consoleLines.Count > 0)
                        {
                            string consoleText = string.Join(" | ", consoleLines);
                            result.Alerts.Add($"�cran : {consoleText}");
                        }
                    }
                }
                catch (Exception)
                {
                    result.Status = "En Ligne (SNMP partiel)";
                }

                // R�cup�ration des consommables
                var customTonerOids = new Dictionary<string, string>();
                if (profile != null)
                {
                    if (!string.IsNullOrEmpty(profile.OidTonerBlack)) customTonerOids["Noir"] = profile.OidTonerBlack;
                    if (!string.IsNullOrEmpty(profile.OidTonerCyan)) customTonerOids["Cyan"] = profile.OidTonerCyan;
                    if (!string.IsNullOrEmpty(profile.OidTonerMagenta)) customTonerOids["Magenta"] = profile.OidTonerMagenta;
                    if (!string.IsNullOrEmpty(profile.OidTonerYellow)) customTonerOids["Jaune"] = profile.OidTonerYellow;
                }

                if (customTonerOids.Any())
                {
                    var tonerOidsList = new List<Variable>();
                    var colorsOrdered = new List<string>();
                    foreach (var kvp in customTonerOids)
                    {
                        tonerOidsList.Add(new Variable(new ObjectIdentifier(kvp.Value)));
                        colorsOrdered.Add(kvp.Key);
                    }

                    try
                    {
                        var tonerResponse = Messenger.Get(versionCode, endpoint, communityBytes, tonerOidsList, 2000);
                        if (tonerResponse != null)
                        {
                            for (int i = 0; i < tonerResponse.Count; i++)
                            {
                                var tonerVar = tonerResponse[i];
                                int val = -1;
                                if (tonerVar.Data is Integer32 valInt) val = valInt.ToInt32();
                                
                                result.Toners.Add(new TonerLevelResult
                                {
                                    Color = colorsOrdered[i],
                                    CurrentLevel = val,
                                    MaxCapacity = 100
                                });
                            }
                        }
                    }
                    catch (Exception) { }
                }
                else
                {
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
                                var desc = DecodeOctetString(descStr).ToLower();
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
                                        toner.CurrentLevel = 50; 
                                    }
                                }
                            }
                        }

                        result.Toners.AddRange(tonersMap.Values);
                    }
                    catch (Exception) { }
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
                3 => "Pr\u00eet",
                4 => "Impression en cours",
                5 => "Pr\u00e9chauffage",
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

        private string DecodeOctetString(OctetString octetString)
        {
            if (octetString == null) return string.Empty;
            var bytes = octetString.GetRaw();
            if (bytes == null || bytes.Length == 0) return string.Empty;

            try
            {
                // Strict UTF-8 decoding to throw on invalid sequences (e.g. ISO-8859-1)
                var strictUtf8 = new System.Text.UTF8Encoding(false, true);
                return strictUtf8.GetString(bytes).Trim();
            }
            catch
            {
                try
                {
                    // Fallback to ISO-8859-1 (Latin-1)
                    return System.Text.Encoding.GetEncoding("ISO-8859-1").GetString(bytes).Trim();
                }
                catch
                {
                    return octetString.ToString().Trim();
                }
            }
        }
    }
}
