using System.Net;
using Autoprint.Server.Data;
using Autoprint.Shared;
using Lextm.SharpSnmpLib;
using Lextm.SharpSnmpLib.Messaging;
using Lextm.SharpSnmpLib.Security;
using Microsoft.EntityFrameworkCore;

namespace Autoprint.Server.Services
{
    /// <summary>Identifiants SNMP effectifs d'une interrogation (secrets déchiffrés, en mémoire uniquement).</summary>
    public sealed class SnmpCredentials
    {
        public int Version { get; init; } = 2;
        public string Community { get; init; } = "public";
        public string? V3Username { get; init; }
        public SnmpV3SecurityLevel V3SecurityLevel { get; init; } = SnmpV3SecurityLevel.AuthPriv;
        public SnmpAuthProtocol V3AuthProtocol { get; init; } = SnmpAuthProtocol.Sha256;
        public string? V3AuthPassword { get; init; }
        public SnmpPrivProtocol V3PrivProtocol { get; init; } = SnmpPrivProtocol.Aes128;
        public string? V3PrivPassword { get; init; }

        /// <summary>Description lisible pour les messages (jamais de secret).</summary>
        public string Describe() => Version == 3 ? $"SNMPv3, utilisateur « {V3Username} »" : $"SNMP v{(Version == 1 ? "1" : "2c")}";

        public static SnmpCredentials Legacy(string? community, int version) => new()
        {
            Version = version == 1 ? 1 : 2,
            Community = string.IsNullOrWhiteSpace(community) ? "public" : community.Trim()
        };

        public static SnmpCredentials FromProfile(SnmpCredential profile, SecretProtector secrets) => new()
        {
            Version = profile.Version is >= 1 and <= 3 ? profile.Version : 2,
            Community = string.IsNullOrEmpty(profile.Community) ? "public" : secrets.Unprotect(profile.Community),
            V3Username = profile.V3Username,
            V3SecurityLevel = profile.V3SecurityLevel,
            V3AuthProtocol = profile.V3AuthProtocol,
            V3AuthPassword = secrets.Unprotect(profile.V3AuthPassword),
            V3PrivProtocol = profile.V3PrivProtocol,
            V3PrivPassword = secrets.Unprotect(profile.V3PrivPassword)
        };

        /// <summary>
        /// Règle de priorité : profil de l'imprimante (cas particulier) → profil du modèle → anciens champs de l'imprimante.
        /// </summary>
        public static SnmpCredentials Resolve(Imprimante printer, IReadOnlyDictionary<int, SnmpCredential> profiles, SecretProtector secrets)
        {
            int? id = printer.SnmpCredentialId ?? printer.Modele?.SnmpCredentialId;
            if (id != null && profiles.TryGetValue(id.Value, out var profile)) return FromProfile(profile, secrets);
            return Legacy(secrets.Unprotect(printer.SnmpCommunity), printer.SnmpVersion); // chiffrée en base (B32)
        }

        public static async Task<Dictionary<int, SnmpCredential>> LoadProfilesAsync(ApplicationDbContext context, CancellationToken token = default)
            => await context.SnmpCredentials.AsNoTracking().ToDictionaryAsync(c => c.Id, token);
    }

    /// <summary>
    /// Interrogations SNMP v1 / v2c / v3 avec une seule API (Get, Walk).
    /// En v3, la découverte du moteur SNMP de l'imprimante est faite une fois par client.
    /// </summary>
    public sealed class SnmpClient
    {
        private readonly IPEndPoint _endpoint;
        private readonly SnmpCredentials _credentials;
        private readonly VersionCode _versionCode;
        private readonly OctetString _community;
        private ReportMessage? _report;
        private IPrivacyProvider? _privacy;

        public SnmpClient(IPEndPoint endpoint, SnmpCredentials credentials)
        {
            _endpoint = endpoint;
            _credentials = credentials;
            _versionCode = credentials.Version switch { 1 => VersionCode.V1, 3 => VersionCode.V3, _ => VersionCode.V2 };
            _community = new OctetString(credentials.Community);
        }

        public IList<Variable> Get(IList<Variable> variables, int timeoutMs)
        {
            if (_versionCode != VersionCode.V3) return Messenger.Get(_versionCode, _endpoint, _community, variables, timeoutMs);

            var report = Discover(timeoutMs);
            var request = new GetRequestMessage(VersionCode.V3, Messenger.NextMessageId, Messenger.NextRequestId,
                new OctetString(_credentials.V3Username ?? ""), OctetString.Empty, variables, GetPrivacy(), Messenger.MaxMessageSize, report);
            return ReadV3Reply(request.GetResponse(timeoutMs, _endpoint));
        }

        public Task<IList<Variable>> GetAsync(IList<Variable> variables, int timeoutMs) => Task.Run(() => Get(variables, timeoutMs));

