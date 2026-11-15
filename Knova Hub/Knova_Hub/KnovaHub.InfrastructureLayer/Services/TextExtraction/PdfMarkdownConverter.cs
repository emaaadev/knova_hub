using System.Text;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;

namespace KnovaHub.InfrastructureLayer.Services.TextExtraction;

/// <summary>
/// Convierte el texto de un PDF a Markdown con PdfPig.
/// El PDF no guarda párrafos ni tablas, solo letras con posición, así que se reconstruyen:
/// - Las palabras con la misma línea base forman una línea.
/// - Un espacio horizontal grande entre palabras separa celdas; varias líneas seguidas
///   con el mismo número de celdas se escriben como tabla Markdown.
/// - Una línea con letra bastante más grande que el texto normal se escribe como título.
/// - Las líneas cercanas se unen en un párrafo; un salto vertical grande abre otro.
/// Limitación conocida: los documentos a dos columnas se leen fila por fila.
/// </summary>
internal static class PdfMarkdownConverter
{
    // Espacio entre palabras, en múltiplos del tamaño de letra, a partir del cual se considera otra celda.
    private const double CellGapFactor = 1.2;
    // Salto vertical, en múltiplos del tamaño de letra, a partir del cual empieza otro párrafo.
    private const double ParagraphGapFactor = 0.8;
    private const double Heading1Factor = 1.6;
    private const double Heading2Factor = 1.25;
    private const int MaxHeadingLength = 120;

    private static readonly string[] Bullets = { "•", "◦", "▪", "‣", "–", "-", "*" };

    public static PdfConversion Convert(byte[] bytes, CancellationToken cancellationToken)
    {
        using var pdf = PdfDocument.Open(bytes);

        var pages = new List<List<PdfLine>>();
        bool hasImages = false;

        foreach (Page page in pdf.GetPages())
        {
            cancellationToken.ThrowIfCancellationRequested();

            var words = page.GetWords()
                .Where(w => !string.IsNullOrWhiteSpace(w.Text))
                .ToList();

            if (words.Count == 0)
            {
                hasImages |= page.GetImages().Any();
                continue;
            }

            pages.Add(GroupIntoLines(words));
        }

        if (pages.Count == 0)
            return new PdfConversion(string.Empty, HasText: false, hasImages);

        // El tamaño "normal" se calcula sobre todo el documento para que una portada no lo distorsione.
        double bodyFontSize = Median(pages.SelectMany(p => p).Select(l => l.FontSize));

        var markdown = new StringBuilder();
        foreach (var lines in pages)
            RenderPage(lines, bodyFontSize, markdown);

        return new PdfConversion(markdown.ToString(), HasText: true, hasImages);
    }

    private static List<PdfLine> GroupIntoLines(List<Word> words)
    {
        var lines = new List<PdfLine>();

        // En PDF el eje Y crece hacia arriba: se recorre de arriba hacia abajo.
        foreach (var word in words.OrderByDescending(w => w.BoundingBox.Bottom).ThenBy(w => w.BoundingBox.Left))
        {
            var line = lines.LastOrDefault();
            double tolerance = Math.Max(word.BoundingBox.Height, 1) * 0.5;

            if (line != null && Math.Abs(line.Baseline - word.BoundingBox.Bottom) <= tolerance)
                line.Words.Add(word);
            else
                lines.Add(new PdfLine(word.BoundingBox.Bottom, new List<Word> { word }));
        }

        foreach (var line in lines)
            line.Complete();

        return lines;
    }

