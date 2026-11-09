using System.Text;
using System.Text.RegularExpressions;

namespace Autoprint.Server.Helpers
{
    // Chemin ADSI « LDAP://domaine[/DN] » construit à partir d'une saisie de la console (test du filtre AD) :
    // domaine limité à un nom DNS, DN de base relu (RFC 4514) puis reconstruit avec chaque valeur échappée.
    // Un DN mal formé est refusé (null) au lieu d'être transmis tel quel (CodeQL « LDAP query built from user-controlled sources », B76).
    public static class LdapPathBuilder
    {
        private const int MaxLength = 1024;

        private static readonly Regex DomainRegex = new(@"^[A-Za-z0-9]([A-Za-z0-9.-]{0,252})$", RegexOptions.CultureInvariant);
        private static readonly Regex AttributeRegex = new(@"^(?:[A-Za-z][A-Za-z0-9-]*|[0-9]+(?:\.[0-9]+)+)$", RegexOptions.CultureInvariant);

        public static string? Build(string? domain, string? baseDn)
        {
            string? host = NormalizeDomain(domain);
            if (host == null) return null;
            if (string.IsNullOrWhiteSpace(baseDn)) return "LDAP://" + host;

            string? dn = NormalizeDn(baseDn);
            return dn == null ? null : "LDAP://" + host + "/" + dn;
        }

        public static string? NormalizeDomain(string? domain)
        {
            if (string.IsNullOrWhiteSpace(domain)) return null;
            string d = domain.Trim();
            if (!DomainRegex.IsMatch(d)) return null;
            var sb = new StringBuilder(d.Length);
            foreach (byte b in Encoding.ASCII.GetBytes(d))
            {
                if ((b >= 'a' && b <= 'z') || (b >= 'A' && b <= 'Z') || (b >= '0' && b <= '9') || b == '.' || b == '-') sb.Append((char)b);
                else return null;
            }
            return sb.ToString();
        }

        // RDN séparés par des virgules non échappées ; chaque RDN « attribut=valeur » (pas de RDN multivalué « + »)
        public static string? NormalizeDn(string? dn)
        {
            if (string.IsNullOrWhiteSpace(dn) || dn.Length > MaxLength) return null;
            var rdns = SplitUnescaped(dn.Trim(), ',');
            if (rdns == null) return null;

            var sb = new StringBuilder(dn.Length + 16);
            for (int i = 0; i < rdns.Count; i++)
            {
                string rdn = rdns[i].Trim();
                int eq = rdn.IndexOf('=');
                if (eq <= 0) return null;
                string attribute = rdn[..eq].Trim();
                if (!AttributeRegex.IsMatch(attribute)) return null;
                byte[]? value = DecodeValue(rdn[(eq + 1)..].Trim());
                if (value == null || value.Length == 0) return null;

                string escaped = EscapeDnValue(value);
                if (escaped.Contains('�')) return null;
                if (i > 0) sb.Append(',');
                sb.Append(EscapeDnValue(Encoding.ASCII.GetBytes(attribute)));
                sb.Append('=');
                sb.Append(escaped);
            }
            return sb.ToString();
        }

        private static List<string>? SplitUnescaped(string s, char separator)
        {
            var parts = new List<string>();
            var current = new StringBuilder();
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if (c == '\\')
                {
                    if (i + 1 >= s.Length) return null;
                    current.Append(c).Append(s[i + 1]);
                    i++;
                }
                else if (c == separator) { parts.Add(current.ToString()); current.Clear(); }
                else current.Append(c);
            }
            parts.Add(current.ToString());
            return parts;
        }

        // Valeur RFC 4514 : caractères (UTF-8), « \ » suivi d'un caractère spécial, ou « \hh »
        private static byte[]? DecodeValue(string v)
        {
            var bytes = new List<byte>(v.Length);
            var run = new StringBuilder();
            for (int i = 0; i < v.Length; i++)
            {
                char c = v[i];
                if (c == '\\')
                {
                    if (i + 1 >= v.Length) return null;
                    char n = v[i + 1];
                    bytes.AddRange(Encoding.UTF8.GetBytes(run.ToString())); run.Clear();
                    if (IsHex(n))
                    {
                        if (i + 2 >= v.Length || !IsHex(v[i + 2])) return null;
                        bytes.Add(Convert.ToByte(v.Substring(i + 1, 2), 16));
                        i += 2;
                    }
                    else if (",+\"\\<>;=# /".IndexOf(n) >= 0) { bytes.Add((byte)n); i++; }
                    else return null;
                }
                else if (c == '+' || c == '"' || c == '<' || c == '>' || c == ';' || c == '\0') return null; // non échappés : refusés
                else run.Append(c);
            }
            bytes.AddRange(Encoding.UTF8.GetBytes(run.ToString()));
            return bytes.ToArray();
        }

        // Spéciaux RFC 4514 et « / » (séparateur ADSI) précédés de « \ » ; contrôles en \hh.
        // Les caractères non ASCII (accents) restent tels quels : ADSI refuse leur forme \hh dans un chemin (« OU=Équipements » testé en labo).
        private static string EscapeDnValue(byte[] value)
        {
            var sb = new StringBuilder(value.Length);
            for (int i = 0; i < value.Length; i++)
            {
                byte b = value[i];
                if (b >= 0x80)
                {
                    int start = i;
                    while (i + 1 < value.Length && value[i + 1] >= 0x80) i++;
                    string text = Encoding.UTF8.GetString(value, start, i - start + 1);
                    if (text.Contains('�')) return "�"; // UTF-8 invalide : refusé par l'appelant
                    sb.Append(text);
                    continue;
                }
                bool edge = (i == 0 && (b == ' ' || b == '#')) || (i == value.Length - 1 && b == ' ');
                if (b < 0x20 || b == 0x7F) sb.Append('\\').Append(b.ToString("X2"));
                else if (edge || b == ',' || b == '+' || b == '"' || b == '\\' || b == '<' || b == '>' || b == ';' || b == '=' || b == '/') sb.Append('\\').Append((char)b);
                else sb.Append((char)b);
            }
            return sb.ToString();
        }

        private static bool IsHex(char c) => c is >= '0' and <= '9' or >= 'a' and <= 'f' or >= 'A' and <= 'F';
    }
}
