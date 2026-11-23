using System.Text;
using System.Text.RegularExpressions;

namespace Autoprint.Server.Helpers
{
    /// <summary>
    /// Textes renvoyés par les imprimantes en SNMP (issue #46) : décodage des chaînes accentuées et noms lisibles
    /// pour les bacs papier.
    /// </summary>
    public static class SnmpText
    {
        private static readonly Encoding StrictUtf8 = new UTF8Encoding(false, true);
        private static readonly Encoding Latin1 = Encoding.Latin1;
        private static readonly Encoding Cp850;
        private static readonly Encoding Cp1252;

        static SnmpText()
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            Cp850 = Encoding.GetEncoding(850);
            Cp1252 = Encoding.GetEncoding(1252);
        }

        /// <summary>
        /// Décode une chaîne d'agent SNMP. UTF-8 en priorité ; sinon, les octets 0x80 à 0x9F (caractères de contrôle
        /// en ISO-8859-1) indiquent une page de code DOS (850 : é = 0x82, è = 0x8A, à = 0x85) ou Windows-1252 : on
        /// retient celle qui donne le plus de lettres. Sans ces octets, ISO-8859-1.
        /// </summary>
        public static string Decode(byte[] bytes)
        {
            if (bytes == null || bytes.Length == 0) return string.Empty;

            try { return StrictUtf8.GetString(bytes); }
            catch (DecoderFallbackException) { }

            if (!bytes.Any(b => b >= 0x80 && b <= 0x9F)) return Latin1.GetString(bytes);

            var dos = Cp850.GetString(bytes);
            var windows = Cp1252.GetString(bytes);
            return dos.Count(char.IsLetter) >= windows.Count(char.IsLetter) ? dos : windows;
        }

        /// <summary>
        /// Répare un libellé déjà enregistré avec l'ancien décodage (ISO-8859-1 seul) : « Unit\u0082 de fusion »
        /// redevient « Unité de fusion ». Un texte sans caractère de contrôle U+0080 à U+009F est rendu tel quel.
        /// </summary>
        public static string Repair(string? text)
        {
            if (string.IsNullOrEmpty(text)) return text ?? string.Empty;
            if (!text.Any(c => c >= '\u0080' && c <= '\u009F') || text.Any(c => c > 'ÿ')) return text;
            return Decode(Latin1.GetBytes(text));
        }

        // Noms de média PWG 5101.1 / IPP (« iso-a4-white », « na_letter_8.5x11in », « iso-designated-long-envelope »)
        private static readonly Regex PwgMedia = new(@"^(iso|na|jis|jpn|prc|roc|om|oe|asme|choice)[-_][a-z0-9]", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        private static readonly (string Key, string Label, bool Envelope)[] Sizes =
        {
            ("iso-designated-long", "DL", true), ("iso-dl", "DL", true),
            ("iso-c4", "C4", true), ("iso-c5", "C5", true), ("iso-c6", "C6", true), ("iso-c6c5", "C6/C5", true),
            ("na-number-10", "n° 10", true), ("na-number-9", "n° 9", true), ("na-monarch", "Monarch", true),
            ("iso-a3", "A3", false), ("iso-a4", "A4", false), ("iso-a5", "A5", false), ("iso-a6", "A6", false),
            ("iso-b4", "B4", false), ("iso-b5", "B5", false), ("jis-b4", "B4 (JIS)", false), ("jis-b5", "B5 (JIS)", false),
            ("na-letter", "Letter", false), ("na-legal", "Legal", false), ("na-executive", "Executive", false),
            ("na-ledger", "Ledger", false), ("na-tabloid", "Tabloïd", false), ("na-index-4x6", "10 × 15", false),
            ("om-folio", "Folio", false), ("na-foolscap", "Foolscap", false), ("na-govt-letter", "Government Letter", false)
        };

        private static readonly Dictionary<string, string> MediaWords = new(StringComparer.OrdinalIgnoreCase)
        {
            ["transparent"] = "transparent", ["labels"] = "étiquettes", ["label"] = "étiquettes", ["cardstock"] = "cartonné",
            ["photographic"] = "photo", ["glossy"] = "brillant", ["recycled"] = "recyclé", ["letterhead"] = "en-tête",
            ["yellow"] = "jaune", ["blue"] = "bleu", ["pink"] = "rose", ["green"] = "vert", ["red"] = "rouge"
        };

        public static bool IsPwgMediaName(string? name) => !string.IsNullOrWhiteSpace(name) && PwgMedia.IsMatch(name.Trim());

        /// <summary>« iso-a4-white » → « A4 », « iso-designated-long-envelope » → « Enveloppe DL ». Texte inchangé s'il n'est pas un nom PWG.</summary>
        public static string HumanizeMediaName(string? name)
        {
            if (!IsPwgMediaName(name)) return name?.Trim() ?? string.Empty;

            var norm = name!.Trim().ToLowerInvariant().Replace('_', '-');
            var size = Sizes.Where(s => norm == s.Key || norm.StartsWith(s.Key + "-"))
                            .OrderByDescending(s => s.Key.Length)
                            .Select(s => ((string Label, bool Envelope)?)(s.Label, s.Envelope))
                            .FirstOrDefault();

            if (size == null)
            {
                // Taille inconnue : on garde le code, sans le préfixe de famille ni la couleur blanche par défaut
                var raw = Regex.Replace(norm, @"^(iso|na|jis|jpn|prc|roc|om|oe|asme|choice)-", "");
                raw = Regex.Replace(raw, @"-white$", "");
                return raw.Replace('-', ' ');
            }

            var rest = norm.Split('-');
            bool envelope = size.Value.Envelope || rest.Contains("envelope");
            var extras = rest.Where(MediaWords.ContainsKey).Select(w => MediaWords[w]).Distinct();

            var label = envelope ? $"Enveloppe {size.Value.Label}" : size.Value.Label;
            var extra = string.Join(", ", extras);
            return extra.Length > 0 ? $"{label} ({extra})" : label;
        }

        /// <summary>
        /// Nom affiché d'un bac. Les profils créés avant l'issue #46 ont gardé le nom de média brut (« iso-a4-white ») :
        /// on le remplace par « Bac N – A4 », N étant le dernier indice de l'OID du bac.
        /// </summary>
        public static string FormatTrayName(string? name, string? oid)
        {
            var clean = Repair(name).Trim();
            if (!IsPwgMediaName(clean)) return clean;

            var media = HumanizeMediaName(clean);
            var index = TrayNumber(oid);
            return index != null ? $"Bac {index} – {media}" : media;
        }

        /// <summary>Dernier indice d'un OID ou d'un suffixe d'index (« 1.3 » → « 3 »).</summary>
        public static string? TrayNumber(string? oidOrIndex)
        {
            if (string.IsNullOrWhiteSpace(oidOrIndex)) return null;
            var last = oidOrIndex.Trim().TrimEnd('.').Split('.').LastOrDefault();
            return int.TryParse(last, out _) ? last : null;
        }
    }
}
