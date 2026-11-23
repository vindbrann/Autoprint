using System.Collections.Concurrent;
using Autoprint.Server.Helpers;
using Autoprint.Shared.DTOs;
using Lextm.SharpSnmpLib;
using Lextm.SharpSnmpLib.Messaging;

namespace Autoprint.Server.Services
{
    /// <summary>
    /// Collecteur automatique (#50) : lit directement la Printer MIB (RFC 3805), qui décrit les consommables, les bacs et
    /// leurs couleurs. La structure de chaque imprimante (liste des éléments et libellés) est mémorisée et relue après un
    /// redémarrage de l'imprimante ou toutes les 24 h ; les autres relevés ne lisent que les niveaux.
    /// </summary>
    public partial class SnmpService
    {
        // Printer MIB / Host Resources MIB
        private const string OidSysUpTime = "1.3.6.1.2.1.1.3.0";
        private const string OidHrPrinterStatus = "1.3.6.1.2.1.25.3.5.1.1.1";
        private const string OidSerialNumber = "1.3.6.1.2.1.43.5.1.1.17.1";
        private const string OidLifeCount = "1.3.6.1.2.1.43.10.2.1.4.1.1";

        private const string SupplyColorant = "1.3.6.1.2.1.43.11.1.1.3";   // prtMarkerSuppliesColorantIndex
        private const string SupplyClass = "1.3.6.1.2.1.43.11.1.1.4";      // 3 = réceptacle, 4 = consommable
        private const string SupplyType = "1.3.6.1.2.1.43.11.1.1.5";       // toner, opc, fuser, wasteToner…
        private const string SupplyDescription = "1.3.6.1.2.1.43.11.1.1.6";
        private const string SupplyUnit = "1.3.6.1.2.1.43.11.1.1.7";
        private const string SupplyMax = "1.3.6.1.2.1.43.11.1.1.8";
        private const string SupplyLevel = "1.3.6.1.2.1.43.11.1.1.9";
        private const string ColorantValue = "1.3.6.1.2.1.43.12.1.1.4";    // « black », « cyan »…
        private const string InputMax = "1.3.6.1.2.1.43.8.2.1.9";
        private const string InputLevel = "1.3.6.1.2.1.43.8.2.1.10";
        private const string InputMedia = "1.3.6.1.2.1.43.8.2.1.12";
        private const string InputName = "1.3.6.1.2.1.43.8.2.1.13";
        private const string ConsoleText = "1.3.6.1.2.1.43.16.5.1.2";

        private const int MibTimeoutMs = 1500;
        private const int OidsPerRequest = 16;
        private static readonly TimeSpan StructureMaxAge = TimeSpan.FromHours(24);

        // prtMarkerSuppliesType qui sont des consommables d'impression (le reste est de la maintenance)
        private static readonly HashSet<int> ConsumableTypes = new() { 3, 5, 6, 7, 12, 13, 21 }; // toner, ink, inkCartridge, inkRibbon, solidWax, ribbonWax, tonerCartridge

        internal sealed record MibSupply(string Index, int Class, int Type, string Name, SupplyKind Kind, string ColorHex, int Unit);
        internal sealed record MibInput(string Index, string Name);

        internal sealed class MibStructure
        {
            public List<MibSupply> Supplies { get; init; } = new();
            public List<MibInput> Inputs { get; init; } = new();
            public DateTime DiscoveredUtc { get; init; } = DateTime.UtcNow;
            public uint LastUptimeTicks { get; set; }
        }

        private static readonly ConcurrentDictionary<string, MibStructure> StructureCache = new();

