using System.IO.Compression;
using System.Text;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Fonts.Standard14Fonts;
using UglyToad.PdfPig.Writer;
using PageSize = UglyToad.PdfPig.Content.PageSize;

namespace KnovaHub.Tests.Helpers;

/// <summary>
/// Archivos de ejemplo generados en memoria para probar la conversión a Markdown:
/// los cuatro PDF que pide el ticket (texto normal, con tablas, vacío y dañado),
/// uno escaneado (solo imagen) y un DOCX con título, lista y tabla.
/// </summary>
public static class SampleFiles
{
    // PNG de 1x1 píxel: basta para simular una página escaneada (imagen sin texto).
    private static readonly byte[] Png1x1 = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+M9QDwADhgGAWjR9awAAAABJRU5ErkJggg==");

    public static byte[] PdfTextoNormal()
    {
        var builder = new PdfDocumentBuilder();
        var font = builder.AddStandard14Font(Standard14Font.Helvetica);
        var page = builder.AddPage(PageSize.A4);

        page.AddText("Manual de Bienvenida", 22, new PdfPoint(50, 760), font);
        page.AddText("Este manual explica las politicas internas de la empresa", 11, new PdfPoint(50, 720), font);
        page.AddText("para los empleados nuevos durante su primera se-", 11, new PdfPoint(50, 706), font);
        page.AddText("mana de trabajo.", 11, new PdfPoint(50, 692), font);
        page.AddText("Horario", 15, new PdfPoint(50, 650), font);
        page.AddText("La jornada laboral es de lunes a viernes de 8:00 a 17:00.", 11, new PdfPoint(50, 625), font);
        page.AddText("- Marcar la entrada en el sistema", 11, new PdfPoint(50, 600), font);
        page.AddText("- Avisar las ausencias al supervisor", 11, new PdfPoint(50, 586), font);

        return builder.Build();
    }

    public static byte[] PdfConTabla()
    {
        var builder = new PdfDocumentBuilder();
        var font = builder.AddStandard14Font(Standard14Font.Helvetica);
        var page = builder.AddPage(PageSize.A4);

        page.AddText("Directorio de departamentos", 11, new PdfPoint(50, 760), font);

        string[][] rows =
        {
            new[] { "Departamento", "Responsable", "Extension" },
            new[] { "Recursos Humanos", "Ana Gomez", "101" },
            new[] { "Finanzas", "Luis Perez", "205" },
            new[] { "Tecnologia", "Maria Diaz", "310" }
        };

        double y = 720;
        foreach (var row in rows)
        {
            page.AddText(row[0], 11, new PdfPoint(50, y), font);
            page.AddText(row[1], 11, new PdfPoint(220, y), font);
            page.AddText(row[2], 11, new PdfPoint(390, y), font);
            y -= 18;
        }

        return builder.Build();
    }

    /// <summary>PDF válido con una página en blanco.</summary>
    public static byte[] PdfVacio()
    {
        var builder = new PdfDocumentBuilder();
        builder.AddPage(PageSize.A4);
        return builder.Build();
    }

    /// <summary>PDF válido cuya única página es una imagen: así se ve un documento escaneado.</summary>
    public static byte[] PdfEscaneado()
    {
        var builder = new PdfDocumentBuilder();
        var page = builder.AddPage(PageSize.A4);
        page.AddPng(Png1x1, new PdfRectangle(0, 0, 595, 842));
        return builder.Build();
    }

    /// <summary>Tiene la firma "%PDF-" (pasa la validación de la subida) pero el contenido es basura.</summary>
    public static byte[] PdfDanado() =>
        Encoding.ASCII.GetBytes("%PDF-1.7\n1 0 obj << /Type /Catalog /Pages 9 0 R >>\nesto no es un pdf valido\n%%EOF");

    /// <summary>
    /// DOCX con título, párrafo, lista y tabla. El estilo de título usa el id en español ("Ttulo1"),
    /// como lo guarda Word en español, para comprobar que se reconoce por su nombre ("heading 1").
    /// </summary>
    public static byte[] DocxCompleto()
    {
        using var stream = new MemoryStream();
        using (var document = WordprocessingDocument.Create(stream, WordprocessingDocumentType.Document))
        {
            var main = document.AddMainDocumentPart();
            var styles = main.AddNewPart<StyleDefinitionsPart>();
            styles.Styles = new Styles(
                new Style(new StyleName { Val = "heading 1" }) { Type = StyleValues.Paragraph, StyleId = "Ttulo1" },
                new Style(new StyleName { Val = "heading 2" }) { Type = StyleValues.Paragraph, StyleId = "Ttulo2" });

            static Paragraph Styled(string styleId, string text) => new(
                new ParagraphProperties(new ParagraphStyleId { Val = styleId }),
                new Run(new Text(text)));

            static Paragraph Bullet(string text) => new(
                new ParagraphProperties(new NumberingProperties(
                    new NumberingLevelReference { Val = 0 }, new NumberingId { Val = 1 })),
                new Run(new Text(text)));

            static TableRow Row(params string[] cells) =>
                new(cells.Select(c => new TableCell(new Paragraph(new Run(new Text(c))))));

            main.Document = new Document(new Body(
                Styled("Ttulo1", "Politica de Vacaciones"),
                new Paragraph(new Run(new Text("Cada empleado tiene derecho a 14 dias al año."))),
                Styled("Ttulo2", "Requisitos"),
                Bullet("Solicitar con dos semanas de anticipacion"),
                Bullet("Tener la aprobacion del supervisor"),
                new Table(
                    Row("Antiguedad", "Dias"),
                    Row("1 a 5 años", "14"),
                    Row("Mas de 5 años", "18"))));
        }

        return stream.ToArray();
    }

    /// <summary>ZIP con la estructura de un DOCX, pero con XML inválido dentro.</summary>
    public static byte[] DocxDanado()
    {
        using var stream = new MemoryStream();
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            using (var writer = new StreamWriter(zip.CreateEntry("[Content_Types].xml").Open()))
                writer.Write("<Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\">");
            using (var writer = new StreamWriter(zip.CreateEntry("word/document.xml").Open()))
                writer.Write("esto no es xml");
        }
        return stream.ToArray();
    }
}
