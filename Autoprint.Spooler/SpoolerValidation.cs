using System.Net;
using System.Net.Sockets;

namespace Autoprint.Spooler
{
    /// <summary>
    /// Contrôles appliqués par le composant privilégié lui-même, indépendamment du site web :
    /// même un site compromis ne peut transmettre que des noms, adresses et pilotes conformes.
    /// </summary>
    public static class SpoolerValidation
    {
        private static readonly char[] ForbiddenShareChars = "\\/[]:|<>+=;,?*\"".ToCharArray();

        public static string RequirePrinterName(string? name, string field = "Nom d'imprimante")
        {
            if (string.IsNullOrWhiteSpace(name) || name.Length > 220 || name.Any(char.IsControl) || name.IndexOfAny(new[] { '\\', ',' }) >= 0)
                throw new ArgumentException($"{field} invalide.");
            return name;
        }

        public static string? OptionalShareName(string? shareName)
        {
            if (string.IsNullOrEmpty(shareName)) return shareName;
            if (shareName.Length > 80 || shareName.Any(char.IsControl) || shareName.IndexOfAny(ForbiddenShareChars) >= 0)
                throw new ArgumentException("Nom de partage invalide.");
            return shareName;
        }

        // Adresse d'imprimante : IPv4 ou nom DNS (le champ « AdresseIp » accepte aussi un nom d'hôte)
        public static string RequireHostAddress(string? address, string field = "Adresse de l'imprimante")
        {
            var value = address?.Trim();
            if (string.IsNullOrEmpty(value) || value.Length > 50) throw new ArgumentException($"{field} invalide.");
            if (IPAddress.TryParse(value, out var parsed))
            {
                if (parsed.AddressFamily != AddressFamily.InterNetwork) throw new ArgumentException($"{field} invalide (IPv4 attendue).");
                return value;
            }
            if (!System.Text.RegularExpressions.Regex.IsMatch(value, @"^[A-Za-z0-9]([A-Za-z0-9-]*[A-Za-z0-9])?(\.[A-Za-z0-9]([A-Za-z0-9-]*[A-Za-z0-9])?)*$"))
                throw new ArgumentException($"{field} invalide.");
            return value;
        }

        public static string? OptionalText(string? text, string field, int maxLength = 256)
        {
            if (text == null) return null;
            if (text.Length > maxLength || text.Any(c => char.IsControl(c) && c != '\r' && c != '\n'))
                throw new ArgumentException($"{field} invalide.");
            return text;
        }

        // Échappement des littéraux WQL (CWE-943)
        public static string EscapeWql(string value) => (value ?? string.Empty).Replace("\\", "\\\\").Replace("'", "\\'");
    }
}