        /// <summary>
        /// Remplit <paramref name="result"/> depuis la Printer MIB. Faux si l'imprimante ne répond pas en SNMP.
        /// </summary>
        private async Task<bool> CollectFromPrinterMibAsync(SnmpClient client, string cacheKey, PrinterDiagnosticResult result, int lowThresholdPercent)
        {
            // 1. Valeurs simples (une seule requête ; en v1, repli OID par OID si l'une manque)
            var scalars = await GetManyAsync(client, new[] { OidSysUpTime, OidHrPrinterStatus, OidSerialNumber, OidLifeCount });
            if (scalars == null || scalars.Count == 0) return false;

            result.PingSuccess = true;
            uint uptimeTicks = 0;
            if (scalars.TryGetValue(OidSysUpTime, out var up) && up is TimeTicks ticks)
            {
                uptimeTicks = ticks.ToUInt32();
                result.UptimeSeconds = uptimeTicks / 100;
            }
            if (scalars.TryGetValue(OidHrPrinterStatus, out var st) && int.TryParse(ExtractString(st), out var stCode))
                result.Status = MapSnmpStatus(stCode);
            if (scalars.TryGetValue(OidSerialNumber, out var sn))
            {
                var serial = ExtractString(sn).Trim();
                if (!string.IsNullOrWhiteSpace(serial) && serial != "0" && serial != "N/A") result.SerialNumber = serial;
            }
            if (scalars.TryGetValue(OidLifeCount, out var lc))
            {
                int pages = ExtractInt(lc);
                if (pages > 0) result.PageCounter = pages;
            }

            // 2. Structure mémorisée, relue après un redémarrage (uptime qui recule) ou au-delà de 24 h
            if (!StructureCache.TryGetValue(cacheKey, out var structure)
                || DateTime.UtcNow - structure.DiscoveredUtc > StructureMaxAge
                || (uptimeTicks > 0 && uptimeTicks < structure.LastUptimeTicks))
            {
                structure = await DiscoverStructureAsync(client);
                // Lecture vide (délai dépassé, agent incomplet) : pas mémorisée, on réessaiera au prochain relevé
                if (structure.Supplies.Count > 0 || structure.Inputs.Count > 0) StructureCache[cacheKey] = structure;
                else StructureCache.TryRemove(cacheKey, out _);
            }
            structure.LastUptimeTicks = uptimeTicks;

            // 3. Niveaux (seules valeurs qui changent d'un relevé à l'autre)
            var levelOids = structure.Supplies.SelectMany(s => new[] { $"{SupplyLevel}.{s.Index}", $"{SupplyMax}.{s.Index}" })
                .Concat(structure.Inputs.SelectMany(i => new[] { $"{InputLevel}.{i.Index}", $"{InputMax}.{i.Index}" }))
                .ToList();
            var levels = levelOids.Count > 0 ? await GetManyAsync(client, levelOids) ?? new() : new();

            int IntOf(string oid) => levels.TryGetValue(oid, out var d) ? ExtractInt(d) : -2;

            bool multipleToners = structure.Supplies.Count(s => s.Kind == SupplyKind.Toner) > 1;
            foreach (var s in structure.Supplies)
            {
                var (level, state) = InterpretSupplyLevel(IntOf($"{SupplyLevel}.{s.Index}"), IntOf($"{SupplyMax}.{s.Index}"), Autoprint.Shared.SnmpValueType.RawWithMax, lowThresholdPercent);
                result.Toners.Add(new TonerLevelResult
                {
                    Key = $"supply:{s.Index}",
                    Color = s.Name,
                    CurrentLevel = level,
                    MaxCapacity = 100,
                    ColorHex = s.ColorHex,
                    State = state,
                    Kind = s.Kind,
                    IsReceptacle = s.Class == 3
                });
            }

            foreach (var i in structure.Inputs)
            {
                var (level, state) = InterpretTrayLevel(IntOf($"{InputLevel}.{i.Index}"), IntOf($"{InputMax}.{i.Index}"));
                result.Trays.Add(new PaperTrayResult { Key = $"input:{i.Index}", Name = i.Name, CurrentLevel = level, MaxCapacity = 100, State = state });
            }

            // 4. Écran de l'imprimante (change souvent, lu à chaque fois)
            try
            {
                var lines = new List<Variable>();
                await Task.Run(() => client.WalkTable(new ObjectIdentifier(ConsoleText), lines, MibTimeoutMs));
                var text = string.Join(" ", lines.Select(v => ExtractString(v.Data).Trim()).Where(t => t.Length > 0));
                if (text.Length > 0) AddAlert(result, AlertSeverity.Info, $"Écran : {text}");
            }
            catch { }

            if (string.IsNullOrEmpty(result.Status) || result.Status == "Inconnu") result.Status = "En Ligne";
            return true;
        }

