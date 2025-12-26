using System.Net.NetworkInformation;
using System.Text;
using Autoprint.Shared;
using Autoprint.Server.Data;
using Microsoft.EntityFrameworkCore;

namespace Autoprint.Server.Services
{
    public class DiscoveryService
    {
        private readonly ILogger<DiscoveryService> _logger;
        private readonly IServiceScopeFactory _scopeFactory;
        // 1. On injecte le service Email (via ScopeFactory car on est dans un Singleton/Worker souvent)

        public DiscoveryService(ILogger<DiscoveryService> logger, IServiceScopeFactory scopeFactory)
        {
            _logger = logger;
            _scopeFactory = scopeFactory;
        }

        public async Task ExecuteScanAsync(int profileId)
        {
            // On crée un scope unique pour tout le traitement (DB + Email)
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var emailService = scope.ServiceProvider.GetRequiredService<IEmailService>(); // Récupération du service

            var profile = await db.DiscoveryProfiles.FindAsync(profileId);
            if (profile == null) return;

            var startTime = DateTime.Now;
            _logger.LogInformation($"[Discovery] Démarrage du scan : {profile.Name}");

            // --- LOGIQUE DE SCAN (IDENTIQUE A AVANT) ---
            var separators = new[] { ';', '\n', '\r', ',' };
            var targets = profile.TargetRanges.Split(separators, StringSplitOptions.RemoveEmptyEntries);
            var exclusions = (profile.ExcludedRanges ?? "").Split(separators, StringSplitOptions.RemoveEmptyEntries).ToList();

            var knownSubnets = profile.SkipKnownSubnets
                ? await db.Emplacements.Select(e => e.CidrIpv4).ToListAsync()
                : new List<string>();

            var subnetsToScan = new List<string>();

            foreach (var cidrRaw in targets)
            {
                var cidr = cidrRaw.Trim();
                if (string.IsNullOrWhiteSpace(cidr) || IsRangeTooLarge(cidr)) continue;

                var chunks = GenerateSubnets24(cidr);
                foreach (var chunk in chunks)
                {
                    if (exclusions.Any(ex => chunk.StartsWith(ex.Replace("/24", "").Trim()))) continue;
                    if (knownSubnets.Contains(chunk)) continue;
                    subnetsToScan.Add(chunk);
                }
            }

            var detectedSubnets = new List<string>();
            var probes = ParseProbes(profile.ProbeTargets);
            var parallelOptions = new ParallelOptions { MaxDegreeOfParallelism = 50 };

            await Parallel.ForEachAsync(subnetsToScan, parallelOptions, async (subnet, token) =>
            {
                if (await IsSubnetAliveAsync(subnet, probes))
                {
                    lock (detectedSubnets) { detectedSubnets.Add(subnet); }
                }
            });

            // --- SAUVEGARDE ET RAPPORT ---

            var newNetworks = new List<string>(); // On garde la liste des nouveaux pour le mail
            string rapportText;

            if (detectedSubnets.Any())
            {
                foreach (var detected in detectedSubnets)
                {
                    if (!await db.Emplacements.AnyAsync(e => e.CidrIpv4 == detected))
                    {
                        db.Emplacements.Add(new Emplacement
                        {
                            Nom = $"Nouveau Réseau ({detected})",
                            CidrIpv4 = detected,
                            Status = LieuStatus.Detected
                        });
                        newNetworks.Add(detected);
                        _logger.LogInformation($"[Discovery] Nouveau réseau détecté : {detected}");
                    }
                }

                if (newNetworks.Any())
                {
                    await db.SaveChangesAsync();
                    rapportText = $"Succès : {newNetworks.Count} nouveaux réseaux ajoutés.";
                }
                else
                {
                    rapportText = $"Terminé. {detectedSubnets.Count} réseaux détectés mais déjà existants.";
                }
            }
            else
            {
                rapportText = "Terminé. R.A.S (Aucun réseau détecté).";
            }

            profile.LastRunResult = rapportText;
            profile.LastRunDate = DateTime.Now;
            await db.SaveChangesAsync();

            // --- ENVOI MAIL ---
            if (profile.SendEmailReport && !string.IsNullOrEmpty(profile.EmailRecipients))
            {
                await SendReportEmail(emailService, profile, newNetworks, detectedSubnets.Count, startTime);
            }
        }

