using System.Text;
using System.Text.RegularExpressions;

namespace Autoprint.Server.Helpers
{
    // Filtre LDAP saisi par l'administrateur (RFC 4515) : relu entièrement puis reconstruit, chaque élément
    // (attribut, valeur) repassant par SecurityHelper.EscapeLdapFilter. Un filtre mal formé est refusé (null)
    // au lieu d'être transmis tel quel à l'annuaire (CodeQL « LDAP query built from user-controlled sources »).
    public static class LdapFilterNormalizer
    {
        private const int MaxDepth = 32;
        private const int MaxLength = 4096;

        // attribut[;options] ou OID, puis :dn et règle de correspondance facultatifs (filtres étendus)
        private static readonly Regex AttributeRegex = new(
            @"^(?:[A-Za-z][A-Za-z0-9-]*(?:;[A-Za-z0-9-]+)*|[0-9]+(?:\.[0-9]+)+)?(?::dn)?(?::(?:[A-Za-z][A-Za-z0-9-]*|[0-9]+(?:\.[0-9]+)+))?$",
            RegexOptions.CultureInvariant);

        public static string? Normalize(string? filter)
        {
            if (string.IsNullOrWhiteSpace(filter) || filter.Length > MaxLength) return null;
            string s = filter.Trim();
            int pos = 0;
            var sb = new StringBuilder(s.Length);
            if (!ParseFilter(s, ref pos, sb, 0) || pos != s.Length) return null;
            return sb.ToString();
        }

        private static bool ParseFilter(string s, ref int pos, StringBuilder sb, int depth)
        {
            if (depth > MaxDepth || pos >= s.Length || s[pos] != '(') return false;
            pos++;
            if (pos >= s.Length) return false;

            char c = s[pos];
            if (c == '&' || c == '|' || c == '!')
            {
                sb.Append(c == '&' ? "(&" : c == '|' ? "(|" : "(!");
                pos++;
                int count = 0;
                while (pos < s.Length && s[pos] == '(')
                {
                    if (!ParseFilter(s, ref pos, sb, depth + 1)) return false;
                    count++;
                }
                if (count == 0 || (c == '!' && count != 1)) return false;
            }
            else
            {
                sb.Append('(');
                if (!ParseItem(s, ref pos, sb)) return false;
            }

            if (pos >= s.Length || s[pos] != ')') return false;
            pos++;
            sb.Append(')');
            return true;
        }

        private static bool ParseItem(string s, ref int pos, StringBuilder sb)
        {
            int end = s.IndexOf(')', pos);
            if (end < 0) return false;
            string item = s[pos..end];
            pos = end;

            int eq = item.IndexOf('=');
            if (eq <= 0) return false;

            string op;
            int attrEnd;
            switch (item[eq - 1])
            {
                case '~': op = "~="; attrEnd = eq - 1; break;
                case '>': op = ">="; attrEnd = eq - 1; break;
                case '<': op = "<="; attrEnd = eq - 1; break;
                case ':': op = ":="; attrEnd = eq - 1; break;
                default: op = "="; attrEnd = eq; break;
            }

            string attribute = item[..attrEnd];
            if (!AttributeRegex.IsMatch(attribute)) return false;
            if (attribute.Length == 0 || (op != ":=" && attribute.Contains(':'))) return false;

            sb.Append(SecurityHelper.EscapeLdapFilter(attribute));
            sb.Append(op switch { "~=" => "~=", ">=" => ">=", "<=" => "<=", ":=" => ":=", _ => "=" });

            string value = item[(eq + 1)..];
            if (op == "=" && value == "*") { sb.Append('*'); return true; }

            string[] pieces = value.Split('*');
            if (pieces.Length > 1 && op != "=") return false;
            for (int i = 0; i < pieces.Length; i++)
            {
                if (i > 0 && i < pieces.Length - 1 && pieces[i].Length == 0) return false; // « ** »
                if (i > 0) sb.Append('*');
                byte[]? bytes = DecodeValue(pieces[i]);
                if (bytes == null) return false;
                sb.Append(SecurityHelper.EscapeLdapFilter(bytes));
            }
            return true;
        }

        // Valeur sans « * » : caractères (UTF-8) et séquences \hh ; « ( », « ) », NUL et « \ » isolé refusés
        private static byte[]? DecodeValue(string piece)
        {
            var bytes = new List<byte>(piece.Length);
            var run = new StringBuilder();
            for (int i = 0; i < piece.Length; i++)
            {
                char ch = piece[i];
                if (ch == '\\')
                {
                    if (i + 2 >= piece.Length) return null;
                    if (!IsHex(piece[i + 1]) || !IsHex(piece[i + 2])) return null;
                    bytes.AddRange(Encoding.UTF8.GetBytes(run.ToString()));
                    run.Clear();
                    bytes.Add(Convert.ToByte(piece.Substring(i + 1, 2), 16));
                    i += 2;
                }
                else if (ch == '(' || ch == ')' || ch == '\0') return null;
                else run.Append(ch);
            }
            bytes.AddRange(Encoding.UTF8.GetBytes(run.ToString()));
            return bytes.ToArray();
        }

        private static bool IsHex(char c) => c is >= '0' and <= '9' or >= 'a' and <= 'f' or >= 'A' and <= 'F';
    }
}