        /// <summary>Lit la description des consommables, des couleurs et des bacs (une fois, puis mémorisée).</summary>
        private async Task<MibStructure> DiscoverStructureAsync(SnmpClient client)
        {
            async Task<Dictionary<string, ISnmpData>> Column(string root)
            {
                var vars = new List<Variable>();
                try { await Task.Run(() => client.WalkTable(new ObjectIdentifier(root), vars, MibTimeoutMs)); } catch { }
                var prefix = root + ".";
                return vars.Where(v => v.Id.ToString().StartsWith(prefix))
                           .GroupBy(v => v.Id.ToString().Substring(prefix.Length))
                           .ToDictionary(g => g.Key, g => g.First().Data);
            }

            var descriptions = await Column(SupplyDescription);
            var classes = await Column(SupplyClass);
            var types = await Column(SupplyType);
            var units = await Column(SupplyUnit);
            var colorantIdx = await Column(SupplyColorant);
            var colorants = await Column(ColorantValue);
            var inputNames = await Column(InputName);
            var inputMedia = await Column(InputMedia);
            // Bacs sans nom ni média : on se repère sur la colonne des niveaux
            var inputLevels = inputNames.Count == 0 && inputMedia.Count == 0 ? await Column(InputLevel) : new();

            var supplies = new List<MibSupply>();
            var supplyIndexes = descriptions.Keys.Union(types.Keys).Distinct().OrderBy(k => k, IndexComparer.Instance).ToList();
            int tonerCount = supplyIndexes.Count(idx => types.TryGetValue(idx, out var t) && ConsumableTypes.Contains(ExtractInt(t)));

            foreach (var idx in supplyIndexes)
            {
                int type = types.TryGetValue(idx, out var t) ? ExtractInt(t) : 2;
                int cls = classes.TryGetValue(idx, out var c) ? ExtractInt(c) : 4;
                int unit = units.TryGetValue(idx, out var u) ? ExtractInt(u) : 0;
                string desc = descriptions.TryGetValue(idx, out var d) ? SnmpText.Repair(ExtractString(d)).Trim() : "";

                // Couleur : prtMarkerColorantValue de la ligne prtMarkerColorantIndex (même hrDeviceIndex)
                string? colorant = null;
                if (colorantIdx.TryGetValue(idx, out var ci) && ExtractInt(ci) is int cIndex && cIndex > 0)
                {
                    var device = idx.Split('.')[0];
                    if (colorants.TryGetValue($"{device}.{cIndex}", out var cv)) colorant = ExtractString(cv).Trim();
                }
                string? colorFr = MapColorName(string.IsNullOrWhiteSpace(colorant) ? "" : colorant.ToLowerInvariant());

                SupplyKind kind = ConsumableTypes.Contains(type) ? SupplyKind.Toner
                    : type is 1 or 2 ? InferSupplyKind(desc)
                    : SupplyKind.Maintenance;

                string name = desc;
                if (string.IsNullOrWhiteSpace(name))
                    name = kind == SupplyKind.Toner ? $"Toner {colorFr ?? idx}" : SupplyTypeName(type) ?? $"Consommable {idx}";
                else if (kind == SupplyKind.Toner && tonerCount > 1 && colorFr != null && MapColorName(name.ToLowerInvariant()) == null)
                    name = $"{name} ({colorFr.ToLowerInvariant()})"; // « Toner » ×4 sur une couleur : on précise laquelle

                string hex = kind == SupplyKind.Toner
                    ? GetDefaultColorHex(colorFr ?? name)
                    : "#6c757d";

                supplies.Add(new MibSupply(idx, cls, type, name, kind, hex, unit));
            }

            var inputs = new List<MibInput>();
            foreach (var idx in inputNames.Keys.Union(inputMedia.Keys).Union(inputLevels.Keys).Distinct().OrderBy(k => k, IndexComparer.Instance))
            {
                string inputName = inputNames.TryGetValue(idx, out var n) ? SnmpText.Repair(ExtractString(n)).Trim() : "";
                string media = inputMedia.TryGetValue(idx, out var m) ? SnmpText.HumanizeMediaName(SnmpText.Repair(ExtractString(m))) : "";
                string baseName = !string.IsNullOrWhiteSpace(inputName) ? inputName : $"Bac {SnmpText.TrayNumber(idx) ?? idx}";
                string name = !string.IsNullOrWhiteSpace(media) && !media.Equals(baseName, StringComparison.OrdinalIgnoreCase) ? $"{baseName} – {media}" : baseName;
                inputs.Add(new MibInput(idx, name));
            }

            return new MibStructure { Supplies = supplies, Inputs = inputs, DiscoveredUtc = DateTime.UtcNow };
        }

