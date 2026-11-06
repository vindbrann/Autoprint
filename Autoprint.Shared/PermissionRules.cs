namespace Autoprint.Shared
{
    /// <summary>
    /// Qui peut accorder quelle permission (règle unique, appliquée par le serveur et reprise par l'interface pour griser les choix).
    /// - « Administration » (ADMIN_ACCESS) donne tous les droits ; seul un administrateur peut l'accorder ;
    /// - « Paramètres » (SETTINGS_MANAGE) permet de reconfigurer l'authentification AD : seul un compte qui l'a déjà peut l'accorder ;
    /// - toute autre permission : un compte qui l'a déjà, ou un compte qui a « Rôles - écriture » (ROLE_WRITE).
    /// </summary>
    public static class PermissionRules
    {
        public const string Admin = "ADMIN_ACCESS";
        public const string Settings = "SETTINGS_MANAGE";
        public const string RoleWrite = "ROLE_WRITE";

        public static bool CanGrant(ISet<string> callerPermissions, string code)
        {
            if (callerPermissions.Contains(Admin)) return true;
            if (string.Equals(code, Admin, StringComparison.OrdinalIgnoreCase)) return false;
            if (callerPermissions.Contains(code)) return true;
            if (string.Equals(code, Settings, StringComparison.OrdinalIgnoreCase)) return false;
            return callerPermissions.Contains(RoleWrite);
        }
    }
}
