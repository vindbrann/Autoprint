using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
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

            if (string.IsNullOrWhiteSpace(ipAddress))
            {
                result.Status = "Adresse IP manquante";
                return result;
            }

            // 1. Test Ping rapide (800ms) - non bloquant si filtré
            bool pingOk = false;
            long pingRtt = 0;
            try
            {
                using var ping = new Ping();
                var reply = await ping.SendPingAsync(ipAddress, 800);
                pingOk = reply.Status == IPStatus.Success;
                pingRtt = reply.RoundtripTime;
            }
            catch
            {
                pingOk = false;
            }

            result.PingSuccess = pingOk;
            result.PingRoundtripTimeMs = pingRtt;

            // 2. Résolution IP / Endpoint
            IPEndPoint endpoint;
            try
            {
                if (!IPAddress.TryParse(ipAddress, out var ip))
                {
                    var addresses = await Dns.GetHostAddressesAsync(ipAddress);
                    if (addresses.Length > 0) ip = addresses[0];
                    else throw new Exception("Impossible de résoudre le nom d'hôte");
                }
                endpoint = new IPEndPoint(ip, port > 0 ? port : 161);
            }
            catch (Exception ex)
            {
                if (!pingOk)
                {
                    result.Status = "Éteint / Hors Ligne";
                }
                else
                {
                    result.Status = $"Erreur résolution IP : {ex.Message}";
                }
                return result;
            }

            var communityBytes = new OctetString(string.IsNullOrWhiteSpace(community) ? "public" : community);
            var versionCode = version == 1 ? VersionCode.V1 : VersionCode.V2;

            // 3. Traitement SNMP
            try
            {
                if (profile == null)
                {
                    result.HasSnmpProfile = false;
                    result.Status = pingOk ? "En Ligne (Aucun profil SNMP configuré)" : "Inconnu (Aucun profil SNMP)";
                    return result;
                }

                result.HasSnmpProfile = true;

                // Préparation du lot de variables à interroger en une seule requête SNMP
                var requestedVariables = new List<Variable>();
                var addedOids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                void AddOidToQuery(string? oidStr)
                {
                    if (!string.IsNullOrWhiteSpace(oidStr) && !addedOids.Contains(oidStr.Trim()))
                    {
                        try
                        {
                            requestedVariables.Add(new Variable(new ObjectIdentifier(oidStr.Trim())));
                            addedOids.Add(oidStr.Trim());
                        }
                        catch { }
                    }
                }

                // Cas A : Profil dynamique (SnmpProfileItems)
                if (profile.Items != null && profile.Items.Any())
                {
                    foreach (var item in profile.Items)
                    {
                        AddOidToQuery(item.Oid);
                        if (item.ValueType == SnmpValueType.RawWithMax && !string.IsNullOrWhiteSpace(item.OidMaxCapacity))
                        {
                            AddOidToQuery(item.OidMaxCapacity);
                        }
                    }

                    // Ajouter l'Uptime et le Numéro de Série si pas déjà dans le profil
                    AddOidToQuery("1.3.6.1.2.1.1.3.0");
                    AddOidToQuery("1.3.6.1.2.1.43.5.1.1.17.1");

                    // Envoi groupé en 1 seul paquet UDP
                    IList<Variable>? snmpResponses = null;
                    try
                    {
                        snmpResponses = await Task.Run(() => Messenger.Get(versionCode, endpoint, communityBytes, requestedVariables, 1500));
                    }
                    catch { }

                    if (snmpResponses != null && snmpResponses.Any())
                    {
                        result.PingSuccess = true; // Si SNMP répond, la machine est forcément en ligne même si Ping était filtré
                        var responseDict = snmpResponses.ToDictionary(v => v.Id.ToString(), v => v.Data, StringComparer.OrdinalIgnoreCase);

                        foreach (var item in profile.Items.OrderBy(i => i.SortOrder))
                        {
                            if (!responseDict.TryGetValue(item.Oid, out var data) || IsNullOrNoSuch(data)) continue;

                            int rawVal = ExtractInt(data);
                            string strVal = ExtractString(data);

                            switch (item.Category)
                            {
                                case SnmpItemCategory.Toner:
                                    int computedLevel = rawVal;
                                    if (item.ValueType == SnmpValueType.RawWithMax && !string.IsNullOrEmpty(item.OidMaxCapacity))
                                    {
                                        int maxCap = -1;
                                        if (responseDict.TryGetValue(item.OidMaxCapacity, out var maxData))
                                        {
                                            maxCap = ExtractInt(maxData);
                                        }

                                        if (rawVal == -3)
                                        {
                                            computedLevel = 100; // RFC 3805: -3 = Some remaining (OK)
                                        }
                                        else if (rawVal == -2)
                                        {
                                            computedLevel = -1; // RFC 3805: -2 = Unknown
                                        }
                                        else if (maxCap > 0 && rawVal >= 0)
                                        {
                                            computedLevel = (int)Math.Round((double)rawVal / maxCap * 100);
                                            if (computedLevel > 100) computedLevel = 100;
                                        }
                                        else if (maxCap <= 0 && rawVal >= 0)
                                        {
                                            computedLevel = rawVal <= 100 ? rawVal : 100;
                                        }
                                    }
                                    else if (item.ValueType == SnmpValueType.Percentage)
                                    {
                                        computedLevel = rawVal >= 0 ? (rawVal <= 100 ? rawVal : 100) : (rawVal == -3 ? 100 : -1);
                                    }

                                    result.Toners.Add(new TonerLevelResult
                                    {
                                        Color = item.Name,
                                        CurrentLevel = computedLevel,
                                        MaxCapacity = 100,
                                        ColorHex = !string.IsNullOrWhiteSpace(item.ColorHex) ? item.ColorHex : GetDefaultColorHex(item.Name)
                                    });
                                    break;

                                case SnmpItemCategory.Tray:
                                    int trayComputed = 0;
                                    if (item.ValueType == SnmpValueType.RawWithMax && !string.IsNullOrWhiteSpace(item.OidMaxCapacity))
                                    {
                                        int maxCap = -1;
                                        if (responseDict.TryGetValue(item.OidMaxCapacity.Trim(), out var maxData))
                                        {
                                            maxCap = ExtractInt(maxData);
                                        }

                                        if (maxCap > 0 && rawVal >= 0)
                                        {
                                            trayComputed = (int)Math.Round((double)rawVal / maxCap * 100);
                                            if (trayComputed > 100) trayComputed = 100;
                                        }
                                        else
                                        {
                                            trayComputed = rawVal >= 0 ? (rawVal <= 100 ? rawVal : 100) : (rawVal == -3 || rawVal == -1 ? 100 : 0);
                                        }
                                    }
                                    else
                                    {
                                        trayComputed = rawVal >= 0 ? (rawVal <= 100 ? rawVal : 100) : (rawVal == -3 || rawVal == -1 ? 100 : 0);
                                    }

                                    result.Trays.Add(new PaperTrayResult
                                    {
                                        Name = item.Name,
                                        CurrentLevel = trayComputed,
                                        MaxCapacity = 100
                                    });
                                    break;

                                case SnmpItemCategory.PageCounter:
                                    if (long.TryParse(strVal.Replace(" ", "").Replace(",", "").Replace(".", ""), out long pc))
                                    {
                                        result.PageCounter = pc;
                                    }
                                    break;

                                case SnmpItemCategory.Status:
                                    if (int.TryParse(strVal, out int stCode))
                                    {
                                        result.Status = MapSnmpStatus(stCode);
                                    }
                                    else if (!string.IsNullOrWhiteSpace(strVal))
                                    {
                                        result.Status = strVal;
                                    }
                                    break;

                                case SnmpItemCategory.SerialNumber:
                                    if (!string.IsNullOrWhiteSpace(strVal))
                                    {
                                        result.SerialNumber = strVal.Trim();
                                    }
                                    break;

                                case SnmpItemCategory.Console:
                                case SnmpItemCategory.Maintenance:
                                    if (!string.IsNullOrWhiteSpace(strVal))
                                    {
                                        result.Alerts.Add($"{item.Name} : {strVal}");
                                    }
                                    break;
                            }
                        }

                        // Récupération Numéro de Série standard (si pas déjà trouvé dans les items personnalisés)
                        if (string.IsNullOrEmpty(result.SerialNumber) && responseDict.TryGetValue("1.3.6.1.2.1.43.5.1.1.17.1", out var snData) && !IsNullOrNoSuch(snData))
                        {
                            string sn = ExtractString(snData).Trim();
                            if (!string.IsNullOrWhiteSpace(sn) && sn != "0" && sn != "N/A")
                            {
                                result.SerialNumber = sn;
                            }
                        }

                        // Récupération Uptime
                        if (responseDict.TryGetValue("1.3.6.1.2.1.1.3.0", out var upData) && upData is TimeTicks ticks)
                        {
                            result.UptimeSeconds = (long)(ticks.ToUInt32() / 100);
                        }

                        if (string.IsNullOrEmpty(result.Status) || result.Status == "Inconnu")
                        {
                            result.Status = "En Ligne";
                        }

                        return result;
                    }
                }
                else
                {
                    // Cas B : Profil legacy
                    AddOidToQuery(profile.OidTonerBlack);
                    AddOidToQuery(profile.OidTonerCyan);
                    AddOidToQuery(profile.OidTonerMagenta);
                    AddOidToQuery(profile.OidTonerYellow);
                    AddOidToQuery(profile.OidPageCounter);
                    AddOidToQuery("1.3.6.1.2.1.1.3.0");
                    AddOidToQuery("1.3.6.1.2.1.43.5.1.1.17.1");

                    IList<Variable>? legacyResponses = null;
                    try
                    {
                        legacyResponses = await Task.Run(() => Messenger.Get(versionCode, endpoint, communityBytes, requestedVariables, 1500));
                    }
                    catch { }

                    if (legacyResponses != null && legacyResponses.Any())
                    {
                        result.PingSuccess = true;
                        var dict = legacyResponses.ToDictionary(v => v.Id.ToString(), v => v.Data, StringComparer.OrdinalIgnoreCase);

                        void AddLegacyToner(string color, string? oid)
                        {
                            if (!string.IsNullOrEmpty(oid) && dict.TryGetValue(oid, out var data) && !IsNullOrNoSuch(data))
                            {
                                int val = ExtractInt(data);
                                result.Toners.Add(new TonerLevelResult
                                {
                                    Color = color,
                                    CurrentLevel = val >= 0 ? val : (val == -3 ? 100 : -1),
                                    MaxCapacity = 100,
                                    ColorHex = GetDefaultColorHex(color)
                                });
                            }
                        }

                        AddLegacyToner("Noir", profile.OidTonerBlack);
                        AddLegacyToner("Cyan", profile.OidTonerCyan);
                        AddLegacyToner("Magenta", profile.OidTonerMagenta);
                        AddLegacyToner("Jaune", profile.OidTonerYellow);

                        if (!string.IsNullOrEmpty(profile.OidPageCounter) && dict.TryGetValue(profile.OidPageCounter, out var pcData))
                        {
                            result.PageCounter = ExtractInt(pcData);
                        }

                        if (dict.TryGetValue("1.3.6.1.2.1.43.5.1.1.17.1", out var snData) && !IsNullOrNoSuch(snData))
                        {
                            string sn = ExtractString(snData).Trim();
                            if (!string.IsNullOrWhiteSpace(sn) && sn != "0" && sn != "N/A")
                            {
                                result.SerialNumber = sn;
                            }
                        }

                        result.Status = "En Ligne";
                        return result;
                    }
                }

                if (!pingOk)
                {
                    result.Status = "Éteint / Hors Ligne";
                }
                else
                {
                    result.Status = "En Ligne (SNMP muet)";
                }
            }
            catch (Exception ex)
            {
                result.Status = $"Erreur diagnostic : {ex.Message}";
            }

            return result;
        }

        public async Task<List<SnmpTestProfileResultDto>> TestProfileItemsAsync(SnmpTestProfileRequestDto request)
        {
            var results = new List<SnmpTestProfileResultDto>();
            if (string.IsNullOrWhiteSpace(request.IpAddress) || request.Items == null || !request.Items.Any())
            {
                return results;
            }

            // 1. Résolution de l'adresse IP
            if (!IPAddress.TryParse(request.IpAddress.Trim(), out var ip))
            {
                try
                {
                    var addresses = await Dns.GetHostAddressesAsync(request.IpAddress.Trim());
                    if (addresses.Length > 0) ip = addresses[0];
                    else throw new InvalidOperationException($"Impossible de résoudre le nom d'hôte : {request.IpAddress}");
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException($"Échec de résolution de l'adresse IP {request.IpAddress} : {ex.Message}");
                }
            }

            var endpoint = new IPEndPoint(ip, request.Port > 0 ? request.Port : 161);
            var communityBytes = new OctetString(string.IsNullOrWhiteSpace(request.Community) ? "public" : request.Community.Trim());
            var versionCode = request.Version == 1 ? VersionCode.V1 : VersionCode.V2;

            // 2. Construction de la liste groupée de variables (sans doublons)
            var variablesToQuery = new List<Variable>();
            var addedOids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var item in request.Items)
            {
                if (!string.IsNullOrWhiteSpace(item.Oid) && !addedOids.Contains(item.Oid.Trim()))
                {
                    try
                    {
                        variablesToQuery.Add(new Variable(new ObjectIdentifier(item.Oid.Trim())));
                        addedOids.Add(item.Oid.Trim());
                    }
                    catch { }
                }

                if (item.ValueType == SnmpValueType.RawWithMax && !string.IsNullOrWhiteSpace(item.OidMaxCapacity) && !addedOids.Contains(item.OidMaxCapacity.Trim()))
                {
                    try
                    {
                        variablesToQuery.Add(new Variable(new ObjectIdentifier(item.OidMaxCapacity.Trim())));
                        addedOids.Add(item.OidMaxCapacity.Trim());
                    }
                    catch { }
                }
            }

            if (!variablesToQuery.Any())
            {
                return results;
            }

            // 3. Exécution d'un seul paquet SNMP GET multi-variables (Timeout strict de 1 200 ms)
            IList<Variable>? responseVariables = null;
            try
            {
                responseVariables = await Task.Run(() => Messenger.Get(versionCode, endpoint, communityBytes, variablesToQuery, 1200));
            }
            catch (Lextm.SharpSnmpLib.Messaging.TimeoutException)
            {
                throw new InvalidOperationException($"L'imprimante à l'adresse {request.IpAddress} n'a pas répondu sur le port SNMP {request.Port} (Communauté '{request.Community}'). Vérifiez que la machine est allumée et que le protocole SNMP v1/v2c est activé.");
            }
            catch (SocketException ex)
            {
                throw new InvalidOperationException($"Erreur réseau de connexion à l'imprimante ({request.IpAddress}:{request.Port}) : {ex.Message}");
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Erreur lors de la requête SNMP : {ex.Message}");
            }

            if (responseVariables == null || !responseVariables.Any())
            {
                throw new InvalidOperationException($"Aucune réponse reçue de l'imprimante ({request.IpAddress}).");
            }

            var responseDict = responseVariables.ToDictionary(v => v.Id.ToString(), v => v.Data, StringComparer.OrdinalIgnoreCase);

            // 4. Traitement et formatage des résultats pour chaque champ du profil
            foreach (var item in request.Items)
            {
                if (!responseDict.TryGetValue(item.Oid.Trim(), out var data) || IsNullOrNoSuch(data))
                {
                    results.Add(new SnmpTestProfileResultDto
                    {
                        Oid = item.Oid,
                        Value = "Non supporté sur cette machine",
                        Success = false
                    });
                    continue;
                }

                int rawVal = ExtractInt(data);
                string strVal = ExtractString(data);
                string formatted = strVal;

                if (item.ValueType == SnmpValueType.RawWithMax && !string.IsNullOrWhiteSpace(item.OidMaxCapacity))
                {
                    int maxCap = -1;
                    if (responseDict.TryGetValue(item.OidMaxCapacity.Trim(), out var maxData))
                    {
                        maxCap = ExtractInt(maxData);
                    }

                    if (rawVal == -3)
                    {
                        formatted = "Niveau OK";
                    }
                    else if (rawVal == -2)
                    {
                        formatted = "Niveau Inconnu";
                    }
                    else if (maxCap > 0 && rawVal >= 0)
                    {
                        int pct = (int)Math.Round((double)rawVal / maxCap * 100);
                        if (pct > 100) pct = 100;
                        formatted = item.Category == SnmpItemCategory.Tray ? $"{pct} % ({rawVal}/{maxCap} f.)" : $"{pct} % ({rawVal}/{maxCap})";
                    }
                    else if (maxCap <= 0 && rawVal >= 0)
                    {
                        formatted = rawVal <= 100 ? $"{rawVal} %" : (item.Category == SnmpItemCategory.Tray ? $"{rawVal} feuilles" : $"{rawVal}");
                    }
                    else
                    {
                        formatted = rawVal.ToString();
                    }
                }
                else if (item.ValueType == SnmpValueType.Percentage)
                {
                    if (rawVal == -3) formatted = "Niveau OK";
                    else if (rawVal == -1) formatted = "Plein (100 %)";
                    else if (rawVal == 0) formatted = "Vide (0 %)";
                    else if (rawVal >= 0)
                    {
                        if (rawVal <= 100)
                        {
                            formatted = $"{rawVal} %";
                        }
                        else
                        {
                            formatted = item.Category == SnmpItemCategory.Tray ? $"{rawVal} feuilles" : $"{rawVal}";
                        }
                    }
                    else formatted = rawVal.ToString();
                }
                else if (data is TimeTicks ticks)
                {
                    var ts = TimeSpan.FromMilliseconds(ticks.ToUInt32() * 10);
                    formatted = $"{ts.Days}j {ts.Hours}h {ts.Minutes}m {ts.Seconds}s";
                }
                else if (item.Category == SnmpItemCategory.PageCounter)
                {
                    if (long.TryParse(strVal, out long cnt))
                    {
                        formatted = cnt.ToString("N0") + " pages";
                    }
                }
                else if (item.Category == SnmpItemCategory.Status)
                {
                    if (int.TryParse(strVal, out int stCode))
                    {
                        formatted = $"{stCode} ({MapSnmpStatus(stCode)})";
                    }
                }

                results.Add(new SnmpTestProfileResultDto
                {
                    Oid = item.Oid,
                    Value = string.IsNullOrWhiteSpace(formatted) ? "Valeur vide" : formatted,
                    Success = true
                });
            }

            return results;
        }

        public async Task<List<DiscoveredOidDto>> ScanPrinterOidsAsync(SnmpScanRequestDto request)
        {
            var discovered = new List<DiscoveredOidDto>();
            if (string.IsNullOrWhiteSpace(request.IpAddress))
            {
                throw new InvalidOperationException("L'adresse IP de l'imprimante est obligatoire.");
            }

            // 1. Résolution de l'adresse IP
            if (!IPAddress.TryParse(request.IpAddress.Trim(), out var ip))
            {
                try
                {
                    var addresses = await Dns.GetHostAddressesAsync(request.IpAddress.Trim());
                    if (addresses.Length > 0) ip = addresses[0];
                    else throw new InvalidOperationException($"Impossible de résoudre le nom d'hôte : {request.IpAddress}");
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException($"Échec de résolution DNS pour {request.IpAddress} : {ex.Message}");
                }
            }

            var endpoint = new IPEndPoint(ip, request.Port > 0 ? request.Port : 161);
            var communityBytes = new OctetString(string.IsNullOrWhiteSpace(request.Community) ? "public" : request.Community.Trim());
            var versionCode = request.Version == 1 ? VersionCode.V1 : VersionCode.V2;

            // 2. Probe initial rapide SNMP (800ms) pour vérifier que le service SNMP est actif
            bool isAlive = false;
            try
            {
                var probeVars = new List<Variable>
                {
                    new Variable(new ObjectIdentifier("1.3.6.1.2.1.1.3.0")),     // sysUpTime
                    new Variable(new ObjectIdentifier("1.3.6.1.2.1.1.1.0")),     // sysDescr
                    new Variable(new ObjectIdentifier("1.3.6.1.2.1.25.3.5.1.1.1")) // hrPrinterStatus
                };
                var probeRes = await Task.Run(() => Messenger.Get(versionCode, endpoint, communityBytes, probeVars, 800));
                if (probeRes != null && probeRes.Any(v => !IsNullOrNoSuch(v.Data)))
                {
                    isAlive = true;
                }
            }
            catch { }

            if (!isAlive)
            {
                throw new InvalidOperationException($"L'imprimante ({request.IpAddress}) ne répond pas sur le port SNMP {request.Port} avec la communauté '{request.Community}'. Vérifiez que l'imprimante est allumée et accessible.");
            }

            // 3. AUTO-DÉCOUVERTE : Consommables & Cartouches (Printer-MIB 1.3.6.1.2.1.43.11.1.1)
            try
            {
                var supplyDescsRaw = new List<Variable>();
                var supplyMaxesRaw = new List<Variable>();
                var supplyLevelsRaw = new List<Variable>();

                await Task.Run(() =>
                {
                    try { Messenger.Walk(versionCode, endpoint, communityBytes, new ObjectIdentifier("1.3.6.1.2.1.43.11.1.1.6"), supplyDescsRaw, 800, WalkMode.WithinSubtree); } catch { }
                    try { Messenger.Walk(versionCode, endpoint, communityBytes, new ObjectIdentifier("1.3.6.1.2.1.43.11.1.1.8"), supplyMaxesRaw, 800, WalkMode.WithinSubtree); } catch { }
                    try { Messenger.Walk(versionCode, endpoint, communityBytes, new ObjectIdentifier("1.3.6.1.2.1.43.11.1.1.9"), supplyLevelsRaw, 800, WalkMode.WithinSubtree); } catch { }
                });

                const string supplyDescPrefix = "1.3.6.1.2.1.43.11.1.1.6.";
                const string supplyMaxPrefix = "1.3.6.1.2.1.43.11.1.1.8.";
                const string supplyLevelPrefix = "1.3.6.1.2.1.43.11.1.1.9.";

                // Filtrage strict : ne garder que les OIDs appartenant strictement à la table des consommables
                var supplyDescs = supplyDescsRaw.Where(v => v.Id.ToString().StartsWith(supplyDescPrefix)).ToList();
                var supplyMaxes = supplyMaxesRaw.Where(v => v.Id.ToString().StartsWith(supplyMaxPrefix)).ToList();
                var supplyLevels = supplyLevelsRaw.Where(v => v.Id.ToString().StartsWith(supplyLevelPrefix)).ToList();

                var supplyIndices = supplyDescs.Select(v => v.Id.ToString().Substring(supplyDescPrefix.Length))
                    .Union(supplyLevels.Select(v => v.Id.ToString().Substring(supplyLevelPrefix.Length)))
                    .Distinct().ToList();

                foreach (var idx in supplyIndices)
                {
                    var levelVar = supplyLevels.FirstOrDefault(v => v.Id.ToString() == supplyLevelPrefix + idx);
                    var maxVar = supplyMaxes.FirstOrDefault(v => v.Id.ToString() == supplyMaxPrefix + idx);
                    var descVar = supplyDescs.FirstOrDefault(v => v.Id.ToString() == supplyDescPrefix + idx);

                    string desc = "";
                    if (descVar != null && descVar.Data is OctetString descStr)
                    {
                        var decoded = DecodeOctetString(descStr);
                        if (!string.IsNullOrWhiteSpace(decoded)) desc = decoded;
                    }

                    int levelVal = levelVar != null ? ExtractInt(levelVar.Data) : -1;
                    int maxVal = maxVar != null ? ExtractInt(maxVar.Data) : -1;

                    string levelFormatted = "Inconnu";
                    if (levelVal == -3) levelFormatted = "Niveau OK (≥ 50%)";
                    else if (levelVal >= 0 && maxVal > 0)
                    {
                        int pct = (int)Math.Round((double)levelVal / maxVal * 100);
                        if (pct > 100) pct = 100;
                        levelFormatted = $"{pct} % ({levelVal}/{maxVal})";
                    }
                    else if (levelVal >= 0)
                    {
                        levelFormatted = $"{levelVal} %";
                    }

                    string levelOid = supplyLevelPrefix + idx;
                    string maxOid = supplyMaxPrefix + idx;

                    string lowerDesc = desc.ToLower();
                    bool isWasteOrMaintenance = lowerDesc.Contains("waste") || lowerDesc.Contains("récupérateur") 
                                             || lowerDesc.Contains("collect") || lowerDesc.Contains("drum") 
                                             || lowerDesc.Contains("tambour") || lowerDesc.Contains("fuser") 
                                             || lowerDesc.Contains("four") || lowerDesc.Contains("photoconductor");

                    string finalName;
                    SnmpItemCategory cat;

                    if (isWasteOrMaintenance)
                    {
                        finalName = string.IsNullOrWhiteSpace(desc) ? $"Maintenance {idx}" : desc;
                        cat = SnmpItemCategory.Maintenance;
                    }
                    else
                    {
                        cat = SnmpItemCategory.Toner;
                        if (!string.IsNullOrWhiteSpace(desc))
                        {
                            finalName = $"Toner {MapColorName(lowerDesc)}";
                        }
                        else
                        {
                            // Par défaut si description non fournie par l'agent
                            finalName = idx.EndsWith(".1") ? "Toner Noir" :
                                        idx.EndsWith(".2") ? "Toner Cyan" :
                                        idx.EndsWith(".3") ? "Toner Magenta" :
                                        idx.EndsWith(".4") ? "Toner Jaune" : $"Consommable {idx}";
                        }
                    }

                    discovered.Add(new DiscoveredOidDto
                    {
                        Oid = levelOid,
                        OidMaxCapacity = maxOid,
                        Value = levelFormatted,
                        ValueType = "RawWithMax",
                        SuggestedName = finalName,
                        SuggestedCategory = cat,
                        IsRelevant = true
                    });
                }
            }
            catch { }

            // 4. AUTO-DÉCOUVERTE : Bacs Papier (Printer-MIB 1.3.6.1.2.1.43.8.2.1)
            try
            {
                var trayDescsRaw = new List<Variable>();
                var trayLevelsRaw = new List<Variable>();
                var trayMaxesRaw = new List<Variable>();

                await Task.Run(() =>
                {
                    try { Messenger.Walk(versionCode, endpoint, communityBytes, new ObjectIdentifier("1.3.6.1.2.1.43.8.2.1.12"), trayDescsRaw, 800, WalkMode.WithinSubtree); } catch { }
                    try { Messenger.Walk(versionCode, endpoint, communityBytes, new ObjectIdentifier("1.3.6.1.2.1.43.8.2.1.10"), trayLevelsRaw, 800, WalkMode.WithinSubtree); } catch { }
                    try { Messenger.Walk(versionCode, endpoint, communityBytes, new ObjectIdentifier("1.3.6.1.2.1.43.8.2.1.9"), trayMaxesRaw, 800, WalkMode.WithinSubtree); } catch { }
                });

                const string trayDescPrefix = "1.3.6.1.2.1.43.8.2.1.12.";
                const string trayLevelPrefix = "1.3.6.1.2.1.43.8.2.1.10.";
                const string trayMaxPrefix = "1.3.6.1.2.1.43.8.2.1.9.";

                // Filtrage strict : ne garder que les OIDs appartenant strictement à la table des bacs papier
                var trayDescs = trayDescsRaw.Where(v => v.Id.ToString().StartsWith(trayDescPrefix)).ToList();
                var trayLevels = trayLevelsRaw.Where(v => v.Id.ToString().StartsWith(trayLevelPrefix)).ToList();
                var trayMaxes = trayMaxesRaw.Where(v => v.Id.ToString().StartsWith(trayMaxPrefix)).ToList();

                var trayIndices = trayDescs.Select(v => v.Id.ToString().Substring(trayDescPrefix.Length))
                    .Union(trayLevels.Select(v => v.Id.ToString().Substring(trayLevelPrefix.Length)))
                    .Distinct().ToList();

                foreach (var idx in trayIndices)
                {
                    var levelVar = trayLevels.FirstOrDefault(v => v.Id.ToString() == trayLevelPrefix + idx);
                    var descVar = trayDescs.FirstOrDefault(v => v.Id.ToString() == trayDescPrefix + idx);
                    var maxVar = trayMaxes.FirstOrDefault(v => v.Id.ToString() == trayMaxPrefix + idx);

                    string trayName = "Bac Papier " + idx.Replace("1.", "");
                    if (descVar != null && descVar.Data is OctetString descStr)
                    {
                        var decodedName = DecodeOctetString(descStr);
                        if (!string.IsNullOrWhiteSpace(decodedName)) trayName = decodedName;
                    }

                    int levelVal = levelVar != null ? ExtractInt(levelVar.Data) : -1;
                    int maxVal = maxVar != null ? ExtractInt(maxVar.Data) : -1;

                    string levelStr = "Inconnu";
                    if (levelVal == -3) levelStr = "Niveau OK";
                    else if (levelVal == -1) levelStr = "Plein (100 %)";
                    else if (levelVal == 0) levelStr = "Vide (0 %)";
                    else if (levelVal >= 0 && maxVal > 0)
                    {
                        int pct = (int)Math.Round((double)levelVal / maxVal * 100);
                        if (pct > 100) pct = 100;
                        levelStr = $"{pct} % ({levelVal}/{maxVal} f.)";
                    }
                    else if (levelVal >= 0)
                    {
                        levelStr = levelVal <= 100 ? $"{levelVal} %" : $"{levelVal} feuilles";
                    }

                    discovered.Add(new DiscoveredOidDto
                    {
                        Oid = trayLevelPrefix + idx,
                        OidMaxCapacity = maxVar != null ? trayMaxPrefix + idx : null,
                        Value = levelStr,
                        ValueType = maxVar != null ? "RawWithMax" : "Percentage",
                        SuggestedName = trayName,
                        SuggestedCategory = SnmpItemCategory.Tray,
                        IsRelevant = true
                    });
                }
            }
            catch { }

            // 5. AUTO-DÉCOUVERTE : Compteurs, Statut, Uptime, Console en 1 seul paquet GET
            try
            {
                var standardVars = new List<Variable>
                {
                    new Variable(new ObjectIdentifier("1.3.6.1.2.1.43.10.2.1.4.1.1")), // Compteur Total Pages
                    new Variable(new ObjectIdentifier("1.3.6.1.2.1.25.3.5.1.1.1")),    // Statut physique
                    new Variable(new ObjectIdentifier("1.3.6.1.2.1.43.16.5.1.2.1.1")), // Écran Console LCD
                    new Variable(new ObjectIdentifier("1.3.6.1.2.1.43.5.1.1.17.1")),   // Numéro de Série
                    new Variable(new ObjectIdentifier("1.3.6.1.2.1.1.3.0"))             // SysUpTime
                };

                var stdRes = await Task.Run(() => Messenger.Get(versionCode, endpoint, communityBytes, standardVars, 1000));
                if (stdRes != null)
                {
                    var dict = stdRes.ToDictionary(v => v.Id.ToString(), v => v.Data, StringComparer.OrdinalIgnoreCase);

                    // Compteur
                    if (dict.TryGetValue("1.3.6.1.2.1.43.10.2.1.4.1.1", out var pcData) && !IsNullOrNoSuch(pcData))
                    {
                        long pc = ExtractInt(pcData);
                        discovered.Add(new DiscoveredOidDto
                        {
                            Oid = "1.3.6.1.2.1.43.10.2.1.4.1.1",
                            Value = pc.ToString("N0") + " pages",
                            ValueType = "Counter",
                            SuggestedName = "Compteur Total Pages",
                            SuggestedCategory = SnmpItemCategory.PageCounter,
                            IsRelevant = true
                        });
                    }

                    // Statut
                    if (dict.TryGetValue("1.3.6.1.2.1.25.3.5.1.1.1", out var stData) && !IsNullOrNoSuch(stData))
                    {
                        int st = ExtractInt(stData);
                        discovered.Add(new DiscoveredOidDto
                        {
                            Oid = "1.3.6.1.2.1.25.3.5.1.1.1",
                            Value = $"{st} ({MapSnmpStatus(st)})",
                            ValueType = "String",
                            SuggestedName = "Statut Imprimante",
                            SuggestedCategory = SnmpItemCategory.Status,
                            IsRelevant = true
                        });
                    }

                    // Console
                    if (dict.TryGetValue("1.3.6.1.2.1.43.16.5.1.2.1.1", out var consData) && !IsNullOrNoSuch(consData))
                    {
                        string txt = ExtractString(consData);
                        if (!string.IsNullOrWhiteSpace(txt))
                        {
                            discovered.Add(new DiscoveredOidDto
                            {
                                Oid = "1.3.6.1.2.1.43.16.5.1.2.1.1",
                                Value = txt,
                                ValueType = "String",
                                SuggestedName = "Écran LCD Console",
                                SuggestedCategory = SnmpItemCategory.Console,
                                IsRelevant = true
                            });
                        }
                    }

                    // Numéro de Série
                    if (dict.TryGetValue("1.3.6.1.2.1.43.5.1.1.17.1", out var snData) && !IsNullOrNoSuch(snData))
                    {
                        string sn = ExtractString(snData).Trim();
                        if (!string.IsNullOrWhiteSpace(sn) && sn != "0" && sn != "N/A")
                        {
                            discovered.Add(new DiscoveredOidDto
                            {
                                Oid = "1.3.6.1.2.1.43.5.1.1.17.1",
                                Value = sn,
                                ValueType = "String",
                                SuggestedName = "Numéro de Série",
                                SuggestedCategory = SnmpItemCategory.SerialNumber,
                                IsRelevant = true
                            });
                        }
                    }

                    // Uptime
                    if (dict.TryGetValue("1.3.6.1.2.1.1.3.0", out var upData) && !IsNullOrNoSuch(upData) && upData is TimeTicks ticks)
                    {
                        var ts = TimeSpan.FromMilliseconds(ticks.ToUInt32() * 10);
                        discovered.Add(new DiscoveredOidDto
                        {
                            Oid = "1.3.6.1.2.1.1.3.0",
                            Value = $"{ts.Days}j {ts.Hours}h {ts.Minutes}m {ts.Seconds}s",
                            ValueType = "TimeTicks",
                            SuggestedName = "Uptime Système",
                            SuggestedCategory = SnmpItemCategory.Status,
                            IsRelevant = true
                        });
                    }
                }
            }
            catch { }

            if (!discovered.Any())
            {
                throw new InvalidOperationException($"L'imprimante ({request.IpAddress}) a répondu au diagnostic initial, mais aucun OID standard n'a pu être découvert.");
            }

            return discovered;
        }

        #region Helper Methods

        private bool IsNullOrNoSuch(ISnmpData data)
        {
            if (data == null) return true;
            if (data is NoSuchInstance || data is NoSuchObject || data is EndOfMibView) return true;
            return false;
        }

        private int ExtractInt(ISnmpData data)
        {
            if (data is Integer32 int32) return int32.ToInt32();
            if (data is Counter32 c32) return (int)c32.ToUInt32();
            if (data is Gauge32 g32) return (int)g32.ToUInt32();
            if (data is Counter64 c64) return (int)c64.ToUInt64();
            if (data is OctetString octet)
            {
                if (int.TryParse(DecodeOctetString(octet), out int parsed)) return parsed;
            }
            return -1;
        }

        private string ExtractString(ISnmpData data)
        {
            if (data == null || IsNullOrNoSuch(data)) return string.Empty;
            if (data is OctetString octet) return DecodeOctetString(octet);
            if (data is Integer32 int32) return int32.ToInt32().ToString();
            if (data is Counter32 c32) return c32.ToUInt32().ToString();
            if (data is TimeTicks ticks) return ticks.ToUInt32().ToString();
            return data.ToString();
        }

        private string GetOidIndex(ObjectIdentifier oid)
        {
            string str = oid.ToString();
            int lastDot = str.LastIndexOf('.');
            if (lastDot >= 0 && lastDot < str.Length - 1)
            {
                return str.Substring(lastDot + 1);
            }
            return "";
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

        private string MapColorName(string desc)
        {
            if (desc.Contains("black") || desc.Contains("noir") || desc.Contains("k ")) return "Noir";
            if (desc.Contains("cyan") || desc.Contains("c ")) return "Cyan";
            if (desc.Contains("magenta") || desc.Contains("m ")) return "Magenta";
            if (desc.Contains("yellow") || desc.Contains("jaune") || desc.Contains("y ")) return "Jaune";

            return System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(desc);
        }

        private string GetDefaultColorHex(string colorName)
        {
            return colorName.ToLower() switch
            {
                "noir" => "#212529",
                "cyan" => "#17a2b8",
                "magenta" => "#e83e8c",
                "jaune" => "#ffc107",
                _ => "#6c757d"
            };
        }

        private string DecodeOctetString(OctetString octetString)
        {
            if (octetString == null) return string.Empty;
            var bytes = octetString.GetRaw();
            if (bytes == null || bytes.Length == 0) return string.Empty;

            // Découpage au premier null-terminator s'il existe
            int len = bytes.Length;
            int nullIdx = Array.IndexOf(bytes, (byte)0);
            if (nullIdx >= 0)
            {
                len = nullIdx;
            }

            if (len == 0) return string.Empty;

            byte[] cleanBytes = new byte[len];
            Array.Copy(bytes, cleanBytes, len);

            try
            {
                var utf8 = new System.Text.UTF8Encoding(false, true);
                return utf8.GetString(cleanBytes).Trim();
            }
            catch
            {
                try
                {
                    return System.Text.Encoding.GetEncoding("ISO-8859-1").GetString(cleanBytes).Trim();
                }
                catch
                {
                    return octetString.ToString().Trim();
                }
            }
        }

        #endregion
    }
}