        private async Task SendReportEmail(IEmailService emailService, DiscoveryProfile profile, List<string> newNetworks, int totalDetected, DateTime start)
        {
            var duration = DateTime.Now - start;
            var sb = new StringBuilder();

            sb.Append($"<h3>Rapport de Scan : {profile.Name}</h3>");
            sb.Append($"<p><strong>Date :</strong> {start:dd/MM/yyyy HH:mm}<br/>");
            sb.Append($"<strong>Durée :</strong> {duration.TotalSeconds:F1} sec<br/>");
            sb.Append($"<strong>Résultat :</strong> {profile.LastRunResult}</p>");

            if (newNetworks.Any())
            {
                sb.Append("<div style='background-color:#d4edda; padding:10px; border-radius:5px; border:1px solid #c3e6cb; color:#155724;'>");
                sb.Append($"<strong>🚀 {newNetworks.Count} Nouveaux réseaux ajoutés :</strong>");
                sb.Append("<ul>");
                foreach (var net in newNetworks)
                {
                    sb.Append($"<li>{net}</li>");
                }
                sb.Append("</ul></div>");
                sb.Append("<p><em>Connectez-vous à Autoprint pour valider ces lieux.</em></p>");
            }
            else
            {
                sb.Append("<div style='background-color:#f8f9fa; padding:10px; border-radius:5px; border:1px solid #ddd; color:#666;'>");
                sb.Append("Aucun nouveau réseau à ajouter à la base de données.");
                sb.Append("</div>");
            }

            var recipients = profile.EmailRecipients.Split(';', StringSplitOptions.RemoveEmptyEntries);
            var subject = newNetworks.Any()
                ? $"[Autoprint] 🚀 {newNetworks.Count} Nouveaux réseaux détectés"
                : $"[Autoprint] Rapport de scan (R.A.S)";

            foreach (var recipient in recipients)
            {
                try
                {
                    await emailService.SendEmailAsync(recipient.Trim(), subject, sb.ToString());
                }
                catch (Exception ex)
                {
                    _logger.LogError($"[Discovery] Erreur envoi mail à {recipient} : {ex.Message}");
                }
            }
        }

        // --- Helpers (Inchangés) ---

        private bool IsRangeTooLarge(string cidr)
        {
            if (!cidr.Contains("/")) return true;
            try
            {
                var parts = cidr.Split('/');
                if (parts.Length != 2) return true;
                if (int.TryParse(parts[1], out int mask)) return mask < 16;
            }
            catch { return true; }
            return true;
        }

        private List<string> GenerateSubnets24(string cidr)
        {
            var results = new List<string>();
            try
            {
                var parts = cidr.Split('/');
                var ip = parts[0];
                int mask = int.Parse(parts[1]);
                if (mask >= 24) { results.Add(cidr); return results; }
                var octets = ip.Split('.').Select(int.Parse).ToArray();
                int count = (int)Math.Pow(2, 24 - mask);
                for (int i = 0; i < count; i++)
                {
                    int third = octets[2] + i;
                    if (third <= 255) results.Add($"{octets[0]}.{octets[1]}.{third}.0/24");
                }
            }
            catch { }
            return results;
        }

        private async Task<bool> IsSubnetAliveAsync(string subnetCidr, List<int> suffixes)
        {
            try
            {
                var baseIp = subnetCidr.Split('/')[0];
                var octets = baseIp.Split('.');
                string prefix = $"{octets[0]}.{octets[1]}.{octets[2]}.";
                using var pinger = new Ping();
                foreach (var suffix in suffixes)
                {
                    try
                    {
                        var reply = await pinger.SendPingAsync(prefix + suffix, 200);
                        if (reply.Status == IPStatus.Success) return true;
                    }
                    catch { }
                }
            }
            catch { }
            return false;
        }

        private List<int> ParseProbes(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return new List<int> { 254, 1 };
            return input.Split(new[] { ';', ',' }, StringSplitOptions.RemoveEmptyEntries)
                        .Select(s => int.TryParse(s.Trim(), out int n) ? n : -1)
                        .Where(n => n > 0 && n < 255)
                        .ToList();
        }
    }
}