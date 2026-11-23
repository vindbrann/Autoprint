using Autoprint.Server.Data;
using Autoprint.Shared;
using Autoprint.Shared.DTOs;
using Microsoft.EntityFrameworkCore;

namespace Autoprint.Server.Services
{
    /// <summary>
    /// Historique des consommables et prévision (#51). Un relevé est écrit quand le niveau ou l'état change, et au
    /// moins une fois par jour : la table reste légère même sur un grand parc relevé toutes les heures.
    /// </summary>
    public static class SupplyHistoryService
    {
        private const int HistoryDays = 400;
        private static readonly TimeSpan MinInterval = TimeSpan.FromHours(20);
        private const int ReplacementJump = 30;   // remontée minimale du niveau (points) pour un changement de consommable

        /// <summary>Clé d'historique : clé stable de la MIB, sinon le nom (champ de profil manuel).</summary>
        public static string KeyOf(TonerLevelResult t)
        {
            var key = !string.IsNullOrWhiteSpace(t.Key) ? t.Key! : "name:" + t.Color.Trim().ToLowerInvariant();
            return key.Length > 120 ? key[..120] : key;
        }

        /// <summary>Relevés récents des imprimantes demandées, regroupés par imprimante.</summary>
        public static async Task<Dictionary<int, List<SupplyHistory>>> LoadAsync(ApplicationDbContext context, IEnumerable<int> printerIds, CancellationToken token = default)
        {
            var ids = printerIds.Distinct().ToList();
            var since = DateTime.UtcNow.AddDays(-HistoryDays);
            var rows = await context.SupplyHistories.AsNoTracking()
                .Where(h => ids.Contains(h.ImprimanteId) && h.RecordedAt >= since)
                .ToListAsync(token);
            return rows.GroupBy(h => h.ImprimanteId).ToDictionary(g => g.Key, g => g.OrderBy(h => h.RecordedAt).ToList());
        }

        /// <summary>
        /// Ajoute au contexte (et à <paramref name="rows"/>) les relevés nouveaux de ce diagnostic. Au premier relevé
        /// d'un consommable, l'ancien historique quotidien (TonerHistories, rangé par nom) est repris.
        /// </summary>
        public static async Task RecordAsync(ApplicationDbContext context, int printerId, PrinterDiagnosticResult result, List<SupplyHistory> rows, CancellationToken token = default)
        {
            if (!result.PingSuccess || result.Toners == null) return;
            var now = DateTime.UtcNow;

            foreach (var t in result.Toners)
            {
                var key = KeyOf(t);
                var previous = rows.Where(r => r.SupplyKey == key).OrderBy(r => r.RecordedAt).LastOrDefault();

                if (previous == null)
                {
                    var legacy = await context.TonerHistories.AsNoTracking()
                        .Where(h => h.ImprimanteId == printerId && h.ComponentColor == t.Color)
                        .OrderBy(h => h.RecordedAt)
                        .ToListAsync(token);
                    foreach (var h in legacy.Where(h => h.RecordedAt >= now.AddDays(-HistoryDays)))
                    {
                        var copy = new SupplyHistory
                        {
                            ImprimanteId = printerId, SupplyKey = key, Name = Truncate(t.Color, 100), Level = h.LevelPercent,
                            State = SupplyState.Unknown, IsReceptacle = t.IsReceptacle, RecordedAt = h.RecordedAt,
                            IsReplacement = previous != null && previous.Level >= 0 && h.LevelPercent - previous.Level >= ReplacementJump
                        };
                        context.SupplyHistories.Add(copy);
                        rows.Add(copy);
                        previous = copy;
                    }
                }

                bool changed = previous == null || previous.Level != t.CurrentLevel || previous.State != t.State;
                if (!changed && now - previous!.RecordedAt < MinInterval) continue;

                var row = new SupplyHistory
                {
                    ImprimanteId = printerId,
                    SupplyKey = key,
                    Name = Truncate(t.Color, 100),
                    Level = t.CurrentLevel,
                    State = t.State,
                    IsReceptacle = t.IsReceptacle,
                    PageCounter = result.PageCounter,
                    RecordedAt = now,
                    IsReplacement = previous != null && IsReplacement(previous, t)
                };
                context.SupplyHistories.Add(row);
                rows.Add(row);
            }
        }

        private static bool IsReplacement(SupplyHistory previous, TonerLevelResult current)
        {
            if (previous.Level >= 0 && current.CurrentLevel >= 0 && current.CurrentLevel - previous.Level >= ReplacementJump) return true;
            // Consommable sans niveau : retour à OK après Bas / Vide
            return previous.State is SupplyState.Low or SupplyState.Empty && current.State == SupplyState.Ok
                   && (current.CurrentLevel < 0 || previous.Level < 0 || current.CurrentLevel > previous.Level);
        }

