using Autoprint.Server.Data;
using Autoprint.Shared;
using Microsoft.EntityFrameworkCore;
using System.Text.RegularExpressions;

namespace Autoprint.Server.Services
{
    /// <summary>
    /// Noms d'hôte acceptés par le serveur (B82, remplace le filtrage « AllowedHosts » d'ASP.NET Core).
    /// La liste est en base (paramètre « AllowedHosts ») et se modifie dans la console sans redémarrer ;
    /// au premier démarrage, elle reprend la valeur « AllowedHosts » d'appsettings.json écrite par l'installeur.
    /// Liste vide ou « * » : tous les noms sont acceptés (serveurs mis à jour depuis une 1.1.x sans nom public).
    /// localhost reste toujours accepté.
    /// </summary>
    public class AllowedHostsService
    {
        public const string SettingKey = "AllowedHosts";
        private static readonly Regex HostPattern = new(@"^(\*\.)?[A-Za-z0-9]([A-Za-z0-9-]*[A-Za-z0-9])?(\.[A-Za-z0-9]([A-Za-z0-9-]*[A-Za-z0-9])?)*$", RegexOptions.Compiled);

        private volatile string[] _hosts = Array.Empty<string>();

        public IReadOnlyList<string> Hosts => _hosts;
        public bool AllowAll => _hosts.Length == 0;

        public static bool IsLocal(string host)
            => host.Equals("localhost", StringComparison.OrdinalIgnoreCase) || host == "127.0.0.1" || host == "::1" || host == "[::1]";

        public static bool IsValidHost(string host) => HostPattern.IsMatch(host);

        /// <summary>« a;b, c » vers une liste sans doublon ni localhost ; « * » donne une liste vide (tout accepté).</summary>
        public static List<string> Parse(string? value)
        {
            var list = (value ?? "").Split(new[] { ';', ',', ' ' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                                    .Distinct(StringComparer.OrdinalIgnoreCase)
                                    .ToList();
            if (list.Contains("*")) return new List<string>();
            return list.Where(h => !IsLocal(h)).ToList();
        }

        public bool IsAllowed(string? host)
        {
            var hosts = _hosts;
            if (hosts.Length == 0 || string.IsNullOrEmpty(host) || IsLocal(host)) return true;
            foreach (var h in hosts)
            {
                if (h.StartsWith("*.", StringComparison.Ordinal))
                {
                    if (host.EndsWith(h[1..], StringComparison.OrdinalIgnoreCase)) return true;
                }
                else if (host.Equals(h, StringComparison.OrdinalIgnoreCase)) return true;
            }
            return false;
        }

        /// <summary>Premier nom public (liens envoyés par courriel) : ni joker ni localhost.</summary>
        public string? PublicHost => _hosts.FirstOrDefault(h => !h.StartsWith("*.", StringComparison.Ordinal));

        /// <summary>Valeur d'appsettings.json, appliquée avant la lecture de la base (et conservée si la base est illisible).</summary>
        public void UseConfiguration(IConfiguration config) => _hosts = Parse(config[SettingKey]).ToArray();

        /// <summary>Démarrage : lit la liste en base, ou la crée à partir d'appsettings.json.</summary>
        public void Initialize(ApplicationDbContext db, IConfiguration config)
        {
            var setting = db.ServerSettings.Find(SettingKey);
            if (setting == null)
            {
                var seed = Parse(config[SettingKey]);
                setting = new ServerSetting { Key = SettingKey, Value = string.Join(";", seed), Type = "STRING", Description = "Noms d'hôte acceptés par le serveur (séparés par ;)" };
                db.ServerSettings.Add(setting);
                db.SaveChanges();
            }
            _hosts = Parse(setting.Value).ToArray();
        }

        /// <summary>Recharge la liste depuis la base (après une restauration, par exemple).</summary>
        public async Task ReloadAsync(ApplicationDbContext db)
        {
            var value = await db.ServerSettings.AsNoTracking().Where(s => s.Key == SettingKey).Select(s => s.Value).FirstOrDefaultAsync();
            _hosts = Parse(value).ToArray();
        }

        public async Task SaveAsync(ApplicationDbContext db, IEnumerable<string> hosts)
        {
            var list = Parse(string.Join(";", hosts));
            var setting = await db.ServerSettings.FindAsync(SettingKey);
            if (setting == null) db.ServerSettings.Add(new ServerSetting { Key = SettingKey, Value = string.Join(";", list), Type = "STRING", Description = "Noms d'hôte acceptés par le serveur (séparés par ;)" });
            else setting.Value = string.Join(";", list);
            await db.SaveChangesAsync();
            _hosts = list.ToArray();
        }
    }
}
