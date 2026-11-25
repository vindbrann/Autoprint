using System.Security.Cryptography;
using System.Text;
using Autoprint.Server.Helpers;

namespace Autoprint.Server.Services
{
    /// <summary>
    /// Le compte de service Active Directory d'Autoprint a été refusé par l'annuaire (mot de passe incorrect, compte
    /// verrouillé, désactivé ou expiré). Ce n'est ni un mauvais mot de passe de l'utilisateur, ni une panne de l'annuaire.
    /// </summary>
    public class AdServiceAccountRejectedException : DirectoryUnavailableException
    {
        public string Reason { get; }

        public AdServiceAccountRejectedException(string reason, Exception? inner)
            : base($"Connexion Active Directory impossible : le compte de service d'Autoprint est refusé par l'annuaire ({reason}). " +
                   "Un administrateur doit le corriger dans Paramètres > Active Directory, en se connectant avec le compte local.", inner)
        {
            Reason = reason;
        }
    }

    /// <summary>
    /// Compte de service Active Directory : reconnaissance des refus et pause après un refus.
    /// Chaque connexion d'un utilisateur AD fait plusieurs liaisons avec ce compte : avec un mauvais mot de passe, quelques
    /// connexions suffisaient à le verrouiller dans l'annuaire, et toutes les connexions AD échouaient ensuite, même après
    /// avoir ressaisi le bon mot de passe (campagne du 25/11/2026). Après un refus, le compte n'est plus présenté à l'annuaire
    /// pendant <see cref="Pause"/>, tant que son mot de passe enregistré n'a pas changé.
    /// </summary>
    public static class AdServiceAccount
    {
        public static readonly TimeSpan Pause = TimeSpan.FromMinutes(5);
        private const string Ambiguous = "mot de passe incorrect, compte verrouillé ou inconnu";

        private static readonly object Gate = new();
        private static DateTime _pausedUntilUtc = DateTime.MinValue;
        private static string? _pausedPasswordHash;
        private static string _pausedReason = "";

        /// <summary>
        /// Motif lisible si l'exception (ou l'une de ses causes) est un refus d'authentification du compte, sinon null.
        /// Codes Windows : 0x8007052E (ouverture de session refusée), 0x80070775 (compte verrouillé), 0x80070533 (désactivé),
        /// 0x8007052F / 0x80070532 / 0x80070773 (restriction, mot de passe expiré ou à changer) ; LDAP : erreur 49, et
        /// « data 52e / 775 / 533 / 532 / 773 » dans le message étendu du contrôleur de domaine.
        /// </summary>
        public static string? RejectionReason(Exception ex)
        {
            for (var e = ex; e != null; e = e.InnerException)
            {
                string text = e.Message ?? "";
                if (e is System.DirectoryServices.DirectoryServicesCOMException ds && !string.IsNullOrEmpty(ds.ExtendedErrorMessage))
                    text += " " + ds.ExtendedErrorMessage;

                if (text.Contains("data 775", StringComparison.OrdinalIgnoreCase)) return "compte verrouillé dans l'annuaire";
                if (text.Contains("data 533", StringComparison.OrdinalIgnoreCase)) return "compte désactivé dans l'annuaire";
                if (text.Contains("data 532", StringComparison.OrdinalIgnoreCase) || text.Contains("data 773", StringComparison.OrdinalIgnoreCase)) return "mot de passe du compte expiré ou à changer";
                if (text.Contains("data 52e", StringComparison.OrdinalIgnoreCase)) return "mot de passe incorrect";

                uint hr = (uint)e.HResult;
                if (hr == 0x80070775) return "compte verrouillé dans l'annuaire";
                if (hr == 0x80070533) return "compte désactivé dans l'annuaire";
                if (hr is 0x80070532 or 0x80070773) return "mot de passe du compte expiré ou à changer";
                // Sans « data xxx » (PrincipalContext), Windows ne distingue pas un compte verrouillé d'un mot de passe faux
                if (hr is 0x8007052E or 0x8007052F) return Ambiguous;
                if (e is System.DirectoryServices.Protocols.LdapException ldap && ldap.ErrorCode == 49) return Ambiguous;
                if (e is System.Security.Authentication.AuthenticationException) return Ambiguous;
            }
            return null;
        }