    private static void RenderPage(List<PdfLine> lines, double bodyFontSize, StringBuilder markdown)
    {
        var paragraph = new StringBuilder();
        PdfLine? previous = null;

        void FlushParagraph()
        {
            if (paragraph.Length == 0) return;
            MarkdownText.EndList(markdown);
            markdown.Append(paragraph).Append("\n\n");
            paragraph.Clear();
        }

        for (int i = 0; i < lines.Count; i++)
        {
            var line = lines[i];

            // Tabla: dos o más líneas seguidas con el mismo número de celdas (mínimo 2).
            int tableEnd = TableRunEnd(lines, i);
            if (tableEnd > i)
            {
                FlushParagraph();
                RenderTable(lines.GetRange(i, tableEnd - i + 1), markdown);
                previous = null;
                i = tableEnd;
                continue;
            }

            string text = string.Join(" ", line.Cells);

            if (line.FontSize >= bodyFontSize * Heading2Factor && text.Length <= MaxHeadingLength)
            {
                FlushParagraph();
                MarkdownText.EndList(markdown);
                string prefix = line.FontSize >= bodyFontSize * Heading1Factor ? "# " : "## ";
                markdown.Append(prefix).Append(text).Append("\n\n");
                previous = null;
                continue;
            }

            string? bullet = Bullets.FirstOrDefault(b => text.StartsWith(b + " ", StringComparison.Ordinal));
            if (bullet != null)
            {
                FlushParagraph();
                markdown.Append("- ").Append(text[(bullet.Length + 1)..].TrimStart()).Append('\n');
                previous = null;
                continue;
            }

            bool newParagraph = previous == null ||
                previous.Bottom - line.Top > line.FontSize * ParagraphGapFactor;

            if (newParagraph)
            {
                FlushParagraph();
                paragraph.Append(text);
            }
            else if (paragraph.Length > 0 && paragraph[^1] == '-' && char.IsLower(text[0]))
            {
                // Palabra cortada con guion al final de la línea: se vuelve a unir.
                paragraph.Length--;
                paragraph.Append(text);
            }
            else
            {
                paragraph.Append(' ').Append(text);
            }

            previous = line;
        }

        FlushParagraph();
    }

    private static int TableRunEnd(List<PdfLine> lines, int start)
    {
        int columns = lines[start].Cells.Count;
        if (columns < 2) return start;

        int end = start;
        while (end + 1 < lines.Count && lines[end + 1].Cells.Count == columns)
            end++;

        return end > start ? end : start;
    }

    private static void RenderTable(List<PdfLine> rows, StringBuilder markdown)
    {
        static string Row(IEnumerable<string> cells) =>
            "| " + string.Join(" | ", cells.Select(c => c.Replace("|", "\\|"))) + " |";

        MarkdownText.EndList(markdown);
        markdown.Append(Row(rows[0].Cells)).Append('\n');
        markdown.Append(Row(rows[0].Cells.Select(_ => "---"))).Append('\n');
        foreach (var row in rows.Skip(1))
            markdown.Append(Row(row.Cells)).Append('\n');
        markdown.Append('\n');
    }

    private static double Median(IEnumerable<double> values)
    {
        var sorted = values.OrderBy(v => v).ToArray();
        if (sorted.Length == 0) return 0;
        int middle = sorted.Length / 2;
        return sorted.Length % 2 == 1 ? sorted[middle] : (sorted[middle - 1] + sorted[middle]) / 2;
    }

    private sealed class PdfLine
    {
        public PdfLine(double baseline, List<Word> words)
        {
            Baseline = baseline;
            Words = words;
        }

        public double Baseline { get; }
        public List<Word> Words { get; }
        public List<string> Cells { get; } = new();
        public double FontSize { get; private set; }
        public double Top { get; private set; }
        public double Bottom { get; private set; }

        public void Complete()
        {
            Words.Sort((a, b) => a.BoundingBox.Left.CompareTo(b.BoundingBox.Left));

            var letters = Words.SelectMany(w => w.Letters).ToList();
            FontSize = letters.Count > 0
                ? letters.Average(l => l.PointSize)
                : Words.Average(w => w.BoundingBox.Height);
            Top = Words.Max(w => w.BoundingBox.Top);
            Bottom = Words.Min(w => w.BoundingBox.Bottom);

            var cell = new StringBuilder(Words[0].Text);
            for (int i = 1; i < Words.Count; i++)
            {
                double gap = Words[i].BoundingBox.Left - Words[i - 1].BoundingBox.Right;
                if (gap > FontSize * CellGapFactor)
                {
                    Cells.Add(cell.ToString());
                    cell.Clear();
                }
                else
                {
                    cell.Append(' ');
                }
                cell.Append(Words[i].Text);
            }
            Cells.Add(cell.ToString());
        }
    }
}

internal record PdfConversion(string Markdown, bool HasText, bool HasImages);
