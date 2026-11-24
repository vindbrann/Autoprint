using Autoprint.Server.Data;
using Autoprint.Server.Helpers;
using Autoprint.Shared;
using Microsoft.EntityFrameworkCore;

namespace Autoprint.Server.Services
{
    /// <summary>
    /// Passage des profils SNMP existants au mode automatique (#50). Rien n'est supprimé : les champs du profil restent
    /// enregistrés (ceux que la détection lit déjà sont ignorés en mode automatique, et resservent si le profil repasse
    /// en manuel). Les noms et couleurs personnalisés deviennent des modifications des éléments détectés.
    /// </summary>
    public static class SnmpProfileConverter
    {
        public const string DoneSettingKey = "Snmp_ProfilsAutomatiques";

        /// <summary>Convertit une fois tous les profils de la base. Renvoie le nombre de profils passés en automatique.</summary>
        public static int ConvertAllOnce(ApplicationDbContext context)
        {
            if (context.ServerSettings.Any(s => s.Key == DoneSettingKey)) return 0;

            var profiles = context.SnmpProfiles.Include(p => p.Items).Include(p => p.Overrides).ToList();
            int converted = profiles.Count(Convert);

            context.ServerSettings.Add(new ServerSetting { Key = DoneSettingKey, Value = "true", Description = "Profils SNMP convertis au mode automatique (#50)", Type = "BOOL" });
            context.SaveChanges();
            return converted;
        }

        /// <summary>
        /// Choisit le mode d'un profil créé avant le mode automatique et prépare ses modifications. Vrai si le profil
        /// passe en automatique. Un profil qui lit toners, bacs ou maintenance à des OID propres au constructeur reste
        /// en manuel : c'est en général que l'imprimante ne renseigne pas les tables standard.
        /// </summary>
        public static bool Convert(SnmpProfile profile)
        {
            // Même règle que l'explication affichée dans la console
            if (SnmpStandardOids.HasVendorLevels(profile))
            {
                profile.Mode = SnmpProfileMode.Manuel;
                return false;
            }

            profile.Mode = SnmpProfileMode.Automatique;
            foreach (var item in profile.Items)
            {
                var key = SnmpStandardOids.KeyFor(item.Oid);
                if (key == null || profile.Overrides.Any(o => o.Key == key)) continue;

                var ov = new SnmpProfileOverride { Key = key };
                var name = SnmpText.Repair(item.Name).Trim();
                if (KeepName(item, name)) ov.DisplayName = name.Length > 100 ? name[..100] : name;
                if (item.Category == SnmpItemCategory.Toner && !string.IsNullOrWhiteSpace(item.ColorHex) && item.ColorHex != "#212529" && item.ColorHex != "#6c757d")
                    ov.ColorHex = item.ColorHex;

                if (!ov.IsEmpty) profile.Overrides.Add(ov);
            }
            return true;
        }

        // Noms à ne pas reprendre : code de média brut d'un bac (« iso-a4-white ») et ancien nom proposé « Toner … »
        // pour une pièce d'usure (#46). L'élément garde alors le nom lu dans l'imprimante.
        private static bool KeepName(SnmpProfileItem item, string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return false;
            if (item.Category == SnmpItemCategory.Tray && SnmpText.IsPwgMediaName(name)) return false;
            if (name.StartsWith("Toner ", StringComparison.OrdinalIgnoreCase) && SnmpService.InferSupplyKind(name) == Autoprint.Shared.DTOs.SupplyKind.Maintenance) return false;
            return true;
        }
    }
}
