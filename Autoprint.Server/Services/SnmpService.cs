using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading.Tasks;
using Autoprint.Server.Helpers;
using Autoprint.Shared.DTOs;
using Autoprint.Shared;
using Lextm.SharpSnmpLib;
using Lextm.SharpSnmpLib.Messaging;

namespace Autoprint.Server.Services
{
    public partial class SnmpService : ISnmpService
    {
        public async Task<PrinterDiagnosticResult> GetPrinterDiagnosticAsync(string ipAddress, int port, SnmpCredentials credentials, SnmpProfile? profile = null, int lowThresholdPercent = MonitoringSettings.DefaultLowThresholdPercent)
        {
            var result = new PrinterDiagnosticResult { LowThresholdPercent = lowThresholdPercent };

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

            // v1 / v2c / v3 selon le profil d'identifiants résolu (imprimante → modèle → anciens champs)
            var client = new SnmpClient(endpoint, credentials);

            // 3. Traitement SNMP
            try
            {
                // Sans profil d'OID sur le modèle : détection automatique par la Printer MIB (#50, remplace les « valeurs
                // standard » limitées au premier toner de B33) ; HasSnmpProfile reste faux pour que la console le signale
                result.HasSnmpProfile = profile != null;
                if (profile == null)
                {
                    if (await CollectFromPrinterMibAsync(client, StructureCacheKey(ipAddress, port, credentials), result, lowThresholdPercent))
                    {
                        await AddStandardAlertsAsync(client, result);
                        return result;
                    }

                    result.Status = pingOk ? "En Ligne (SNMP muet)" : "Éteint / Hors Ligne";
                    return result;
                }

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
                        snmpResponses = await client.GetAsync(requestedVariables, 1500);
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
                                    AddSupply(result, item, rawVal, responseDict, InferSupplyKind(item.Name), lowThresholdPercent);
                                    break;

                                case SnmpItemCategory.Tray:
                                    {
                                        int maxCap = -1;
                                        if (item.ValueType == SnmpValueType.RawWithMax && !string.IsNullOrWhiteSpace(item.OidMaxCapacity)
                                            && responseDict.TryGetValue(item.OidMaxCapacity.Trim(), out var maxData))
                                        {
                                            maxCap = ExtractInt(maxData);
                                        }
                                        var (trayLevel, trayState) = InterpretTrayLevel(rawVal, maxCap);
                                        result.Trays.Add(new PaperTrayResult
                                        {
                                            Name = SnmpText.FormatTrayName(item.Name, item.Oid),
                                            CurrentLevel = trayLevel,
                                            MaxCapacity = 100,
                                            State = trayState
                                        });
                                    }
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

                                case SnmpItemCategory.Maintenance when item.ValueType != SnmpValueType.String:
                                    // #49 : un élément de maintenance qui a un niveau (photoconducteur, kit…) a une jauge, pas une alerte
                                    AddSupply(result, item, rawVal, responseDict, SupplyKind.Maintenance, lowThresholdPercent);
                                    break;

                                case SnmpItemCategory.Console:
                                case SnmpItemCategory.Maintenance:
                                    if (!string.IsNullOrWhiteSpace(strVal))
                                    {
                                        AddAlert(result, AlertSeverity.Info, $"{SnmpText.Repair(item.Name)} : {strVal}");
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

                        await AddStandardAlertsAsync(client, result);
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
                        legacyResponses = await client.GetAsync(requestedVariables, 1500);
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
                                var (level, state) = InterpretSupplyLevel(ExtractInt(data), -1, SnmpValueType.Percentage, lowThresholdPercent);
                                result.Toners.Add(new TonerLevelResult
                                {
                                    Color = color,
                                    CurrentLevel = level,
                                    MaxCapacity = 100,
                                    ColorHex = GetDefaultColorHex(color),
                                    State = state
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
                        await AddStandardAlertsAsync(client, result);
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

        public async Task<List<SnmpTestProfileResultDto>> TestProfileItemsAsync(SnmpTestProfileRequestDto request, SnmpCredentials credentials)
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
            var client = new SnmpClient(endpoint, credentials);

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
                responseVariables = await client.GetAsync(variablesToQuery, 1200);
            }
            catch (Lextm.SharpSnmpLib.Messaging.TimeoutException)
            {
                throw new InvalidOperationException($"L'imprimante à l'adresse {request.IpAddress} n'a pas répondu sur le port SNMP {request.Port} ({credentials.Describe()}). Vérifiez que la machine est allumée et que SNMP est activé avec ces identifiants.");
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
                        formatted = "Il en reste (niveau non communiqué)";
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
                    if (rawVal == -3) formatted = "Il en reste (niveau non communiqué)";
                    else if (rawVal == -1 || rawVal == -2) formatted = "Niveau inconnu";
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

        public async Task<List<DiscoveredOidDto>> ScanPrinterOidsAsync(SnmpScanRequestDto request, SnmpCredentials credentials)
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
            var client = new SnmpClient(endpoint, credentials);

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
                var probeRes = await client.GetAsync(probeVars, 800);
                if (probeRes != null && probeRes.Any(v => !IsNullOrNoSuch(v.Data)))
                {
                    isAlive = true;
                }
            }
            catch { }

            if (!isAlive)
            {
                throw new InvalidOperationException($"L'imprimante ({request.IpAddress}) ne répond pas sur le port SNMP {request.Port} ({credentials.Describe()}). Vérifiez que l'imprimante est allumée et accessible.");
            }

            // 3. AUTO-DÉCOUVERTE : Consommables & Cartouches (Printer-MIB 1.3.6.1.2.1.43.11.1.1)
            try
            {
                var supplyDescsRaw = new List<Variable>();
                var supplyMaxesRaw = new List<Variable>();
                var supplyLevelsRaw = new List<Variable>();

                await Task.Run(() =>
                {
                    try { client.Walk(new ObjectIdentifier("1.3.6.1.2.1.43.11.1.1.6"), supplyDescsRaw, 800); } catch { }
                    try { client.Walk(new ObjectIdentifier("1.3.6.1.2.1.43.11.1.1.8"), supplyMaxesRaw, 800); } catch { }
                    try { client.Walk(new ObjectIdentifier("1.3.6.1.2.1.43.11.1.1.9"), supplyLevelsRaw, 800); } catch { }
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
                    if (levelVal == -3) levelFormatted = "Il en reste (niveau non communiqué)";
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
                        var colorName = MapColorName(lowerDesc);
                        if (colorName != null)
                        {
                            finalName = $"Toner {colorName}";
                        }
                        else if (!string.IsNullOrWhiteSpace(desc))
                        {
                            // Consommable sans couleur (unité de fusion, kit de maintenance…) : libellé de l'agent, sans préfixe « Toner »
                            finalName = char.ToUpper(desc[0]) + desc.Substring(1);
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
                var trayNamesRaw = new List<Variable>();
                var trayLevelsRaw = new List<Variable>();
                var trayMaxesRaw = new List<Variable>();

                await Task.Run(() =>
                {
                    try { client.Walk(new ObjectIdentifier("1.3.6.1.2.1.43.8.2.1.12"), trayDescsRaw, 800); } catch { } // prtInputMediaName
                    try { client.Walk(new ObjectIdentifier("1.3.6.1.2.1.43.8.2.1.13"), trayNamesRaw, 800); } catch { } // prtInputName
                    try { client.Walk(new ObjectIdentifier("1.3.6.1.2.1.43.8.2.1.10"), trayLevelsRaw, 800); } catch { }
                    try { client.Walk(new ObjectIdentifier("1.3.6.1.2.1.43.8.2.1.9"), trayMaxesRaw, 800); } catch { }
                });

                const string trayDescPrefix = "1.3.6.1.2.1.43.8.2.1.12.";
                const string trayNamePrefix = "1.3.6.1.2.1.43.8.2.1.13.";
                const string trayLevelPrefix = "1.3.6.1.2.1.43.8.2.1.10.";
                const string trayMaxPrefix = "1.3.6.1.2.1.43.8.2.1.9.";

                // Filtrage strict : ne garder que les OIDs appartenant strictement à la table des bacs papier
                var trayDescs = trayDescsRaw.Where(v => v.Id.ToString().StartsWith(trayDescPrefix)).ToList();
                var trayNames = trayNamesRaw.Where(v => v.Id.ToString().StartsWith(trayNamePrefix)).ToList();
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

                    var nameVar = trayNames.FirstOrDefault(v => v.Id.ToString() == trayNamePrefix + idx);

                    // Nom lisible : nom du bac donné par l'agent (« Tray 1 ») et format du média (« iso-a4-white » → « A4 »)
                    string inputName = nameVar?.Data is OctetString nameStr ? DecodeOctetString(nameStr) : "";
                    string media = descVar?.Data is OctetString descStr ? SnmpText.HumanizeMediaName(DecodeOctetString(descStr)) : "";
                    string baseName = !string.IsNullOrWhiteSpace(inputName) ? inputName : $"Bac {SnmpText.TrayNumber(idx) ?? idx}";
                    string trayName = !string.IsNullOrWhiteSpace(media) && !media.Equals(baseName, StringComparison.OrdinalIgnoreCase)
                        ? $"{baseName} – {media}"
                        : baseName;

                    int levelVal = levelVar != null ? ExtractInt(levelVar.Data) : -1;
                    int maxVal = maxVar != null ? ExtractInt(maxVar.Data) : -1;

                    string levelStr = "Inconnu";
                    if (levelVal == -3) levelStr = "Papier présent (niveau non communiqué)";
                    else if (levelVal == -1 || levelVal == -2) levelStr = "Inconnu";
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

                var stdRes = await client.GetAsync(standardVars, 1000);
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

        private static bool IsNullOrNoSuch(ISnmpData data)
        {
            if (data == null) return true;
            if (data is NoSuchInstance || data is NoSuchObject || data is EndOfMibView) return true;
            return false;
        }

        private static int ExtractInt(ISnmpData data)
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

        private static string ExtractString(ISnmpData data)
        {
            if (data == null || IsNullOrNoSuch(data)) return string.Empty;
            if (data is OctetString octet) return DecodeOctetString(octet);
            if (data is Integer32 int32) return int32.ToInt32().ToString();
            if (data is Counter32 c32) return c32.ToUInt32().ToString();
            if (data is TimeTicks ticks) return ticks.ToUInt32().ToString();
            return data.ToString();
        }

        private static string GetOidIndex(ObjectIdentifier oid)
        {
            string str = oid.ToString();
            int lastDot = str.LastIndexOf('.');
            if (lastDot >= 0 && lastDot < str.Length - 1)
            {
                return str.Substring(lastDot + 1);
            }
            return "";
        }

        #region Niveaux, états et alertes (#49)

        /// <summary>Ajoute un consommable (toner ou maintenance) : niveau en % quand il est connu, état dans tous les cas.</summary>
        private void AddSupply(PrinterDiagnosticResult result, SnmpProfileItem item, int rawVal, IDictionary<string, ISnmpData> responseDict, SupplyKind kind, int lowThresholdPercent)
        {
            int maxCap = -1;
            if (item.ValueType == SnmpValueType.RawWithMax && !string.IsNullOrWhiteSpace(item.OidMaxCapacity)
                && responseDict.TryGetValue(item.OidMaxCapacity.Trim(), out var maxData))
            {
                maxCap = ExtractInt(maxData);
            }

            var (level, state) = InterpretSupplyLevel(rawVal, maxCap, item.ValueType, lowThresholdPercent);
            var name = SnmpText.Repair(item.Name);
            result.Toners.Add(new TonerLevelResult
            {
                Color = name,
                CurrentLevel = level,
                MaxCapacity = 100,
                ColorHex = !string.IsNullOrWhiteSpace(item.ColorHex) ? item.ColorHex : GetDefaultColorHex(name),
                State = state,
                Kind = kind
            });
        }

        /// <summary>
        /// Niveau d'un consommable (prtMarkerSuppliesLevel, RFC 3805) : -3 = « il en reste » (niveau non communiqué),
        /// -2 = inconnu, -1 = autre. Aucun pourcentage n'est inventé : le niveau vaut alors -1 et seul l'état est connu.
        /// </summary>
        internal static (int Level, SupplyState State) InterpretSupplyLevel(int raw, int maxCapacity, SnmpValueType valueType, int lowThresholdPercent)
        {
            if (raw == -3) return (-1, SupplyState.Ok);
            if (raw < 0) return (-1, SupplyState.Unknown);

            int level;
            if (valueType == SnmpValueType.RawWithMax && maxCapacity > 0)
            {
                level = (int)Math.Round((double)raw / maxCapacity * 100);
            }
            else if (raw <= 100)
            {
                level = raw;
            }
            else
            {
                // Valeur brute sans capacité connue (pages, grammes…) : pas de pourcentage possible
                return (-1, SupplyState.Ok);
            }

            level = Math.Clamp(level, 0, 100);
            var state = level == 0 ? SupplyState.Empty : level <= lowThresholdPercent ? SupplyState.Low : SupplyState.Ok;
            return (level, state);
        }

        /// <summary>Niveau d'un bac (prtInputCurrentLevel) : mêmes valeurs spéciales ; sans capacité, une valeur > 100 est un nombre de feuilles.</summary>
        internal static (int Level, SupplyState State) InterpretTrayLevel(int raw, int maxCapacity)
        {
            if (raw == -3) return (-1, SupplyState.Ok);
            if (raw < 0) return (-1, SupplyState.Unknown);
            if (raw == 0) return (0, SupplyState.Empty);
            if (maxCapacity > 0) return (Math.Clamp((int)Math.Round((double)raw / maxCapacity * 100), 0, 100), SupplyState.Ok);
            return (raw, SupplyState.Ok);
        }

        private static readonly string[] MaintenanceWords =
        {
            "fuser", "fusion", "four", "drum", "tambour", "photocond", "photoconductor", "imaging", "unité d'image", "image unit",
            "kit", "maintenance", "waste", "usagé", "used", "récup", "recup", "collect", "transfer", "transfert", "belt", "courroie", "roller", "rouleau", "developer", "développeur"
        };

        /// <summary>Un élément « Toner » dont le nom désigne une pièce d'usure (unité de fusion, kit, photoconducteur…) va dans la section Maintenance.</summary>
        internal static SupplyKind InferSupplyKind(string? name)
        {
            if (string.IsNullOrWhiteSpace(name)) return SupplyKind.Toner;
            var n = name.ToLowerInvariant();
            if (MaintenanceWords.Any(n.Contains)) return SupplyKind.Maintenance;
            if (System.Text.RegularExpressions.Regex.IsMatch(n, @"(^|[^a-z])o?pc([^a-z]|$)")) return SupplyKind.Maintenance;
            return SupplyKind.Toner;
        }

        // Code constructeur en fin de message (« Mode économie d'énergie {10033} ») et ponctuation finale ignorés pour la comparaison
        private static string NormalizeAlert(string text)
        {
            var t = System.Text.RegularExpressions.Regex.Replace(text, @"\s*\{\d+\}\s*$", "");
            return t.Trim().TrimEnd('.', '!', ' ').ToLowerInvariant();
        }

        /// <summary>
        /// Ajoute une alerte sans doublon : un même message lu sur l'écran (« Écran LCD Console : … ») et dans la table
        /// d'alertes n'apparaît qu'une fois, avec la gravité la plus haute.
        /// </summary>
        private static void AddAlert(PrinterDiagnosticResult result, AlertSeverity severity, string text)
        {
            text = text.Trim();
            if (text.Length == 0) return;
            var key = NormalizeAlert(text);
            var existing = result.Alerts.FirstOrDefault(a =>
            {
                var other = NormalizeAlert(a.Text);
                var sep = a.Text.IndexOf(" : ", StringComparison.Ordinal);
                var otherMessage = sep >= 0 ? NormalizeAlert(a.Text[(sep + 3)..]) : other;
                return other == key || otherMessage == key;
            });
            if (existing == null) result.Alerts.Add(new PrinterAlert(severity, text));
            else if (severity > existing.Severity) existing.Severity = severity;
        }

        private const string HrPrinterDetectedErrorStateOid = "1.3.6.1.2.1.25.3.5.1.2.1";
        private const string PrtAlertSeverityPrefix = "1.3.6.1.2.1.43.18.1.1.2.";
        private const string PrtAlertDescriptionPrefix = "1.3.6.1.2.1.43.18.1.1.8.";

        /// <summary>
        /// État d'erreur (hrPrinterDetectedErrorState) et alertes actives (prtAlertTable), interrogés à part : en SNMPv1,
        /// un OID absent dans la requête principale ferait échouer toute la requête.
        /// </summary>
        private async Task AddStandardAlertsAsync(SnmpClient client, PrinterDiagnosticResult result)
        {
            try
            {
                var res = await client.GetAsync(new List<Variable> { new(new ObjectIdentifier(HrPrinterDetectedErrorStateOid)) }, 1000);
                var data = res?.FirstOrDefault()?.Data;
                if (data is OctetString octet) ApplyDetectedErrorState(result, octet.GetRaw());
            }
            catch { }

            try
            {
                var severities = new List<Variable>();
                var descriptions = new List<Variable>();
                await Task.Run(() =>
                {
                    try { client.Walk(new ObjectIdentifier(PrtAlertSeverityPrefix.TrimEnd('.')), severities, 1000); } catch { }
                    try { client.Walk(new ObjectIdentifier(PrtAlertDescriptionPrefix.TrimEnd('.')), descriptions, 1000); } catch { }
                });

                foreach (var desc in descriptions.Where(v => v.Id.ToString().StartsWith(PrtAlertDescriptionPrefix)).Take(20))
                {
                    var text = ExtractString(desc.Data);
                    if (string.IsNullOrWhiteSpace(text)) continue;

                    var index = desc.Id.ToString().Substring(PrtAlertDescriptionPrefix.Length);
                    var sevVar = severities.FirstOrDefault(v => v.Id.ToString() == PrtAlertSeverityPrefix + index);
                    // prtAlertSeverityLevel : 1 = autre, 3 = critique, 4 = avertissement, 5 = avertissement ponctuel
                    var severity = (sevVar != null ? ExtractInt(sevVar.Data) : 1) switch
                    {
                        3 => AlertSeverity.Critical,
                        4 or 5 => AlertSeverity.Warning,
                        _ => AlertSeverity.Info
                    };
                    AddAlert(result, severity, text);
                }
            }
            catch { }
        }

        /// <summary>
        /// hrPrinterDetectedErrorState (RFC 2790/3805), bits du premier octet de poids fort à faible : lowPaper, noPaper,
        /// lowToner, noToner, doorOpen, jammed, offline, serviceRequested ; second octet : inputTrayMissing,
        /// outputTrayMissing, markerSupplyMissing, outputNearFull, outputFull, inputTrayEmpty, overduePreventMaint.
        /// </summary>
        internal static void ApplyDetectedErrorState(PrinterDiagnosticResult result, byte[]? bytes)
        {
            if (bytes == null || bytes.Length == 0) return;
            byte b0 = bytes[0];
            byte b1 = bytes.Length > 1 ? bytes[1] : (byte)0;

            bool lowToner = (b0 & 0x20) != 0;
            bool noToner = (b0 & 0x10) != 0;

            if (lowToner || noToner)
            {
                var toners = result.Toners.Where(t => t.Kind == SupplyKind.Toner).ToList();
                if (toners.Count == 1)
                {
                    // Monochrome : l'indicateur concerne forcément le seul toner
                    var t = toners[0];
                    if (noToner) t.State = SupplyState.Empty;
                    else if (t.State is SupplyState.Ok or SupplyState.Unknown) t.State = SupplyState.Low;
                }
                else if (!toners.Any(t => noToner ? t.State == SupplyState.Empty : t.NeedsAttention))
                {
                    // Couleur : on ne sait pas quelle cartouche est concernée
                    AddAlert(result, noToner ? AlertSeverity.Critical : AlertSeverity.Warning,
                        noToner ? "Toner vide signalé par l'imprimante" : "Toner bas signalé par l'imprimante");
                }
            }

            if ((b0 & 0x80) != 0) AddAlert(result, AlertSeverity.Info, "Papier bas");
            if ((b0 & 0x40) != 0) AddAlert(result, AlertSeverity.Warning, "Plus de papier");
            if ((b0 & 0x08) != 0) AddAlert(result, AlertSeverity.Critical, "Capot ouvert");
            if ((b0 & 0x04) != 0) AddAlert(result, AlertSeverity.Critical, "Bourrage papier");
            if ((b0 & 0x02) != 0) AddAlert(result, AlertSeverity.Info, "Imprimante hors ligne"); // levé par certaines imprimantes en veille
            if ((b0 & 0x01) != 0) AddAlert(result, AlertSeverity.Warning, "Intervention demandée");
            if ((b1 & 0x80) != 0) AddAlert(result, AlertSeverity.Info, "Bac d'alimentation absent"); // bac optionnel non installé
            if ((b1 & 0x40) != 0) AddAlert(result, AlertSeverity.Warning, "Bac de sortie absent");
            if ((b1 & 0x20) != 0) AddAlert(result, AlertSeverity.Critical, "Consommable absent");
            if ((b1 & 0x10) != 0) AddAlert(result, AlertSeverity.Info, "Bac de sortie presque plein");
            if ((b1 & 0x08) != 0) AddAlert(result, AlertSeverity.Warning, "Bac de sortie plein");
            if ((b1 & 0x04) != 0) AddAlert(result, AlertSeverity.Info, "Bac d'alimentation vide"); // souvent le bac manuel, vide en temps normal
            if ((b1 & 0x02) != 0) AddAlert(result, AlertSeverity.Warning, "Maintenance préventive dépassée");
        }

        #endregion

        private static string MapSnmpStatus(int statusCode)
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

        /// <summary>Couleur d'un toner d'après la description de l'agent ; null si ce n'est pas un toner de couleur.</summary>
        private static string? MapColorName(string desc)
        {
            if (string.IsNullOrWhiteSpace(desc)) return null;
            // Lettre isolée (« Toner K », « C Cartridge ») : un mot entier, pour ne pas prendre « kit » ou « unité de fusion »
            bool Letter(char l) => System.Text.RegularExpressions.Regex.IsMatch(desc, $@"(^|[^a-z0-9]){l}([^a-z0-9]|$)");
            if (desc.Contains("black") || desc.Contains("noir") || Letter('k')) return "Noir";
            if (desc.Contains("cyan") || Letter('c')) return "Cyan";
            if (desc.Contains("magenta") || Letter('m')) return "Magenta";
            if (desc.Contains("yellow") || desc.Contains("jaune") || Letter('y')) return "Jaune";
            return null;
        }

        private static string GetDefaultColorHex(string colorName)
        {
            var name = colorName.ToLowerInvariant();
            if (name.Contains("noir") || name.Contains("black")) return "#212529";
            if (name.Contains("cyan")) return "#17a2b8";
            if (name.Contains("magenta")) return "#e83e8c";
            if (name.Contains("jaune") || name.Contains("yellow")) return "#ffc107";
            return "#6c757d";
        }

        private static string DecodeOctetString(OctetString octetString)
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

            // UTF-8, puis page de code DOS / Windows / ISO-8859-1 selon les octets (issue #46 : « Unité de fusion »)
            return SnmpText.Decode(cleanBytes).Trim();
        }

        #endregion
    }
}
