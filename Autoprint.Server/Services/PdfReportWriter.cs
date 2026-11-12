using SkiaSharp;

namespace Autoprint.Server.Services
{
    /// <summary>
    /// Petit écrivain PDF (format A4, unités en points) basé sur SkiaSharp (Microsoft, licence MIT).
    /// Remplace PDFsharp, passé sous licence AGPL à partir de la 6.5 (backlog B42).
    /// Les coordonnées Y de <see cref="Text"/> désignent la ligne de base du texte, comme avec PDFsharp.
    /// </summary>
    public sealed class PdfReportWriter : IDisposable
    {
        public const float PageWidth = 595f;   // A4 en points (1/72 de pouce)
        public const float PageHeight = 842f;

        private readonly MemoryStream _stream = new();
        private readonly SKDocument _document;
        private readonly Dictionary<(bool Bold, float Size), SKFont> _fonts = new();
        private readonly SKTypeface _regular;
        private readonly SKTypeface _bold;
        private SKCanvas? _canvas;

        public PdfReportWriter(string title)
        {
            var metadata = new SKDocumentPdfMetadata
            {
                Title = title,
                Creator = "Autoprint",
                Producer = "Autoprint (SkiaSharp)",
                Creation = DateTime.Now,
                Modified = DateTime.Now
            };
            _document = SKDocument.CreatePdf(_stream, metadata);
            // Police embarquée (IBM Plex Sans de la charte, sous-ensemble latin renommé, ~38 Ko) : Skia intègre toujours
            // la police entière dans le PDF, une police système comme Arial (~1 Mo) donnerait des rapports de plus d'un mégaoctet.
            // Arial ne sert que de secours si la ressource manquait.
            _regular = LoadEmbeddedTypeface("AutoprintSans-Regular.ttf")
                       ?? SKTypeface.FromFamilyName("Arial", SKFontStyle.Normal) ?? SKTypeface.Default;
            _bold = LoadEmbeddedTypeface("AutoprintSans-Bold.ttf")
                    ?? SKTypeface.FromFamilyName("Arial", SKFontStyle.Bold) ?? _regular;
            NewPage();
        }

        private static SKTypeface? LoadEmbeddedTypeface(string fileName)
        {
            using var resource = typeof(PdfReportWriter).Assembly.GetManifestResourceStream("Autoprint.Fonts." + fileName);
            if (resource == null) return null;
            using var data = SKData.Create(resource);
            return data == null ? null : SKTypeface.FromData(data);
        }

        /// <summary>Numéro de la page en cours (1 pour la première).</summary>
        public int PageNumber { get; private set; }

        public void NewPage()
        {
            if (_canvas != null) _document.EndPage();
            _canvas = _document.BeginPage(PageWidth, PageHeight);
            PageNumber++;
        }

        public float MeasureText(string text, float size, bool bold = false) => Font(size, bold).MeasureText(text);

        /// <summary>Raccourcit <paramref name="text"/> (avec « … ») pour qu'il tienne dans <paramref name="maxWidth"/> points.</summary>
        public string Fit(string text, float maxWidth, float size, bool bold = false)
        {
            if (string.IsNullOrEmpty(text) || MeasureText(text, size, bold) <= maxWidth) return text;
            var t = text;
            while (t.Length > 1 && MeasureText(t.TrimEnd() + "…", size, bold) > maxWidth) t = t[..^1];
            return t.TrimEnd() + "…";
        }

        /// <summary>Texte aligné à droite sur <paramref name="rightX"/>.</summary>
        public void TextRight(string text, float rightX, float baselineY, float size, SKColor color, bool bold = false)
            => Text(text, rightX - MeasureText(text, size, bold), baselineY, size, color, bold);

        /// <summary>Rectangle plein aux coins arrondis.</summary>
        public void FillRoundRect(float x, float y, float width, float height, float radius, SKColor color)
        {
            using var paint = new SKPaint { Color = color, Style = SKPaintStyle.Fill, IsAntialias = true };
            Canvas.DrawRoundRect(x, y, width, height, radius, radius, paint);
        }

        private SKCanvas Canvas => _canvas ?? throw new InvalidOperationException("Aucune page ouverte.");

        private SKFont Font(float size, bool bold)
        {
            if (!_fonts.TryGetValue((bold, size), out var font))
            {
                font = new SKFont(bold ? _bold : _regular, size) { Subpixel = true };
                _fonts[(bold, size)] = font;
            }
            return font;
        }

        /// <summary>Texte dont <paramref name="baselineY"/> est la ligne de base.</summary>
        public void Text(string text, float x, float baselineY, float size, SKColor color, bool bold = false)
        {
            using var paint = new SKPaint { Color = color, IsAntialias = true };
            Canvas.DrawText(text, x, baselineY, SKTextAlign.Left, Font(size, bold), paint);
        }

        /// <summary>Texte dont <paramref name="topY"/> est le haut des lettres (comme XStringFormats.TopLeft).</summary>
        public void TextTop(string text, float x, float topY, float size, SKColor color, bool bold = false)
            => Text(text, x, topY - Font(size, bold).Metrics.Ascent, size, color, bold);

        public void Line(float x1, float y1, float x2, float y2, SKColor color, float width = 1f)
        {
            using var paint = new SKPaint { Color = color, StrokeWidth = width, IsAntialias = true, Style = SKPaintStyle.Stroke };
            Canvas.DrawLine(x1, y1, x2, y2, paint);
        }

        public void FillRect(float x, float y, float width, float height, SKColor color)
        {
            using var paint = new SKPaint { Color = color, Style = SKPaintStyle.Fill };
            Canvas.DrawRect(x, y, width, height, paint);
        }

        public byte[] ToArray()
        {
            if (_canvas != null) { _document.EndPage(); _canvas = null; }
            _document.Close();
            return _stream.ToArray();
        }

        public void Dispose()
        {
            foreach (var font in _fonts.Values) font.Dispose();
            _document.Dispose();
            _stream.Dispose();
        }
    }
}