        /// <summary>
        /// Une liaison LDAP avec le compte (RootDSE) : renvoie le contrôleur de domaine joint, ou lève l'exception de l'annuaire.
        /// Préférée à PrincipalContext pour tester le compte : un compte verrouillé y est signalé « data 775 » dans le message
        /// étendu, alors que PrincipalContext ne renvoie que « nom d'utilisateur ou mot de passe incorrect » (labo, 25/11/2026).
        /// </summary>
        [System.Runtime.Versioning.SupportedOSPlatform("windows")]
        public static string Bind(string domain, string user, string password)
        {
            // Domaine saisi dans la console : relu et reconstruit (nom DNS seulement) avant d'entrer dans le chemin LDAP
            string host = LdapPathBuilder.NormalizeDomain(domain) ?? throw new ArgumentException("Nom de domaine invalide.", nameof(domain));
            using var entry = new System.DirectoryServices.DirectoryEntry("LDAP://" + host + "/RootDSE", user, password,
                System.DirectoryServices.AuthenticationTypes.Secure | System.DirectoryServices.AuthenticationTypes.Sealing);
            entry.RefreshCache(); // force la liaison avec ce compte
            return entry.Properties["dnsHostName"].Count > 0 ? entry.Properties["dnsHostName"][0]?.ToString() ?? domain : domain;
        }

        /// <summary>
        /// Refus sans détail (liaison Kerberos, compte écrit « DOMAINE\x » ou « x@domaine ») : lit l'état du compte dans
        /// l'annuaire avec l'identité du serveur (compte ordinateur), sans présenter le mot de passe, donc sans compter
        /// d'échec. Renvoie le motif précis (verrouillé, désactivé, expiré), « mot de passe incorrect » si le compte est sain,
        /// ou le motif reçu si l'état ne peut pas être lu.
        /// </summary>
        [System.Runtime.Versioning.SupportedOSPlatform("windows")]
        public static string Refine(string reason, string domain, string user)
        {
            if (reason != Ambiguous) return reason;
            try
            {
                string sam = user.Contains('\\') ? user[(user.IndexOf('\\') + 1)..] : user.Contains('@') ? user[..user.IndexOf('@')] : user;
                string? host = LdapPathBuilder.NormalizeDomain(domain);
                if (host == null) return reason;
                using var root = new System.DirectoryServices.DirectoryEntry("LDAP://" + host);
                using var searcher = new System.DirectoryServices.DirectorySearcher(root,
                    $"(&(objectCategory=person)(objectClass=user)(sAMAccountName={SecurityHelper.EscapeLdapFilter(sam)}))", new[] { "distinguishedName" });
                var found = searcher.FindOne();
                if (found == null) return "compte inconnu dans l'annuaire";
                using var account = found.GetDirectoryEntry();
                account.RefreshCache(new[] { "msDS-User-Account-Control-Computed", "userAccountControl" });
                int uac = account.Properties["userAccountControl"].Count > 0 ? Convert.ToInt32(account.Properties["userAccountControl"][0]) : 0;
                int computed = account.Properties["msDS-User-Account-Control-Computed"].Count > 0 ? Convert.ToInt32(account.Properties["msDS-User-Account-Control-Computed"][0]) : 0;
                if ((computed & 0x10) != 0) return "compte verrouillé dans l'annuaire";          // UF_LOCKOUT
                if ((uac & 0x2) != 0) return "compte désactivé dans l'annuaire";                 // UF_ACCOUNTDISABLE
                if ((computed & 0x800000) != 0) return "mot de passe du compte expiré ou à changer"; // UF_PASSWORD_EXPIRED
                return "mot de passe incorrect";
            }
            catch (Exception)
            {
                return reason; // état illisible (serveur hors domaine, droits) : motif d'origine
            }
        }

        public static bool IsLocked(string reason) => reason == "compte verrouillé dans l'annuaire";

        /// <summary>Lève l'exception de refus sans contacter l'annuaire si le compte est en pause avec ce même mot de passe.</summary>
        public static void ThrowIfPaused(string servicePassword)
        {
            lock (Gate)
            {
                if (DateTime.UtcNow < _pausedUntilUtc && _pausedPasswordHash == Hash(servicePassword))
                    throw new AdServiceAccountRejectedException(_pausedReason, null);
            }
        }

        /// <summary>Si <paramref name="ex"/> est un refus du compte : pause, puis exception de refus. Sinon ne fait rien.</summary>
        public static void ThrowIfRejected(Exception ex, string servicePassword)
        {
            var reason = RejectionReason(ex);
            if (reason == null) return;
            lock (Gate)
            {
                _pausedUntilUtc = DateTime.UtcNow.Add(Pause);
                _pausedPasswordHash = Hash(servicePassword);
                _pausedReason = reason;
            }
            Console.WriteLine($"[AD] Compte de service refusé ({reason}) : plus de tentative pendant {Pause.TotalMinutes:0} min.");
            throw new AdServiceAccountRejectedException(reason, ex);
        }

        /// <summary>Fin de la pause (mot de passe ou compte de service changé dans les paramètres).</summary>
        public static void Reset()
        {
            lock (Gate) { _pausedUntilUtc = DateTime.MinValue; _pausedPasswordHash = null; }
        }

        private static string Hash(string value) =>
            Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value ?? "")));
    }
}
