using Autoprint.Server.Data;
using Autoprint.Shared;
using Autoprint.Shared.DTOs;
using Microsoft.EntityFrameworkCore;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Autoprint.Server.Services
{
    public interface IReportGeneratorService
    {
        Task<byte[]> GenerateCsvReportAsync(ReportSchedule schedule);
        Task<byte[]> GeneratePdfReportAsync(ReportSchedule schedule);
    }

    public class ReportGeneratorService : IReportGeneratorService
    {
        private readonly ApplicationDbContext _context;
        private readonly ISnmpService _snmpService;
        private readonly IPredictiveService _predictiveService;

        private readonly SecretProtector _secrets;

        public ReportGeneratorService(ApplicationDbContext context, ISnmpService snmpService, IPredictiveService predictiveService, SecretProtector secrets)
        {
            _secrets = secrets;
            _context = context;
            _snmpService = snmpService;
            _predictiveService = predictiveService;
        }

        private async Task<List<PrinterReportItem>> FetchReportDataAsync(ReportSchedule schedule)
        {
            var query = _context.Imprimantes
                .Include(i => i.Emplacement)
                .Include(i => i.Modele).ThenInclude(m => m!.Marque)
                .Include(i => i.Modele).ThenInclude(m => m!.SnmpProfile).ThenInclude(p => p!.Items)
                .Where(i => !i.IsArchived);

            try
            {
                var scope = JsonSerializer.Deserialize<ReportScopeFilter>(schedule.ScopeFilterJson);
                if (scope != null)
                {
                    if (scope.Rules != null && scope.Rules.Any())
                    {
                        foreach (var rule in scope.Rules)
                        {
                            if (rule.Field == "LocationId")
                            {
                                if (rule.Operator == "In" && rule.Values != null && rule.Values.Any())
                                {
                                    query = query.Where(i => rule.Values.Contains(i.EmplacementId));
                                }
                                else if (rule.Operator == "NotIn" && rule.Values != null && rule.Values.Any())
                                {
                                    query = query.Where(i => !rule.Values.Contains(i.EmplacementId));
                                }
                            }
                            else if (rule.Field == "BrandId")
                            {
                                if (rule.Operator == "In" && rule.Values != null && rule.Values.Any())
                                {
                                    query = query.Where(i => i.Modele != null && rule.Values.Contains(i.Modele.MarqueId));
                                }
                                else if (rule.Operator == "NotIn" && rule.Values != null && rule.Values.Any())
                                {
                                    query = query.Where(i => i.Modele == null || !rule.Values.Contains(i.Modele.MarqueId));
                                }
                            }
                            else if (rule.Field == "ModelId")
                            {
                                if (rule.Operator == "In" && rule.Values != null && rule.Values.Any())
                                {
                                    query = query.Where(i => rule.Values.Contains(i.ModeleId));
                                }
                                else if (rule.Operator == "NotIn" && rule.Values != null && rule.Values.Any())
                                {
                                    query = query.Where(i => !rule.Values.Contains(i.ModeleId));
                                }
                            }
                            else if (rule.Field == "SnmpProfileId")
                            {
                                if (rule.Operator == "In" && rule.Values != null && rule.Values.Any())
                                {
                                    query = query.Where(i => i.Modele != null && i.Modele.SnmpProfileId.HasValue && rule.Values.Contains(i.Modele.SnmpProfileId.Value));
                                }
                                else if (rule.Operator == "NotIn" && rule.Values != null && rule.Values.Any())
                                {
                                    query = query.Where(i => i.Modele == null || !i.Modele.SnmpProfileId.HasValue || !rule.Values.Contains(i.Modele.SnmpProfileId.Value));
                                }
                            }
                            else if (rule.Field == "Nom")
                            {
                                if (rule.Operator == "Contains" && !string.IsNullOrEmpty(rule.Value))
                                {
                                    query = query.Where(i => i.NomAffiche != null && i.NomAffiche.ToLower().Contains(rule.Value.ToLower()));
                                }
                                else if (rule.Operator == "Equals" && !string.IsNullOrEmpty(rule.Value))
                                {
                                    query = query.Where(i => i.NomAffiche != null && i.NomAffiche.ToLower() == rule.Value.ToLower());
                                }
                            }
                            else if (rule.Field == "AdresseIp")
                            {
                                if (rule.Operator == "Contains" && !string.IsNullOrEmpty(rule.Value))
                                {
                                    query = query.Where(i => i.AdresseIp != null && i.AdresseIp.Contains(rule.Value));
                                }
                                else if (rule.Operator == "StartsWith" && !string.IsNullOrEmpty(rule.Value))
                                {
                                    query = query.Where(i => i.AdresseIp != null && i.AdresseIp.StartsWith(rule.Value));
                                }
                            }
                            else if (rule.Field == "Code")
                            {
                                if (rule.Operator == "Contains" && !string.IsNullOrEmpty(rule.Value))
                                {
                                    query = query.Where(i => i.Code != null && i.Code.ToLower().Contains(rule.Value.ToLower()));
                                }
                                else if (rule.Operator == "Equals" && !string.IsNullOrEmpty(rule.Value))
                                {
                                    query = query.Where(i => i.Code != null && i.Code.ToLower() == rule.Value.ToLower());
                                }
                            }
                        }
                    }
                    else
                    {
                        // Fallback to legacy singular format
                        if (scope.BrandId > 0)
                        {
                            query = query.Where(i => i.Modele != null && i.Modele.MarqueId == scope.BrandId);
                        }
                        if (scope.ModelId > 0)
                        {
                            query = query.Where(i => i.ModeleId == scope.ModelId);
                        }
                        if (scope.LocationId > 0)
                        {
                            query = query.Where(i => i.EmplacementId == scope.LocationId);
                        }
                    }
                }
            }
            catch { }

            var printers = await query.ToListAsync();
            var printerIds = printers.Select(p => p.Id).ToList();
            var snmpProfiles = await SnmpCredentials.LoadProfilesAsync(_context);

            // Load toner history for all target printers in a single batch query
            var histories = await _context.TonerHistories
                .Where(h => printerIds.Contains(h.ImprimanteId))
                .ToListAsync();

            bool dbUpdated = false;

            // Run SNMP diagnostic query in parallel for all selected printers
            var tasks = printers.Select(async printer =>
            {
                var printerHistory = histories.Where(h => h.ImprimanteId == printer.Id).ToList();
                try
                {
                    var diagnostic = await _snmpService.GetPrinterDiagnosticAsync(
                        printer.AdresseIp,
                        printer.SnmpPort,
                        SnmpCredentials.Resolve(printer, snmpProfiles, _secrets),
                        printer.Modele?.SnmpProfile
                    );

                    var toners = diagnostic.Toners ?? new List<TonerLevelResult>();
                    foreach (var toner in toners)
                    {
                        var tonerHistory = printerHistory
                            .Where(h => h.ComponentColor.Equals(toner.Color, StringComparison.OrdinalIgnoreCase))
                            .ToList();
                        toner.EstimatedDaysRemaining = _predictiveService.PredictDaysRemaining(tonerHistory);
                    }

                    string resolvedSn = !string.IsNullOrWhiteSpace(diagnostic.SerialNumber)
                        ? diagnostic.SerialNumber.Trim()
                        : (printer.SerialNumber ?? "");

                    // Auto-remplissage du numéro de série en base si découvert en direct
                    if (!string.IsNullOrWhiteSpace(diagnostic.SerialNumber) && string.IsNullOrWhiteSpace(printer.SerialNumber))
                    {
                        printer.SerialNumber = diagnostic.SerialNumber.Trim();
                        dbUpdated = true;
                    }

                    return new PrinterReportItem
                    {
                        Printer = printer,
                        PingSuccess = diagnostic.PingSuccess,
                        PageCounter = diagnostic.PageCounter,
                        Status = diagnostic.Status,
                        Alerts = diagnostic.Alerts,
                        Toners = toners,
                        Trays = diagnostic.Trays ?? new List<PaperTrayResult>(),
                        SerialNumber = resolvedSn
                    };
                }
                catch
                {
                    return new PrinterReportItem
                    {
                        Printer = printer,
                        PingSuccess = false,
                        PageCounter = 0,
                        Status = "Erreur SNMP",
                        Alerts = new List<string> { "Impossible d'interroger la machine en SNMP." },
                        Toners = new List<TonerLevelResult>(),
                        Trays = new List<PaperTrayResult>(),
                        SerialNumber = printer.SerialNumber ?? ""
                    };
                }
            });

            var results = await Task.WhenAll(tasks);

            if (dbUpdated)
            {
                try
                {
                    await _context.SaveChangesAsync();
                }
                catch (Exception ex)
                {
                    // Le rapport est quand même produit ; l'historique (prévisions d'épuisement) n'est simplement pas mis à jour
                    Console.WriteLine($"[ReportGenerator] Enregistrement de l'historique des toners impossible : {ex.Message}");
                }
            }

            return results.ToList();
        }

        public async Task<byte[]> GenerateCsvReportAsync(ReportSchedule schedule)
        {
            var data = await FetchReportDataAsync(schedule);
            List<string> metrics = new();
            try { metrics = JsonSerializer.Deserialize<List<string>>(schedule.SelectedMetricsJson) ?? new(); } catch { }

            var sb = new StringBuilder();

            // Header line
            var headers = new List<string> { "Nom imprimante", "N° Série", "Adresse IP", "Emplacement", "Modèle" };
            if (metrics.Contains("Pages")) headers.Add("Compteur de Pages");
            if (metrics.Contains("Toner")) headers.Add("Niveaux de Toners");
            if (metrics.Contains("Trays")) headers.Add("Bacs Papier");
            if (metrics.Contains("Availability")) headers.Add("Disponibilité");
            if (metrics.Contains("Alerts")) headers.Add("Pannes / Alertes");
            if (metrics.Contains("Predictions")) headers.Add("Prévision d'épuisement");

            sb.AppendLine(string.Join(";", headers.Select(EscapeCsv)));

            foreach (var item in data)
            {
                string snValue = !string.IsNullOrWhiteSpace(item.SerialNumber)
                    ? item.SerialNumber
                    : (!string.IsNullOrWhiteSpace(item.Printer.SerialNumber) ? item.Printer.SerialNumber : "");

                var row = new List<string>
                {
                    item.Printer.NomAffiche,
                    snValue,
                    item.Printer.AdresseIp,
                    item.Printer.Emplacement?.Nom ?? "Non défini",
                    item.Printer.Modele?.Nom ?? "Générique"
                };

                if (metrics.Contains("Pages"))
                {
                    row.Add(item.PingSuccess ? item.PageCounter.ToString() : "Hors ligne");
                }
                if (metrics.Contains("Toner"))
                {
                    if (item.PingSuccess && item.Toners.Any())
                    {
                        var tonerParts = item.Toners.Select(t => $"{t.Color}: {(t.CurrentLevel >= 0 ? $"{t.CurrentLevel}%" : "Niveau OK")}");
                        row.Add(string.Join(" | ", tonerParts));
                    }
                    else
                    {
                        row.Add(item.PingSuccess ? "Aucun toner configuré" : "Hors ligne");
                    }
                }
                if (metrics.Contains("Trays"))
                {
                    if (item.PingSuccess && item.Trays.Any())
                    {
                        var trayParts = item.Trays.Select(t => $"{t.Name}: {(t.CurrentLevel >= 0 ? (t.CurrentLevel <= 100 ? $"{t.CurrentLevel}%" : $"{t.CurrentLevel} f.") : "Inconnu")}");
                        row.Add(string.Join(" | ", trayParts));
                    }
                    else
                    {
                        row.Add(item.PingSuccess ? "Aucun bac configuré" : "Hors ligne");
                    }
                }
                if (metrics.Contains("Availability"))
                {
                    row.Add(item.PingSuccess ? "En ligne" : "Hors ligne");
                }
                if (metrics.Contains("Alerts"))
                {
                    row.Add(item.Alerts.Any() ? string.Join(" , ", item.Alerts) : "Aucune panne");
                }
                if (metrics.Contains("Predictions"))
                {
                    if (item.PingSuccess && item.Toners.Any())
                    {
                        var predictionParts = item.Toners.Select(t =>
                        {
                            var days = t.EstimatedDaysRemaining;
                            if (days == null) return $"{t.Color}: Inconnu";
                            if (days == -99) return $"{t.Color}: Stable / Faible usage";
                            return $"{t.Color}: {days} jours";
                        });
                        row.Add(string.Join(" | ", predictionParts));
                    }
                    else
                    {
                        row.Add(item.PingSuccess ? "Aucune donnée de prévision" : "Hors ligne");
                    }
                }

                sb.AppendLine(string.Join(";", row.Select(EscapeCsv)));
            }

            // Ligne de Totalisation optionnelle en bas du tableau CSV
            if (metrics.Contains("TotalPages"))
            {
                long totalPages = data.Where(d => d.PingSuccess).Sum(d => d.PageCounter);
                var totalRow = new List<string> { "TOTAL DU PARC", "", "", "", $"{data.Count} imprimantes" };
                if (metrics.Contains("Pages")) totalRow.Add($"{totalPages:N0} pages");
                if (metrics.Contains("Toner")) totalRow.Add("");
                if (metrics.Contains("Trays")) totalRow.Add("");
                if (metrics.Contains("Availability")) totalRow.Add($"{data.Count(d => d.PingSuccess)}/{data.Count} en ligne");
                if (metrics.Contains("Alerts")) totalRow.Add($"{data.Count(d => d.Alerts.Any())} alerte(s)");
                if (metrics.Contains("Predictions")) totalRow.Add("");
                sb.AppendLine(string.Join(";", totalRow.Select(EscapeCsv)));
            }

            return Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(sb.ToString())).ToArray();
        }

        public async Task<byte[]> GeneratePdfReportAsync(ReportSchedule schedule)
        {
            var data = await FetchReportDataAsync(schedule);
            List<string> metrics = new();
            try { metrics = JsonSerializer.Deserialize<List<string>>(schedule.SelectedMetricsJson) ?? new(); } catch { }

            const float left = 20f;
            const float right = PdfReportWriter.PageWidth - 20f;
            const float sizeTitle = 18f, sizeText = 8.5f;

            // Charte graphique Autoprint (B77, mêmes valeurs que la console en thème clair)
            var primary = SKColor.Parse("#1d5fcf");
            var text = SKColor.Parse("#161a21");
            var textSecondary = SKColor.Parse("#4a5262");
            var textMuted = SKColor.Parse("#646c7c");
            var border = SKColor.Parse("#e1e4e9");
            var borderStrong = SKColor.Parse("#858d9b");
            var surfaceRaised = SKColor.Parse("#eef0f3");
            var success = SKColor.Parse("#1b7a43");
            var danger = SKColor.Parse("#c0302b");
            var warning = SKColor.Parse("#9a5200");
            var black = text;
            var slate = textSecondary;

            using var pdf = new PdfReportWriter($"Rapport Autoprint - {schedule.ReportName}");

            void DrawFooter()
            {
                pdf.Line(left, PdfReportWriter.PageHeight - 30, right, PdfReportWriter.PageHeight - 30, border);
                pdf.Text("Autoprint", left, PdfReportWriter.PageHeight - 18, 7.5f, textMuted, bold: true);
                pdf.TextRight($"Page {pdf.PageNumber}", right, PdfReportWriter.PageHeight - 18, 7.5f, textMuted);
            }

            // 1. En-tête : filet bleu, marque, titre et informations du rapport
            pdf.FillRect(left, 20, right - left, 3, primary);
            pdf.Text("Autoprint", left, 44, 10f, primary, bold: true);
            pdf.TextRight($"Généré le {DateTime.Now:dd/MM/yyyy à HH:mm}", right, 44, sizeText, textMuted);
            pdf.Text("Rapport d'activité", left, 70, sizeTitle, text, bold: true);
            pdf.Text($"{schedule.ReportName}  ·  {schedule.Frequency}", left, 87, 9.5f, textSecondary);

            int totalCount = data.Count;
            int onlineCount = data.Count(d => d.PingSuccess);
            int offlineCount = totalCount - onlineCount;
            int alertCount = data.Count(d => d.Alerts.Any());

            float summaryY = 104;

            // 2. Résumé en pavés, comme le tableau de bord de la console
            void DrawTiles(IReadOnlyList<(string Label, string Value, SKColor Color)> tiles)
            {
                const float gap = 8f, height = 42f;
                float width = (right - left - gap * (tiles.Count - 1)) / tiles.Count;
                for (int i = 0; i < tiles.Count; i++)
                {
                    float x = left + i * (width + gap);
                    pdf.FillRoundRect(x, summaryY, width, height, 6, surfaceRaised);
                    pdf.Text(tiles[i].Label, x + 10, summaryY + 15, 7.5f, textMuted);
                    pdf.Text(tiles[i].Value, x + 10, summaryY + 33, 15f, tiles[i].Color, bold: true);
                }
                summaryY += height + 10;
            }

            if (metrics.Contains("FleetSummary") || !metrics.Contains("TotalPages"))
            {
                DrawTiles(new[]
                {
                    ("Imprimantes", totalCount.ToString("N0"), text),
                    ("En ligne", onlineCount.ToString("N0"), success),
                    ("Hors ligne", offlineCount.ToString("N0"), offlineCount > 0 ? danger : text),
                    ("En alerte", alertCount.ToString("N0"), alertCount > 0 ? warning : text)
                });
            }

            var extra = new List<(string Label, string Value, SKColor Color)>();
            if (metrics.Contains("TotalPages"))
            {
                long totalPages = data.Where(d => d.PingSuccess).Sum(d => d.PageCounter);
                extra.Add(("Volume total d'impressions du parc", $"{totalPages:N0} pages", primary));
            }
            if (metrics.Contains("Predictions"))
            {
                int predictionThreshold = schedule.PredictionThresholdDays;
                int nearExhaustionCount = data.Count(d => d.PingSuccess && d.Toners.Any(t => t.EstimatedDaysRemaining.HasValue && t.EstimatedDaysRemaining.Value >= 0 && t.EstimatedDaysRemaining.Value <= predictionThreshold));
                extra.Add(($"Toners bientôt épuisés (≤ {predictionThreshold} jours)", nearExhaustionCount.ToString("N0"), nearExhaustionCount > 0 ? danger : text));
            }
            if (extra.Count > 0) DrawTiles(extra);
            summaryY += 4;

            // Positions des colonnes (page de 595 points, marges de 20)
            const float colName = 20, colSerial = 125, colIp = 210, colLocation = 290, colStatus = 370, colMetric1 = 430, colMetric2 = 505;

            // Mesures affichées dans les deux dernières colonnes : toujours dans cet ordre, pour les intitulés
            // comme pour les valeurs (avant : intitulés dans l'ordre de sélection, valeurs dans l'ordre fixe → colonnes inversées)
            var columnMetrics = new[] { "Pages", "Toner", "Trays", "Availability", "Alerts", "Predictions" }
                .Where(metrics.Contains).Take(2).ToList();

            // En-tête du tableau, répété en haut de chaque page (le trait gris est au-dessus des intitulés : B36)
            float DrawTableHeader(float top)
            {
                float baseline = top + 12;
                pdf.Text("Imprimante", colName, baseline, sizeText, textSecondary, bold: true);
                pdf.Text("N° série", colSerial, baseline, sizeText, textSecondary, bold: true);
                pdf.Text("Adresse IP", colIp, baseline, sizeText, textSecondary, bold: true);
                pdf.Text("Lieu", colLocation, baseline, sizeText, textSecondary, bold: true);
                pdf.Text("Statut", colStatus, baseline, sizeText, textSecondary, bold: true);
                if (columnMetrics.Count > 0) pdf.Text(MapMetricHeader(columnMetrics[0]), colMetric1, baseline, sizeText, textSecondary, bold: true);
                if (columnMetrics.Count > 1) pdf.Text(MapMetricHeader(columnMetrics[1]), colMetric2, baseline, sizeText, textSecondary, bold: true);
                float bottom = baseline + 6;
                pdf.Line(left, bottom, right, bottom, borderStrong);
                return bottom;
            }

            // 3. En-tête du tableau
            float y = DrawTableHeader(summaryY + 10);

            // 4. Lignes
            for (int index = 0; index < data.Count; index++)
            {
                var item = data[index];
                y += 16;
                if (y > PdfReportWriter.PageHeight - 46)
                {
                    DrawFooter();
                    pdf.NewPage();
                    y = DrawTableHeader(30) + 16;
                }

                // Séparateur fin entre les lignes (charte : pas de fond alterné)
                pdf.Line(left, y + 5, right, y + 5, border, 0.5f);

                string printName = item.Printer.NomAffiche.Length > 16 ? item.Printer.NomAffiche.Substring(0, 14) + ".." : item.Printer.NomAffiche;
                pdf.Text(printName, colName, y, sizeText, black);

                string snDisplay = !string.IsNullOrWhiteSpace(item.SerialNumber)
                    ? item.SerialNumber
                    : (!string.IsNullOrWhiteSpace(item.Printer.SerialNumber) ? item.Printer.SerialNumber : "-");
                if (snDisplay.Length > 13) snDisplay = snDisplay.Substring(0, 11) + "..";
                pdf.Text(snDisplay, colSerial, y, sizeText, slate);

                pdf.Text(item.Printer.AdresseIp, colIp, y, sizeText, black);

                string locName = item.Printer.Emplacement?.Nom ?? "Non défini";
                if (locName.Length > 12) locName = locName.Substring(0, 10) + "..";
                pdf.Text(locName, colLocation, y, sizeText, black);

                pdf.Text(item.PingSuccess ? "En ligne" : "Hors ligne", colStatus, y, sizeText, item.PingSuccess ? success : danger, bold: true);

                // Deux premières mesures choisies, dans l'ordre
                var values = new List<string>();
                if (metrics.Contains("Pages"))
                    values.Add(item.PingSuccess ? item.PageCounter.ToString("N0") : "-");
                if (metrics.Contains("Toner"))
                    values.Add(item.PingSuccess && item.Toners.Any()
                        ? string.Join(" ", item.Toners.Select(t => $"{t.Color.Substring(0, 1)}:{(t.CurrentLevel >= 0 ? $"{t.CurrentLevel}%" : "OK")}"))
                        : "-");
                if (metrics.Contains("Trays"))
                    values.Add(item.PingSuccess && item.Trays.Any()
                        ? string.Join(" ", item.Trays.Select(t => $"{t.Name.Replace("Bac", "B").Trim()}:{(t.CurrentLevel >= 0 ? (t.CurrentLevel <= 100 ? $"{t.CurrentLevel}%" : $"{t.CurrentLevel}f") : "?")}"))
                        : "-");
                if (metrics.Contains("Availability"))
                    values.Add(item.PingSuccess ? "100%" : "0%");
                if (metrics.Contains("Alerts"))
                    values.Add(item.Alerts.Any() ? $"{item.Alerts.Count} alerte(s)" : "Aucune");
                if (metrics.Contains("Predictions"))
                {
                    string val = "-";
                    if (item.PingSuccess && item.Toners.Any())
                    {
                        var list = item.Toners
                            .Where(t => t.EstimatedDaysRemaining.HasValue)
                            .Select(t =>
                            {
                                var days = t.EstimatedDaysRemaining!.Value;
                                return days == -99 ? $"{t.Color.Substring(0, 1)}:Stab" : $"{t.Color.Substring(0, 1)}:{days}j";
                            })
                            .ToList();
                        if (list.Count > 0) val = string.Join(" ", list);
                    }
                    values.Add(val);
                }
                if (values.Count > 0) pdf.Text(values[0], colMetric1, y, sizeText, black);
                if (values.Count > 1) pdf.Text(values[1], colMetric2, y, sizeText, black);
            }

            if (data.Count == 0) pdf.Text("Aucune imprimante dans le périmètre de ce rapport.", left, y + 20, sizeText, textMuted);
            DrawFooter();
            return pdf.ToArray();
        }

        private string MapMetricHeader(string metric)
        {
            return metric switch
            {
                "Pages" => "Compteur",
                "Toner" => "Toners",
                "Trays" => "Bacs papier",
                "Availability" => "Dispo",
                "Alerts" => "Pannes",
                "Predictions" => "Prévision",
                _ => ""
            };
        }

        private string EscapeCsv(string? field)
        {
            if (string.IsNullOrEmpty(field)) return string.Empty;

            // Protection contre l'injection de formules CSV (CWE-1236)
            if (field.StartsWith("=") || field.StartsWith("+") || field.StartsWith("-") || field.StartsWith("@") || field.StartsWith("\t") || field.StartsWith("\r"))
            {
                field = "'" + field;
            }

            if (field.Contains(";") || field.Contains("\"") || field.Contains("\n") || field.Contains("\r"))
            {
                return "\"" + field.Replace("\"", "\"\"") + "\"";
            }
            return field;
        }

        private class ReportScopeFilter
        {
            public int BrandId { get; set; }
            public int ModelId { get; set; }
            public int LocationId { get; set; }

            public List<ReportRule>? Rules { get; set; }
        }

        private class ReportRule
        {
            public string Field { get; set; } = string.Empty;
            public string Operator { get; set; } = string.Empty;
            public List<int>? Values { get; set; }
            public string? Value { get; set; }
        }

        private class PrinterReportItem
        {
            public Imprimante Printer { get; set; } = null!;
            public bool PingSuccess { get; set; }
            public long PageCounter { get; set; }
            public string Status { get; set; } = string.Empty;
            public string SerialNumber { get; set; } = string.Empty;
            public List<string> Alerts { get; set; } = new();
            public List<TonerLevelResult> Toners { get; set; } = new();
            public List<PaperTrayResult> Trays { get; set; } = new();
        }
    }
}
