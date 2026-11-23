using Autoprint.Server.Data;
using Autoprint.Shared;
using Autoprint.Shared.DTOs;
using Microsoft.EntityFrameworkCore;

namespace Autoprint.Server.Services
{
    /// <summary>Réglages de supervision partagés par le diagnostic, la supervision et les rapports (#49).</summary>
    public static class MonitoringSettings
    {
        public const string LowThresholdKey = "Monitoring_LowThresholdPercent";

        /// <summary>Seuil « bas » par défaut : 20 %, pour avoir le temps de commander et d'envoyer un consommable.</summary>
        public const int DefaultLowThresholdPercent = 20;

        public const string LeadTimeKey = "Monitoring_SupplyLeadTimeDays";

        /// <summary>Délai d'approvisionnement par défaut : 14 jours entre la commande et la réception d'un consommable (#51).</summary>
        public const int DefaultLeadTimeDays = 14;

        public static int ParseLeadTime(string? value)
            => int.TryParse(value, out var v) && v is >= 1 and <= 180 ? v : DefaultLeadTimeDays;

        public static async Task<int> GetLeadTimeAsync(ApplicationDbContext context, CancellationToken token = default)
        {
            var value = await context.ServerSettings.AsNoTracking()
                .Where(s => s.Key == LeadTimeKey)
                .Select(s => s.Value)
                .FirstOrDefaultAsync(token);
            return ParseLeadTime(value);
        }

        /// <summary>Délai du profil s'il en a un, sinon réglage global.</summary>
        public static int EffectiveLeadTime(SnmpProfile? profile, int globalLeadTime)
            => profile?.SupplyLeadTimeDays is int d and >= 1 and <= 180 ? d : globalLeadTime;

        public static int ParseLowThreshold(string? value)
            => int.TryParse(value, out var v) && v is >= 1 and <= 99 ? v : DefaultLowThresholdPercent;

        public static async Task<int> GetLowThresholdAsync(ApplicationDbContext context, CancellationToken token = default)
        {
            var value = await context.ServerSettings.AsNoTracking()
                .Where(s => s.Key == LowThresholdKey)
                .Select(s => s.Value)
                .FirstOrDefaultAsync(token);
            return ParseLowThreshold(value);
        }
    }

    /// <summary>Statut de supervision d'une imprimante et historique des niveaux, à partir d'un diagnostic (#49).</summary>
    public static class PrinterHealth
    {
        // Écran de la console sans gravité : seuls ces mots signalent une panne (comportement antérieur conservé)
        private static readonly string[] ConsoleFailureWords = { "erreur", "bourrage", "ouvert" };

        /// <summary>Offline, Critical, Warning ou Ok.</summary>
        public static string Evaluate(PrinterDiagnosticResult result)
        {
            if (!result.PingSuccess) return "Offline";

            var alerts = result.Alerts ?? new List<PrinterAlert>();
            if (alerts.Any(a => a.Severity == AlertSeverity.Critical)
                || alerts.Any(a => a.Severity == AlertSeverity.Info && ConsoleFailureWords.Any(w => a.Text.Contains(w, StringComparison.OrdinalIgnoreCase))))
            {
                return "Critical";
            }

            bool statusWarning = result.Status != null && (result.Status.Contains("Alerte") || result.Status.Contains("Inconnu"));
            if (statusWarning
                || alerts.Any(a => a.Severity == AlertSeverity.Warning)
                || (result.Toners ?? new List<TonerLevelResult>()).Any(t => t.NeedsAttention || t.NeedsOrder))
            {
                return "Warning";
            }

            return "Ok";
        }
    }
}