        /// <summary>Libellé d'un type de consommable sans description (prtMarkerSuppliesTypeTC).</summary>
        private static string? SupplyTypeName(int type) => type switch
        {
            4 => "Récupérateur de toner",
            8 => "Récupérateur d'encre",
            9 => "Photoconducteur",
            10 => "Développeur",
            15 => "Unité de fusion",
            18 => "Unité de nettoyage",
            20 => "Unité de transfert",
            32 => "Agrafes",
            _ => null
        };

        /// <summary>
        /// Lit plusieurs OID par paquets de 16. En SNMPv1, un OID absent fait échouer tout le paquet (noSuchName) :
        /// le paquet est alors coupé en deux, jusqu'à l'OID seul, pour récupérer tout ce qui existe. Un délai dépassé
        /// arrête la lecture (l'imprimante ne répond pas). Null si rien n'a répondu.
        /// </summary>
        internal static async Task<Dictionary<string, ISnmpData>?> GetManyAsync(SnmpClient client, IEnumerable<string> oids)
        {
            var values = new Dictionary<string, ISnmpData>(StringComparer.OrdinalIgnoreCase);
            bool anyAnswer = false;
            bool timedOut = false;

            async Task Fetch(List<string> chunk)
            {
                if (timedOut || chunk.Count == 0) return;
                try
                {
                    var res = await client.GetAsync(chunk.Select(o => new Variable(new ObjectIdentifier(o))).ToList(), MibTimeoutMs);
                    anyAnswer = true;
                    foreach (var v in res)
                        if (!IsNullOrNoSuch(v.Data)) values[v.Id.ToString()] = v.Data;
                }
                catch (Lextm.SharpSnmpLib.Messaging.TimeoutException)
                {
                    timedOut = true;
                }
                catch
                {
                    // Erreur renvoyée par l'agent (v1 : noSuchName) : on isole l'OID fautif
                    anyAnswer = true;
                    if (chunk.Count == 1) return;
                    int half = chunk.Count / 2;
                    await Fetch(chunk.Take(half).ToList());
                    await Fetch(chunk.Skip(half).ToList());
                }
            }

            foreach (var chunk in oids.Distinct().Chunk(OidsPerRequest))
                await Fetch(chunk.ToList());

            return anyAnswer ? values : null;
        }

        internal static string StructureCacheKey(string address, int port, SnmpCredentials credentials)
            => $"{address.Trim().ToLowerInvariant()}:{(port > 0 ? port : 161)}:v{credentials.Version}";

        /// <summary>Tri naturel des index d'OID (« 1.2 » avant « 1.10 »).</summary>
        private sealed class IndexComparer : IComparer<string>
        {
            public static readonly IndexComparer Instance = new();
            public int Compare(string? x, string? y)
            {
                var a = (x ?? "").Split('.');
                var b = (y ?? "").Split('.');
                for (int i = 0; i < Math.Min(a.Length, b.Length); i++)
                {
                    int c = int.TryParse(a[i], out var ia) && int.TryParse(b[i], out var ib) ? ia.CompareTo(ib) : string.CompareOrdinal(a[i], b[i]);
                    if (c != 0) return c;
                }
                return a.Length.CompareTo(b.Length);
            }
        }
    }
}
