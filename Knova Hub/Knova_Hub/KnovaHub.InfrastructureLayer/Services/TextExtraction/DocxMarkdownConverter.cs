using System.Text;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace KnovaHub.InfrastructureLayer.Services.TextExtraction;

/// <summary>
/// Convierte un DOCX a Markdown con DocumentFormat.OpenXml: los títulos de Word pasan a "#",
/// las listas a "- " y las tablas a tablas Markdown. Se descarta el formato visual (negritas,
/// colores, fuentes) porque no aporta a la IA y cuesta tokens.
/// </summary>
internal static class DocxMarkdownConverter
{
    public static string Convert(byte[] bytes)
    {
        using var stream = new MemoryStream(bytes, writable: false);
        using var document = WordprocessingDocument.Open(stream, isEditable: false);

        var mainPart = document.MainDocumentPart
            ?? throw new InvalidDataException("El DOCX no tiene documento principal.");
        var body = mainPart.Document?.Body
            ?? throw new InvalidDataException("El DOCX no tiene cuerpo.");

        // Word guarda el nombre canónico del estilo ("heading 1", "Title") aunque esté en español,
        // mientras que el id cambia con el idioma ("Ttulo1"). Por eso se resuelve por nombre.
        var styleNames = mainPart.StyleDefinitionsPart?.Styles?
            .Elements<Style>()
            .Where(s => s.StyleId?.Value != null && s.StyleName?.Val?.Value != null)
            .GroupBy(s => s.StyleId!.Value!)
            .ToDictionary(g => g.Key, g => g.First().StyleName!.Val!.Value!.ToLowerInvariant())
            ?? new Dictionary<string, string>();

        var markdown = new StringBuilder();
        RenderBlocks(body.ChildElements, styleNames, markdown);
        return markdown.ToString();
    }

    private static void RenderBlocks(
        IEnumerable<OpenXmlElement> elements,
        IReadOnlyDictionary<string, string> styleNames,
        StringBuilder markdown)
    {
        foreach (var element in elements)
        {
            switch (element)
            {
                case Paragraph paragraph:
                    RenderParagraph(paragraph, styleNames, markdown);
                    break;
                case Table table:
                    RenderTable(table, markdown);
                    break;
                case SdtBlock contentControl:
                    // Controles de contenido (por ejemplo, índices o plantillas): se procesa lo que contienen.
                    var inner = contentControl.SdtContentBlock?.ChildElements;
                    if (inner != null) RenderBlocks(inner, styleNames, markdown);
                    break;
            }
        }
    }

    private static void RenderParagraph(
        Paragraph paragraph,
        IReadOnlyDictionary<string, string> styleNames,
        StringBuilder markdown)
    {
        string text = TextOf(paragraph);
        if (text.Length == 0) return;

        var properties = paragraph.ParagraphProperties;
        string? styleId = properties?.ParagraphStyleId?.Val?.Value;
        string styleName = styleId != null && styleNames.TryGetValue(styleId, out var name) ? name : string.Empty;

        if (properties?.NumberingProperties != null || styleName.StartsWith("list", StringComparison.Ordinal))
        {
            markdown.Append("- ").Append(text).Append('\n');
            return;
        }

        MarkdownText.EndList(markdown);

        int level = HeadingLevel(styleName, properties?.OutlineLevel?.Val?.Value);
        if (level > 0)
        {
            markdown.Append('#', level).Append(' ').Append(text).Append("\n\n");
            return;
        }

        markdown.Append(text).Append("\n\n");
    }

    private static int HeadingLevel(string styleName, int? outlineLevel)
    {
        if (styleName == "title")
            return 1;

        if (styleName.StartsWith("heading ", StringComparison.Ordinal) &&
            int.TryParse(styleName["heading ".Length..], out int heading))
            return Math.Clamp(heading, 1, 6);

        // Nivel de esquema 0-8 definido directamente en el párrafo.
        return outlineLevel is int outline and < 9 ? Math.Min(outline + 1, 6) : 0;
    }

    private static void RenderTable(Table table, StringBuilder markdown)
    {
        var rows = table.Elements<TableRow>()
            .Select(row => row.Elements<TableCell>()
                .Select(cell => string.Join(" ", cell.Elements<Paragraph>().Select(TextOf).Where(t => t.Length > 0))
                    .Replace("|", "\\|"))
                .ToList())
            .Where(cells => cells.Any(c => c.Length > 0))
            .ToList();

        if (rows.Count == 0) return;

        int columns = rows.Max(r => r.Count);
        foreach (var row in rows)
            while (row.Count < columns) row.Add(string.Empty);

        static string Row(IEnumerable<string> cells) => "| " + string.Join(" | ", cells) + " |";

        MarkdownText.EndList(markdown);
        markdown.Append(Row(rows[0])).Append('\n');
        markdown.Append(Row(Enumerable.Repeat("---", columns))).Append('\n');
        foreach (var row in rows.Skip(1))
            markdown.Append(Row(row)).Append('\n');
        markdown.Append('\n');
    }

    private static string TextOf(OpenXmlElement element)
    {
        var text = new StringBuilder();
        foreach (var node in element.Descendants())
        {
            switch (node)
            {
                case Text t:
                    text.Append(t.Text);
                    break;
                case TabChar:
                case Break:
                case CarriageReturn:
                    text.Append(' ');
                    break;
            }
        }
        return text.ToString().Trim();
    }
}
