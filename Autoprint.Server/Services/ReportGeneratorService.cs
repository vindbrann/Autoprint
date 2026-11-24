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

        private readonly SecretProtector _secrets;

        public ReportGeneratorService(ApplicationDbContext context, ISnmpService snmpService, SecretProtector secrets)
        {
            _secrets = secrets;
            _context = context;
            _snmpService = snmpService;
        }

        private async Task<List<PrinterReportItem>> FetchReportDataAsync(ReportSchedule schedule)
        {
            var query = _context.Imprimantes
                .Include(i => i.Emplacement)
                .Include(i => i.Modele).ThenInclude(m => m!.Marque)
                .Include(i => i.Modele).ThenInclude(m => m!.SnmpProfile).ThenInclude(p => p!.Items)
                .Include(i => i.Modele).ThenInclude(m => m!.SnmpProfile).ThenInclude(p => p!.Overrides)
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
            var lowThreshold = await MonitoringSettings.GetLowThresholdAsync(_context);

            // #51 : historique des consommables des imprimantes du rapport, en une requête
            var histories = await SupplyHistoryService.LoadAsync(_context, printerIds);
            var globalLeadTime = await MonitoringSettings.GetLeadTimeAsync(_context);

            bool dbUpdated = false;

            // Run SNMP diagnostic query in parallel for all selected printers
            var tasks = printers.Select(async printer =>
            {
                var printerHistory = histories.GetValueOrDefault(printer.Id) ?? new List<SupplyHistory>();
                try
                {
                    var diagnostic = await _snmpService.GetPrinterDiagnosticAsync(
                        printer.AdresseIp,
                        printer.SnmpPort,
                        SnmpCredentials.Resolve(printer, snmpProfiles, _secrets),
                        printer.Modele?.SnmpProfile,
                        lowThreshold
                    );

                    var toners = diagnostic.Toners ?? new List<TonerLevelResult>();
                    if (diagnostic.PingSuccess)
                        SupplyHistoryService.Enrich(diagnostic, printerHistory, MonitoringSettings.EffectiveLeadTime(printer.Modele?.SnmpProfile, globalLeadTime));
                    var since90 = DateTime.UtcNow.AddDays(-90);
                    var lifetimes = toners.Where(t => t.AverageLifetimeDays.HasValue).Select(t => t.AverageLifetimeDays!.Value).ToList();

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
                        SerialNumber = resolvedSn,
                        Replacements90 = printerHistory.Count(h => h.IsReplacement && h.RecordedAt >= since90),
                        AverageLifetimeDays = lifetimes.Count > 0 ? (int)Math.Round(lifetimes.Average()) : null
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
                        Alerts = new List<PrinterAlert> { new(AlertSeverity.Warning, "Impossible d'interroger la machine en SNMP.") },
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
            if (metrics.Contains("ToOrder")) headers.Add("Consommables à commander");
            if (metrics.Contains("Replacements")) headers.Add("Changements de consommables (90 j)");

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
                        var tonerParts = item.Toners.Select(t => $"{t.Color}: {SupplyText(t)}");
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
                        var trayParts = item.Trays.Select(t => $"{t.Name}: {TrayText(t)}");
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
                    row.Add(item.Alerts.Any() ? string.Join(" , ", item.Alerts.Select(a => a.Text)) : "Aucune panne");
                }
                if (metrics.Contains("Predictions"))
                {
                    if (item.PingSuccess && item.Toners.Any())
                    {
                        var predictionParts = item.Toners.Select(t =>
                        {
                            return $"{t.Color}: {ForecastText(t)}";
                        });
                        row.Add(string.Join(" | ", predictionParts));
                    }
                    else
                    {
                        row.Add(item.PingSuccess ? "Aucune donnée de prévision" : "Hors ligne");
                    }
                }
                if (metrics.Contains("ToOrder"))
                {
                    var toOrder = item.Toners.Where(t => t.NeedsOrder).Select(t => $"{t.Color} ({SupplyText(t)})").ToList();
                    row.Add(!item.PingSuccess ? "Hors ligne" : toOrder.Count > 0 ? string.Join(" | ", toOrder) : "Rien à commander");
                }
                if (metrics.Contains("Replacements"))
                {
                    row.Add(item.Replacements90 == 0 ? "Aucun" : $"{item.Replacements90}{(item.AverageLifetimeDays is int life ? $" (durée moyenne {life} j)" : "")}");
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
                if (metrics.Contains("ToOrder")) totalRow.Add($"{data.Sum(d => d.Toners.Count(t => t.NeedsOrder))} à commander");
                if (metrics.Contains("Replacements")) totalRow.Add($"{data.Sum(d => d.Replacements90)} changement(s)");
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
            // Libellé sur deux lignes au plus, dans la largeur du pavé (campagne SNMP : libellés qui débordaient)
            void DrawTiles(IReadOnlyList<(string Label, string Value, SKColor Color)> tiles)
            {
                const float gap = 8f, labelSize = 7.5f, lineHeight = 9f;
                float width = (right - left - gap * (tiles.Count - 1)) / tiles.Count;
                var labels = tiles.Select(t => pdf.Wrap(t.Label, width - 20, labelSize)).ToList();
                for (int i = 0; i < labels.Count; i++)
                    if (labels[i].Count > 2) labels[i] = new List<string> { labels[i][0], pdf.Fit(string.Join(" ", labels[i].Skip(1)), width - 20, labelSize) };
                int labelLines = Math.Max(1, labels.Max(l => l.Count));
                float height = 33f + lineHeight * labelLines;
                for (int i = 0; i < tiles.Count; i++)
                {
                    float x = left + i * (width + gap);
                    pdf.FillRoundRect(x, summaryY, width, height, 6, surfaceRaised);
                    for (int l = 0; l < labels[i].Count; l++)
                        pdf.Text(labels[i][l], x + 10, summaryY + 15 + l * lineHeight, labelSize, textMuted);
                    pdf.Text(pdf.Fit(tiles[i].Value, width - 20, 15f, bold: true), x + 10, summaryY + height - 9, 15f, tiles[i].Color, bold: true);
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
                extra.Add(("Pages imprimées (parc)", $"{totalPages:N0}", primary));
            }
            if (metrics.Contains("Predictions"))
            {
                int predictionThreshold = schedule.PredictionThresholdDays;
                int nearExhaustionCount = data.Count(d => d.PingSuccess && d.Toners.Any(t => t.NeedsOrder || (t.EstimatedDaysRemaining.HasValue && t.EstimatedDaysRemaining.Value >= 0 && t.EstimatedDaysRemaining.Value <= predictionThreshold)));
                extra.Add(($"Imprimantes à approvisionner (sous {predictionThreshold} j)", nearExhaustionCount.ToString("N0"), nearExhaustionCount > 0 ? danger : text));
            }
            if (metrics.Contains("ToOrder"))
            {
                int toOrder = data.Sum(d => d.Toners.Count(t => t.NeedsOrder));
                extra.Add(("Consommables à commander", toOrder.ToString("N0"), toOrder > 0 ? danger : text));
            }
            if (metrics.Contains("Replacements"))
            {
                extra.Add(("Changements de consommables (90 j)", data.Sum(d => d.Replacements90).ToString("N0"), primary));
            }
            if (extra.Count > 0) DrawTiles(extra);
            summaryY += 4;

            // Tableau : une ligne d'identification par imprimante, puis le détail sur toute la largeur, avec retour à la
            // ligne (campagne SNMP : deux colonnes de mesures abrégées ne suffisaient plus — consommables, bacs, alertes,
            // prévision et commandes y étaient illisibles ou absents)
            const float colName = 20, colSerial = 150, colIp = 240, colLocation = 320, colStatus = 440;
            const float detailSize = 7.5f, detailLine = 10f, indent = colName + 8;
            bool showCounter = metrics.Contains("Pages");

            float DrawTableHeader(float top)
            {
                float baseline = top + 12;
                pdf.Text("Imprimante", colName, baseline, sizeText, textSecondary, bold: true);
                pdf.Text("N° série", colSerial, baseline, sizeText, textSecondary, bold: true);
                pdf.Text("Adresse IP", colIp, baseline, sizeText, textSecondary, bold: true);
                pdf.Text("Lieu", colLocation, baseline, sizeText, textSecondary, bold: true);
                pdf.Text("Statut", colStatus, baseline, sizeText, textSecondary, bold: true);
                if (showCounter) pdf.TextRight("Compteur", right, baseline, sizeText, textSecondary, bold: true);
                float bottom = baseline + 6;
                pdf.Line(left, bottom, right, bottom, borderStrong);
                return bottom;
            }

            // Lignes de détail d'une imprimante : (intitulé, texte, couleur)
            List<(string Label, string Text, SKColor Color)> Details(PrinterReportItem item)
            {
                var d = new List<(string, string, SKColor)>();
                if (!item.PingSuccess) { d.Add(("Hors ligne", "aucune valeur relevée", danger)); return d; }
                if (metrics.Contains("Toner"))
                {
                    string Supply(TonerLevelResult t)
                    {
                        var s = $"{t.Color} {SupplyText(t)}";
                        if (metrics.Contains("Predictions") && t.EstimatedDaysRemaining is int days) s += $", ≈ {days} j";
                        if (t.NeedsOrder) s += ", à commander";
                        return s;
                    }
                    d.Add(("Consommables", item.Toners.Any() ? string.Join("  ·  ", item.Toners.Select(Supply)) : "aucun relevé", black));
                }
                if (metrics.Contains("Trays") && item.Trays.Any())
                    d.Add(("Bacs", string.Join("  ·  ", item.Trays.Select(t => $"{t.Name} {TrayText(t)}")), black));
                if (metrics.Contains("Alerts") && item.Alerts.Any())
                    d.Add(("Alertes", string.Join("  ·  ", item.Alerts.Select(a => a.Text)), warning));
                if (metrics.Contains("ToOrder") && item.Toners.Any(t => t.NeedsOrder))
                    d.Add(("À commander", string.Join(", ", item.Toners.Where(t => t.NeedsOrder).Select(t => t.Color)), danger));
                if (metrics.Contains("Replacements") && item.Replacements90 > 0)
                    d.Add(("Changements (90 j)", $"{item.Replacements90}{(item.AverageLifetimeDays is int life ? $", durée moyenne {life} j" : "")}", black));
                return d;
            }

            float y = DrawTableHeader(summaryY + 10);

            foreach (var item in data)
            {
                // Mise en forme du détail avant dessin, pour garder une imprimante entière sur une page
                var blocks = new List<(string Label, List<string> Lines, SKColor Color, float LabelWidth)>();
                foreach (var (label, value, color) in Details(item))
                {
                    var lbl = label + " : ";
                    float lw = pdf.MeasureText(lbl, detailSize, bold: true);
                    blocks.Add((lbl, pdf.Wrap(value, right - indent - lw, detailSize), color, lw));
                }
                float blockHeight = 16 + blocks.Sum(b => Math.Max(1, b.Lines.Count) * detailLine) + 6;
                if (y + blockHeight > PdfReportWriter.PageHeight - 46)
                {
                    DrawFooter();
                    pdf.NewPage();
                    y = DrawTableHeader(30);
                }

                y += 16;
                pdf.Text(pdf.Fit(item.Printer.NomAffiche, colSerial - colName - 6, sizeText, bold: true), colName, y, sizeText, black, bold: true);
                string snDisplay = !string.IsNullOrWhiteSpace(item.SerialNumber) ? item.SerialNumber
                    : (!string.IsNullOrWhiteSpace(item.Printer.SerialNumber) ? item.Printer.SerialNumber : "-");
                pdf.Text(pdf.Fit(snDisplay, colIp - colSerial - 6, sizeText), colSerial, y, sizeText, slate);
                pdf.Text(pdf.Fit(item.Printer.AdresseIp, colLocation - colIp - 6, sizeText), colIp, y, sizeText, black);
                pdf.Text(pdf.Fit(item.Printer.Emplacement?.Nom ?? "Non défini", colStatus - colLocation - 6, sizeText), colLocation, y, sizeText, black);
                pdf.Text(item.PingSuccess ? "En ligne" : "Hors ligne", colStatus, y, sizeText, item.PingSuccess ? success : danger, bold: true);
                if (showCounter) pdf.TextRight(item.PingSuccess ? item.PageCounter.ToString("N0") : "-", right, y, sizeText, black);

                foreach (var b in blocks)
                {
                    y += detailLine;
                    pdf.Text(b.Label, indent, y, detailSize, textSecondary, bold: true);
                    for (int l = 0; l < b.Lines.Count; l++)
                    {
                        if (l > 0) y += detailLine;
                        pdf.Text(b.Lines[l], indent + b.LabelWidth, y, detailSize, b.Color);
                    }
                }
                y += 6;
                pdf.Line(left, y, right, y, border, 0.5f);
            }
            if (data.Count == 0) pdf.Text("Aucune imprimante dans le périmètre de ce rapport.", left, y + 20, sizeText, textMuted);
            DrawFooter();
            return pdf.ToArray();
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
            // #52 : changements de consommables sur les 90 derniers jours et durée moyenne observée
            public int Replacements90 { get; set; }
            public int? AverageLifetimeDays { get; set; }
            public Imprimante Printer { get; set; } = null!;
            public bool PingSuccess { get; set; }
            public long PageCounter { get; set; }
            public string Status { get; set; } = string.Empty;
            public string SerialNumber { get; set; } = string.Empty;
            public List<PrinterAlert> Alerts { get; set; } = new();
            public List<TonerLevelResult> Toners { get; set; } = new();
            public List<PaperTrayResult> Trays { get; set; } = new();
        }

        // #51 : jours restants (date et fiabilité), « Bas depuis » pour un consommable sans niveau, « à commander »
        private static string ForecastText(TonerLevelResult t)
        {
            string text;
            if (t.EstimatedDaysRemaining is int days)
            {
                string reliability = t.ForecastConfidence switch
                {
                    ForecastConfidence.Good => "fiable",
                    ForecastConfidence.Medium => "fiabilité moyenne",
                    _ => "peu fiable"
                };
                text = $"{days} jours (vers le {DateTime.Today.AddDays(days):dd/MM}, {reliability})";
            }
            else if (t.LowSince is DateTime since)
            {
                text = $"{(t.State == SupplyState.Empty ? (t.IsReceptacle ? "Plein" : "Vide") : (t.IsReceptacle ? "Presque plein" : "Bas"))} depuis le {since.ToLocalTime():dd/MM}";
            }
            else
            {
                text = "Pas assez de relevés";
            }
            return t.NeedsOrder ? $"À commander : {text}" : text;
        }

        // #49 : niveau quand il est connu, sinon l'état (jamais un pourcentage inventé)
        private static string SupplyText(TonerLevelResult t, bool compact = false)
        {
            string state = t.State switch
            {
                SupplyState.Ok => "OK",
                SupplyState.Low => t.IsReceptacle ? "Presque plein" : "Bas",
                SupplyState.Empty => t.IsReceptacle ? "Plein" : "Vide",
                _ => compact ? "?" : "Inconnu"
            };
            if (!t.HasLevel) return state;
            string level = compact ? $"{t.CurrentLevel}%" : $"{t.CurrentLevel} %";
            return t.State is SupplyState.Low or SupplyState.Empty ? $"{level} ({state})" : level;
        }

        private static string TrayText(PaperTrayResult t, bool compact = false)
        {
            if (t.HasLevel) return t.CurrentLevel <= 100 ? (compact ? $"{t.CurrentLevel}%" : $"{t.CurrentLevel} %") : (compact ? $"{t.CurrentLevel}f" : $"{t.CurrentLevel} f.");
            return t.State switch
            {
                SupplyState.Ok => "OK",
                SupplyState.Empty => "Vide",
                _ => compact ? "?" : "Inconnu"
            };
        }
    }
}