        /// <summary>
        /// Complète chaque consommable du diagnostic : jours restants, fiabilité, « Bas depuis », durée de vie moyenne et
        /// « à commander » (Bas / Vide, ou fin prévue dans moins que <paramref name="leadTimeDays"/> avec une fiabilité au moins moyenne).
        /// </summary>
        public static void Enrich(PrinterDiagnosticResult result, IReadOnlyList<SupplyHistory> rows, int leadTimeDays, DateTime? nowUtc = null)
        {
            var now = nowUtc ?? DateTime.UtcNow;
            result.SupplyLeadTimeDays = leadTimeDays;
            if (result.Toners == null) return;

            // Compteur de pages de l'imprimante dans le temps (tous consommables confondus)
            var pageSeries = rows.Where(r => r.PageCounter > 0)
                                 .GroupBy(r => r.RecordedAt)
                                 .Select(g => (At: g.Key, Pages: g.Max(r => r.PageCounter)))
                                 .OrderBy(p => p.At)
                                 .ToList();
            if (result.PageCounter > 0) pageSeries.Add((now, result.PageCounter));
            double? pagesPerDay = PagesPerDay(pageSeries, now);

            foreach (var t in result.Toners)
            {
                var key = KeyOf(t);
                var history = rows.Where(r => r.SupplyKey == key).OrderBy(r => r.RecordedAt).ToList();
                var forecast = Forecast(history, t, result.PageCounter, pagesPerDay, now);

                t.EstimatedDaysRemaining = forecast.Days;
                t.ForecastConfidence = forecast.Confidence;
                t.ForecastMethod = forecast.Method;
                t.LowSince = LowSince(history, t);
                t.AverageLifetimeDays = AverageLifetime(history);
                t.NeedsOrder = t.NeedsAttention
                               || (forecast.Days is int d && d <= leadTimeDays && forecast.Confidence >= ForecastConfidence.Medium);
            }
        }

        public readonly record struct ForecastResult(int? Days, ForecastConfidence Confidence, string? Method);

        /// <summary>
        /// Prévision sur le consommable en cours (depuis son dernier changement). Par les pages : consommation en % par
        /// page (régression du niveau sur le compteur), puis volume moyen par jour sur 30 jours ; sinon dans le temps.
        /// Rien sans une baisse d'au moins 3 points sur au moins 3 relevés.
        /// </summary>
        public static ForecastResult Forecast(IReadOnlyList<SupplyHistory> history, TonerLevelResult current, long currentPages, double? pagesPerDay, DateTime now)
        {
            if (!current.HasLevel) return new(null, ForecastConfidence.None, null);

            int start = 0;
            for (int i = 0; i < history.Count; i++) if (history[i].IsReplacement) start = i;
            var points = history.Skip(start).Where(h => h.Level >= 0).Select(h => (h.RecordedAt, Level: (double)h.Level, h.PageCounter)).ToList();
            // Relevé courant (s'il n'a pas déjà été écrit) : le temps écoulé à niveau constant compte aussi
            if (points.Count == 0 || points[^1].RecordedAt < now.AddMinutes(-1))
                points.Add((now, current.CurrentLevel, currentPages));

            if (points.Count < 3) return new(null, ForecastConfidence.None, null);
            double drop = points.Max(p => p.Level) - current.CurrentLevel;
            if (drop < 3) return new(null, ForecastConfidence.None, null);

            double spanDays = (points[^1].RecordedAt - points[0].RecordedAt).TotalDays;
            var confidence = drop >= 10 && spanDays >= 14 ? ForecastConfidence.Good
                           : drop >= 5 && spanDays >= 5 ? ForecastConfidence.Medium
                           : ForecastConfidence.Low;

            // 1. Par les pages
            var paged = points.Where(p => p.PageCounter > 0).ToList();
            if (paged.Count >= 3 && pagesPerDay is double ppd && ppd > 0)
            {
                double slope = Slope(paged.Select(p => ((double)p.PageCounter, p.Level)).ToList()); // % par page
                if (slope < 0)
                {
                    double remainingPages = current.CurrentLevel / -slope;
                    return new(ClampDays(remainingPages / ppd), confidence, "pages");
                }
            }

            // 2. Dans le temps
            var t0 = points[0].RecordedAt;
            double perDay = Slope(points.Select(p => ((p.RecordedAt - t0).TotalDays, p.Level)).ToList());
            if (perDay < 0) return new(ClampDays(current.CurrentLevel / -perDay), confidence, "temps");

            return new(null, ForecastConfidence.None, null);
        }

        /// <summary>Volume moyen sur les 30 derniers jours (au moins 2 jours de relevés), null si inconnu.</summary>
        public static double? PagesPerDay(IReadOnlyList<(DateTime At, long Pages)> series, DateTime now)
        {
            var recent = series.Where(p => p.At >= now.AddDays(-30)).OrderBy(p => p.At).ToList();
            if (recent.Count < 2) return null;
            double days = (recent[^1].At - recent[0].At).TotalDays;
            long delta = recent[^1].Pages - recent[0].Pages;
            if (days < 2 || delta < 0) return null; // compteur remis à zéro : pas d'estimation
            return delta / days;
        }

        private static DateTime? LowSince(IReadOnlyList<SupplyHistory> history, TonerLevelResult current)
        {
            if (!current.NeedsAttention) return null;
            DateTime? since = null;
            for (int i = history.Count - 1; i >= 0; i--)
            {
                if (history[i].State is SupplyState.Low or SupplyState.Empty) since = history[i].RecordedAt;
                else break;
            }
            return since;
        }

        private static int? AverageLifetime(IReadOnlyList<SupplyHistory> history)
        {
            var dates = history.Where(h => h.IsReplacement).Select(h => h.RecordedAt).OrderBy(d => d).ToList();
            if (dates.Count < 2) return null;
            return (int)Math.Round(dates.Zip(dates.Skip(1), (a, b) => (b - a).TotalDays).Average());
        }

        private static double Slope(IReadOnlyList<(double X, double Y)> points)
        {
            int n = points.Count;
            double sx = points.Sum(p => p.X), sy = points.Sum(p => p.Y);
            double sxy = points.Sum(p => p.X * p.Y), sxx = points.Sum(p => p.X * p.X);
            double den = n * sxx - sx * sx;
            return Math.Abs(den) < 1e-9 ? 0 : (n * sxy - sx * sy) / den;
        }

        private static int ClampDays(double days) => (int)Math.Clamp(Math.Round(days), 0, 3650);

        private static string Truncate(string s, int max) => s.Length > max ? s[..max] : s;
    }
}
