using System.Security.Cryptography;
using System.Text;
using Autoprint.Server.Data;
using Microsoft.AspNetCore.DataProtection;

namespace Autoprint.Server.Services
{
    /// <summary>
    /// Chiffrement des secrets stockés dans ServerSettings (audit #9).
    /// - En base : ASP.NET Data Protection, clés protégées par DPAPI machine (lisibles uniquement sur ce serveur).
    /// - Dans les sauvegardes : AES-GCM avec une clé dérivée d'un mot de passe choisi à l'export.
    /// Les valeurs non préfixées sont considérées comme du texte clair hérité (avant chiffrement).
    /// </summary>
    public class SecretProtector
    {
        // Tableau simple : traduisible en SQL par EF Core (Where(s => SecretKeys.Contains(s.Key)))
        public static readonly string[] SecretKeys = { "AdServicePassword", "SmtpPass", "AgentApiKey", "AgentApiKeyPrevious" };

        private const string DbPrefix = "enc:v1:";
        private const string BackupPrefix = "bkp:v1:";
        private const int Pbkdf2Iterations = 210_000;

        private readonly IDataProtector _protector;

        public SecretProtector(IDataProtectionProvider provider)
        {
            _protector = provider.CreateProtector("Autoprint.ServerSettings.v1");
        }

        public static bool IsSecret(string key) => SecretKeys.Contains(key, StringComparer.OrdinalIgnoreCase);

        public string Protect(string? plain)
        {
            if (string.IsNullOrEmpty(plain)) return string.Empty;
            return DbPrefix + _protector.Protect(plain);
        }

        public string Unprotect(string? stored)
        {
            if (string.IsNullOrEmpty(stored)) return string.Empty;
            if (!stored.StartsWith(DbPrefix, StringComparison.Ordinal)) return stored; // texte clair hérité
            return _protector.Unprotect(stored[DbPrefix.Length..]);
        }

        /// <summary>Chiffre au démarrage les secrets encore en clair (migration des installations existantes).</summary>
        public int EncryptPlaintextSecrets(ApplicationDbContext context)
        {
            int count = 0;
            foreach (var setting in context.ServerSettings.Where(s => SecretKeys.Contains(s.Key)).ToList())
            {
                if (!string.IsNullOrEmpty(setting.Value) && !setting.Value.StartsWith(DbPrefix, StringComparison.Ordinal))
                {
                    setting.Value = Protect(setting.Value);
                    count++;
                }
            }
            if (count > 0) context.SaveChanges();
            return count;
        }

        // --- Sauvegardes ---

        public static string ProtectForBackup(string plain, string password)
        {
            var salt = RandomNumberGenerator.GetBytes(16);
            var nonce = RandomNumberGenerator.GetBytes(12);
            var key = Rfc2898DeriveBytes.Pbkdf2(password, salt, Pbkdf2Iterations, HashAlgorithmName.SHA256, 32);

            var plainBytes = Encoding.UTF8.GetBytes(plain);
            var cipher = new byte[plainBytes.Length];
            var tag = new byte[16];
            using (var aes = new AesGcm(key, 16)) aes.Encrypt(nonce, plainBytes, cipher, tag);

            return BackupPrefix + Convert.ToBase64String(salt.Concat(nonce).Concat(tag).Concat(cipher).ToArray());
        }

        public static bool IsBackupProtected(string? value) => value != null && value.StartsWith(BackupPrefix, StringComparison.Ordinal);

        /// <exception cref="CryptographicException">Mot de passe incorrect ou donnée altérée.</exception>
        public static string UnprotectFromBackup(string value, string password)
        {
            var data = Convert.FromBase64String(value[BackupPrefix.Length..]);
            var salt = data[..16];
            var nonce = data[16..28];
            var tag = data[28..44];
            var cipher = data[44..];
            var key = Rfc2898DeriveBytes.Pbkdf2(password, salt, Pbkdf2Iterations, HashAlgorithmName.SHA256, 32);

            var plain = new byte[cipher.Length];
            using (var aes = new AesGcm(key, 16)) aes.Decrypt(nonce, cipher, tag, plain);
            return Encoding.UTF8.GetString(plain);
        }
    }
}
