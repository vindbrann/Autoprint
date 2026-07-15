using System;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;

namespace Autoprint.Server.Helpers
{
    public static class SecurityHelper
    {
        private static readonly PasswordHasher<string> _hasher = new PasswordHasher<string>();

        public static string ComputeSha256Hash(string rawData)
        {
            using (SHA256 sha256Hash = SHA256.Create())
            {
                byte[] bytes = sha256Hash.ComputeHash(Encoding.UTF8.GetBytes(rawData));
                StringBuilder builder = new StringBuilder();
                for (int i = 0; i < bytes.Length; i++)
                {
                    builder.Append(bytes[i].ToString("x2"));
                }
                return builder.ToString();
            }
        }

        public static string HashPassword(string password)
        {
            return _hasher.HashPassword("", password);
        }

        public static bool VerifyPassword(string hashedPassword, string providedPassword)
        {
            if (string.IsNullOrEmpty(hashedPassword)) return false;

            // Détection de l'ancien hachage SHA-256 (hexadécimal de 64 caractères)
            if (hashedPassword.Length == 64 && System.Text.RegularExpressions.Regex.IsMatch(hashedPassword, @"^[a-fA-F0-9]+$"))
            {
                return hashedPassword.Equals(ComputeSha256Hash(providedPassword), StringComparison.OrdinalIgnoreCase);
            }

            var result = _hasher.VerifyHashedPassword("", hashedPassword, providedPassword);
            return result == PasswordVerificationResult.Success || result == PasswordVerificationResult.SuccessRehashNeeded;
        }

        public static bool IsRehashNeeded(string hashedPassword)
        {
            if (string.IsNullOrEmpty(hashedPassword)) return true;
            if (hashedPassword.Length == 64 && System.Text.RegularExpressions.Regex.IsMatch(hashedPassword, @"^[a-fA-F0-9]+$"))
            {
                return true; // Les anciens hashes SHA-256 doivent être migrés
            }
            return false;
        }

        public static string EscapeLdapFilter(string input)
        {
            if (string.IsNullOrEmpty(input)) return input;
            var sb = new StringBuilder(input.Length);
            foreach (char c in input)
            {
                switch (c)
                {
                    case '\\': sb.Append("\\5c"); break;
                    case '*':  sb.Append("\\2a"); break;
                    case '(':  sb.Append("\\28"); break;
                    case ')':  sb.Append("\\29"); break;
                    case '\0': sb.Append("\\00"); break;
                    default:   sb.Append(c); break;
                }
            }
            return sb.ToString();
        }
    }
}