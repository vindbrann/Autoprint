using Autoprint.Server.Data;
using Autoprint.Shared;
using Microsoft.EntityFrameworkCore;
using PdfSharpCore.Drawing;
using PdfSharpCore.Pdf;
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

        public ReportGeneratorService(ApplicationDbContext context, ISnmpService snmpService, IPredictiveService predictiveService)
        {
            _context = context;
            _snmpService = snmpService;
            _predictiveService = predictiveService;
        }

        private async Task<List<PrinterReportItem>> FetchReportDataAsync(ReportSchedule schedule)
        {
            var query = _context.Imprimantes
                .Include(i => i.Emplacement)
                .Include(i => i.Modele).ThenInclude(m => m!.Marque)
                .Include(i => i.Modele).ThenInclude(m => m!.SnmpProfile)
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

            // Load toner history for all target printers in a single batch query
            var histories = await _context.TonerHistories
                .Where(h => printerIds.Contains(h.ImprimanteId))
                .ToListAsync();

            // Run SNMP diagnostic query in parallel for all selected printers
            var tasks = printers.Select(async printer =>
            {
                var printerHistory = histories.Where(h => h.ImprimanteId == printer.Id).ToList();
                try
                {
                    var diagnostic = await _snmpService.GetPrinterDiagnosticAsync(
                        printer.AdresseIp,
                        printer.SnmpPort,
                        printer.SnmpCommunity ?? "public",
                        printer.SnmpVersion,
                        printer.Modele?.SnmpProfile
                    );

                    var toners = diagnostic.Toners ?? new List<Autoprint.Shared.DTOs.TonerLevelResult>();
                    foreach (var toner in toners)
                    {
                        var tonerHistory = printerHistory
                            .Where(h => h.ComponentColor.Equals(toner.Color, StringComparison.OrdinalIgnoreCase))
                            .ToList();
                        toner.EstimatedDaysRemaining = _predictiveService.PredictDaysRemaining(tonerHistory);
                    }

                    return new PrinterReportItem
                    {
                        Printer = printer,
                        PingSuccess = diagnostic.PingSuccess,
                        PageCounter = diagnostic.PageCounter,
                        Status = diagnostic.Status,
                        Alerts = diagnostic.Alerts,
                        Toners = toners
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
                        Toners = new List<Autoprint.Shared.DTOs.TonerLevelResult>()
                    };
                }
            });

            var results = await Task.WhenAll(tasks);
            return results.ToList();
        }

        public async Task<byte[]> GenerateCsvReportAsync(ReportSchedule schedule)
        {
            var data = await FetchReportDataAsync(schedule);
            List<string> metrics = new();
            try { metrics = JsonSerializer.Deserialize<List<string>>(schedule.SelectedMetricsJson) ?? new(); } catch { }

            var sb = new StringBuilder();

            // Header line
            var headers = new List<string> { "Nom imprimante", "Adresse IP", "Emplacement", "Modèle" };
            if (metrics.Contains("Pages")) headers.Add("Compteur de Pages");
            if (metrics.Contains("Toner")) headers.Add("Niveaux de Toners");
            if (metrics.Contains("Availability")) headers.Add("Disponibilité");
            if (metrics.Contains("Alerts")) headers.Add("Pannes / Alertes");
            if (metrics.Contains("Predictions")) headers.Add("Prévision d'épuisement");

            sb.AppendLine(string.Join(";", headers.Select(EscapeCsv)));

            foreach (var item in data)
            {
                var row = new List<string>
                {
                    item.Printer.NomAffiche,
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
                        var tonerParts = item.Toners.Select(t => $"{t.Color}: {t.CurrentLevel}%");
                        row.Add(string.Join(" | ", tonerParts));
                    }
                    else
                    {
                        row.Add(item.PingSuccess ? "Aucun toner détecté" : "Hors ligne");
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

            return Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(sb.ToString())).ToArray();
        }

        public async Task<byte[]> GeneratePdfReportAsync(ReportSchedule schedule)
        {
            var data = await FetchReportDataAsync(schedule);
            List<string> metrics = new();
            try { metrics = JsonSerializer.Deserialize<List<string>>(schedule.SelectedMetricsJson) ?? new(); } catch { }

            using (var document = new PdfDocument())
            {
                document.Info.Title = $"Rapport Autoprint - {schedule.ReportName}";
                var page = document.AddPage();
                var gfx = XGraphics.FromPdfPage(page);

                // Fonts
                var fontTitle = new XFont("Arial", 18, XFontStyle.Bold);
                var fontSubtitle = new XFont("Arial", 11, XFontStyle.Bold);
                var fontRegular = new XFont("Arial", 9, XFontStyle.Regular);
                var fontBold = new XFont("Arial", 9, XFontStyle.Bold);

                // 1. Draw Title Header Band
                var headerRect = new XRect(20, 20, page.Width - 40, 50);
                var headerBrush = new XSolidBrush(XColor.FromArgb(13, 110, 253)); // Blue bootstrap primary
                gfx.DrawRectangle(headerBrush, headerRect);

                gfx.DrawString("🖨️ AUTOPRINT - RAPPORT D'ACTIVITÉ", fontTitle, XBrushes.White, new XRect(30, 30, page.Width - 60, 30), XStringFormats.TopLeft);

                // 2. Draw Report Summary Card
                gfx.DrawString($"Rapport : {schedule.ReportName}", fontSubtitle, XBrushes.Black, 20, 95);
                gfx.DrawString($"Généré le : {DateTime.Now.ToString("dd/MM/yyyy HH:mm")}", fontRegular, XBrushes.Gray, 20, 115);
                gfx.DrawString($"Fréquence : {schedule.Frequency}", fontRegular, XBrushes.Gray, 20, 130);

                int totalCount = data.Count;
                int onlineCount = data.Count(d => d.PingSuccess);
                int offlineCount = totalCount - onlineCount;
                int alertCount = data.Count(d => d.Alerts.Any());
                int predictionThreshold = schedule.PredictionThresholdDays;
                int nearExhaustionCount = 0;
                if (metrics.Contains("Predictions"))
                {
                    nearExhaustionCount = data.Count(d => d.PingSuccess && d.Toners.Any(t => t.EstimatedDaysRemaining.HasValue && t.EstimatedDaysRemaining.Value >= 0 && t.EstimatedDaysRemaining.Value <= predictionThreshold));
                }

                string summaryText = $"Total Imprimantes : {totalCount}  |  En ligne : {onlineCount}  |  Hors ligne : {offlineCount}  |  En alerte : {alertCount}";
                if (metrics.Contains("Predictions"))
                {
                    summaryText += $"  |  Épuisement proche (<={predictionThreshold}j) : {nearExhaustionCount}";
                }
                gfx.DrawString(summaryText, fontBold, XBrushes.DarkSlateGray, 20, 155);

                // 3. Draw Table Headers
                int y = 180;
                gfx.DrawLine(XPens.DarkGray, 20, y, page.Width - 20, y);
                y += 5;

                // Column X positions
                int colName = 20;
                int colIp = 120;
                int colLocation = 210;
                int colStatus = 290;
                int colMetric1 = 370;
                int colMetric2 = 480;

                gfx.DrawString("Nom Imprimante", fontBold, XBrushes.Black, colName, y);
                gfx.DrawString("Adresse IP", fontBold, XBrushes.Black, colIp, y);
                gfx.DrawString("Emplacement", fontBold, XBrushes.Black, colLocation, y);
                gfx.DrawString("Statut", fontBold, XBrushes.Black, colStatus, y);

                // Metric Headers
                string mHeader1 = "";
                string mHeader2 = "";

                var activeMetrics = metrics.Where(m => m == "Pages" || m == "Toner" || m == "Availability" || m == "Alerts" || m == "Predictions").ToList();
                if (activeMetrics.Count > 0) mHeader1 = MapMetricHeader(activeMetrics[0]);
                if (activeMetrics.Count > 1) mHeader2 = MapMetricHeader(activeMetrics[1]);

                if (!string.IsNullOrEmpty(mHeader1)) gfx.DrawString(mHeader1, fontBold, XBrushes.Black, colMetric1, y);
                if (!string.IsNullOrEmpty(mHeader2)) gfx.DrawString(mHeader2, fontBold, XBrushes.Black, colMetric2, y);

                y += 12;
                gfx.DrawLine(XPens.Black, 20, y, page.Width - 20, y);

                // 4. Draw Rows
                foreach (var item in data)
                {
                    y += 15;
                    // If near end of page, add a page
                    if (y > page.Height - 40)
                    {
                        page = document.AddPage();
                        gfx = XGraphics.FromPdfPage(page);
                        y = 40;
                        gfx.DrawLine(XPens.Black, 20, y, page.Width - 20, y);
                        y += 15;
                    }

                    // Alternating background
                    var bgRect = new XRect(20, y - 11, page.Width - 40, 15);
                    if (data.IndexOf(item) % 2 == 1)
                    {
                        gfx.DrawRectangle(new XSolidBrush(XColor.FromArgb(245, 245, 245)), bgRect);
                    }

                    // Print printer base details
                    string printName = item.Printer.NomAffiche.Length > 18 ? item.Printer.NomAffiche.Substring(0, 16) + ".." : item.Printer.NomAffiche;
                    gfx.DrawString(printName, fontRegular, XBrushes.Black, colName, y);
                    gfx.DrawString(item.Printer.AdresseIp, fontRegular, XBrushes.Black, colIp, y);
                    
                    string locName = item.Printer.Emplacement?.Nom ?? "Non défini";
                    if (locName.Length > 13) locName = locName.Substring(0, 11) + "..";
                    gfx.DrawString(locName, fontRegular, XBrushes.Black, colLocation, y);

                    // Status display
                    var statusColor = item.PingSuccess ? XBrushes.Green : XBrushes.Red;
                    gfx.DrawString(item.PingSuccess ? "En ligne" : "Hors ligne", fontBold, statusColor, colStatus, y);

                    // Print selected metrics
                    int printedMetrics = 0;
                    if (metrics.Contains("Pages"))
                    {
                        string val = item.PingSuccess ? item.PageCounter.ToString("N0") : "-";
                        DrawMetricValue(gfx, val, fontRegular, ref printedMetrics, colMetric1, colMetric2, y);
                    }
                    if (metrics.Contains("Toner"))
                    {
                        string val = "-";
                        if (item.PingSuccess && item.Toners.Any())
                        {
                            var list = item.Toners.Select(t => $"{t.Color.Substring(0,1)}:{t.CurrentLevel}%");
                            val = string.Join(" ", list);
                        }
                        DrawMetricValue(gfx, val, fontRegular, ref printedMetrics, colMetric1, colMetric2, y);
                    }
                    if (metrics.Contains("Availability"))
                    {
                        string val = item.PingSuccess ? "100%" : "0%";
                        DrawMetricValue(gfx, val, fontRegular, ref printedMetrics, colMetric1, colMetric2, y);
                    }
                    if (metrics.Contains("Alerts"))
                    {
                        string val = item.Alerts.Any() ? $"{item.Alerts.Count} alerte(s)" : "Aucune";
                        DrawMetricValue(gfx, val, fontRegular, ref printedMetrics, colMetric1, colMetric2, y);
                    }
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
                                    if (days == -99) return $"{t.Color.Substring(0,1)}:Stab";
                                    return $"{t.Color.Substring(0,1)}:{days}j";
                                });
                            if (list.Any()) val = string.Join(" ", list);
                        }
                        DrawMetricValue(gfx, val, fontRegular, ref printedMetrics, colMetric1, colMetric2, y);
                    }
                }

                using (var ms = new MemoryStream())
                {
                    document.Save(ms);
                    return ms.ToArray();
                }
            }
        }

        private string MapMetricHeader(string metric)
        {
            return metric switch
            {
                "Pages" => "Compteur",
                "Toner" => "Toners",
                "Availability" => "Dispo",
                "Alerts" => "Pannes",
                "Predictions" => "Prévision",
                _ => ""
            };
        }

        private void DrawMetricValue(XGraphics gfx, string val, XFont font, ref int printedMetrics, int col1, int col2, int y)
        {
            if (printedMetrics == 0)
            {
                gfx.DrawString(val, font, XBrushes.Black, col1, y);
                printedMetrics++;
            }
            else if (printedMetrics == 1)
            {
                gfx.DrawString(val, font, XBrushes.Black, col2, y);
                printedMetrics++;
            }
        }

        private string EscapeCsv(string? field)
        {
            if (string.IsNullOrEmpty(field)) return string.Empty;
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
            public List<string> Alerts { get; set; } = new();
            public List<Autoprint.Shared.DTOs.TonerLevelResult> Toners { get; set; } = new();
        }
    }
}