        /// <summary>Parcourt le sous-arbre <paramref name="root"/> et ajoute les valeurs trouvées à <paramref name="results"/>.</summary>
        public void Walk(ObjectIdentifier root, IList<Variable> results, int timeoutMs)
        {
            if (_versionCode != VersionCode.V3)
            {
                Messenger.Walk(_versionCode, _endpoint, _community, root, results, timeoutMs, WalkMode.WithinSubtree);
                return;
            }

            var report = Discover(timeoutMs);
            var current = new Variable(root);
            for (int i = 0; i < 10_000; i++) // garde-fou contre un agent qui bouclerait
            {
                var request = new GetNextRequestMessage(VersionCode.V3, Messenger.NextMessageId, Messenger.NextRequestId,
                    new OctetString(_credentials.V3Username ?? ""), OctetString.Empty, new List<Variable> { current }, GetPrivacy(), Messenger.MaxMessageSize, report);
                var next = ReadV3Reply(request.GetResponse(timeoutMs, _endpoint)).FirstOrDefault();
                if (next == null || next.Data.TypeCode == SnmpType.EndOfMibView || !IsInSubtree(next.Id, root)) return;
                results.Add(next);
                current = new Variable(next.Id);
            }
        }

        private ReportMessage Discover(int timeoutMs)
        {
            if (_report != null) return _report;
            if (string.IsNullOrWhiteSpace(_credentials.V3Username))
                throw new InvalidOperationException("Profil SNMPv3 incomplet : nom d'utilisateur manquant.");

            var discovery = Messenger.GetNextDiscovery(SnmpType.GetRequestPdu);
            _report = discovery.GetResponse(timeoutMs, _endpoint);
            return _report;
        }

        private IPrivacyProvider GetPrivacy()
        {
            if (_privacy != null) return _privacy;
            var c = _credentials;

            if (c.V3SecurityLevel == SnmpV3SecurityLevel.NoAuthNoPriv)
                return _privacy = new DefaultPrivacyProvider(DefaultAuthenticationProvider.Instance);

            if (string.IsNullOrEmpty(c.V3AuthPassword) || c.V3AuthPassword.Length < 8)
                throw new InvalidOperationException("Profil SNMPv3 incomplet : mot de passe d'authentification manquant (8 caractères minimum).");

            var authPassword = new OctetString(c.V3AuthPassword);
            // SHA-1, MD5 et 3DES : obsolètes, proposés uniquement pour les anciens matériels (signalés « déconseillé » dans la console)
#pragma warning disable CS0618
            IAuthenticationProvider auth = c.V3AuthProtocol switch
            {
                SnmpAuthProtocol.Sha1 => new SHA1AuthenticationProvider(authPassword),
                SnmpAuthProtocol.Sha384 => new SHA384AuthenticationProvider(authPassword),
                SnmpAuthProtocol.Sha512 => new SHA512AuthenticationProvider(authPassword),
                SnmpAuthProtocol.Md5 => new MD5AuthenticationProvider(authPassword),
                _ => new SHA256AuthenticationProvider(authPassword)
            };

            if (c.V3SecurityLevel == SnmpV3SecurityLevel.AuthNoPriv)
                return _privacy = new DefaultPrivacyProvider(auth);

            if (string.IsNullOrEmpty(c.V3PrivPassword) || c.V3PrivPassword.Length < 8)
                throw new InvalidOperationException("Profil SNMPv3 incomplet : mot de passe de chiffrement manquant (8 caractères minimum).");

            var privPassword = new OctetString(c.V3PrivPassword);
            return _privacy = c.V3PrivProtocol switch
            {
                SnmpPrivProtocol.Aes192 => new AES192PrivacyProvider(privPassword, auth),
                SnmpPrivProtocol.Aes256 => new AES256PrivacyProvider(privPassword, auth),
                SnmpPrivProtocol.TripleDes => new TripleDESPrivacyProvider(privPassword, auth),
                _ => new AESPrivacyProvider(privPassword, auth)
            };
#pragma warning restore CS0618
        }

        private static IList<Variable> ReadV3Reply(ISnmpMessage reply)
        {
            // Une réponse de type « Report » signale un refus du moteur SNMPv3 (utilisateur inconnu, mauvais mot de passe…)
            if (reply is ReportMessage)
                throw new InvalidOperationException("Identifiants SNMPv3 refusés par l'imprimante (utilisateur, mots de passe ou protocoles incorrects).");

            var pdu = reply.Pdu();
            if (pdu.ErrorStatus.ToInt32() != 0)
                throw new InvalidOperationException($"Erreur SNMP renvoyée par l'imprimante (code {pdu.ErrorStatus.ToInt32()}).");
            return pdu.Variables;
        }

        private static bool IsInSubtree(ObjectIdentifier oid, ObjectIdentifier root)
        {
            var o = oid.ToNumerical();
            var r = root.ToNumerical();
            if (o.Length <= r.Length) return false;
            for (int i = 0; i < r.Length; i++) if (o[i] != r[i]) return false;
            return true;
        }
    }
}
