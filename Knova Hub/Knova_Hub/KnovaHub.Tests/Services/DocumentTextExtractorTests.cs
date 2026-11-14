using System.Text;
using KnovaHub.ApplicationLayer.DTOs;
using KnovaHub.InfrastructureLayer.Services;
using KnovaHub.Tests.Helpers;
using Microsoft.Extensions.Logging.Abstractions;

namespace KnovaHub.Tests.Services;

/// <summary>
/// Pruebas de la conversión de adjuntos a Markdown con archivos de ejemplo reales
/// (generados en memoria con PdfPig y OpenXml). Cubren los cuatro tipos de PDF del ticket:
/// texto normal, con tablas, vacío y dañado; además del PDF escaneado, DOCX, TXT y MD.
/// </summary>
[TestFixture]
public class DocumentTextExtractorTests
{
    private DocumentTextExtractor _extractor = null!;

    [SetUp]
    public void SetUp()
    {
        _extractor = new DocumentTextExtractor(NullLogger<DocumentTextExtractor>.Instance);
    }

    private Task<DocumentTextResult> Extraer(byte[] bytes, string extension) =>
        _extractor.ExtractAsync(new MemoryStream(bytes), extension);

    // ---------- PDF: los cuatro tipos del ticket ----------

    [Test]
    public async Task Pdf_TextoNormal_EsIndexableConTitulosParrafosYListas()
    {
        var resultado = await Extraer(SampleFiles.PdfTextoNormal(), ".pdf");

        Assert.That(resultado.IsIndexable, Is.True, resultado.Reason);
        Assert.Multiple(() =>
        {
            Assert.That(resultado.Markdown, Does.StartWith("# Manual de Bienvenida\n\n"));
            Assert.That(resultado.Markdown, Does.Contain("\n## Horario\n"));
            // Las tres líneas del párrafo se unen en una sola, y "se-" + "mana" vuelve a ser "semana".
            Assert.That(resultado.Markdown, Does.Contain(
                "Este manual explica las politicas internas de la empresa para los empleados nuevos durante su primera semana de trabajo."));
            Assert.That(resultado.Markdown, Does.Contain("- Marcar la entrada en el sistema\n- Avisar las ausencias al supervisor"));
            Assert.That(resultado.Reason, Is.Null);
        });
    }

    [Test]
    public async Task Pdf_ConTabla_ConvierteLasFilasEnTablaMarkdown()
    {
        var resultado = await Extraer(SampleFiles.PdfConTabla(), ".pdf");

        Assert.That(resultado.IsIndexable, Is.True, resultado.Reason);
        Assert.That(resultado.Markdown, Does.Contain(
            "| Departamento | Responsable | Extension |\n" +
            "| --- | --- | --- |\n" +
            "| Recursos Humanos | Ana Gomez | 101 |\n" +
            "| Finanzas | Luis Perez | 205 |\n" +
            "| Tecnologia | Maria Diaz | 310 |"));
        Assert.That(resultado.Markdown, Does.StartWith("Directorio de departamentos\n\n"),
            "El texto antes de la tabla queda como párrafo, fuera de la tabla");
    }

    [Test]
    public async Task Pdf_Vacio_NoEsIndexableYExplicaElMotivo()
    {
        var resultado = await Extraer(SampleFiles.PdfVacio(), ".pdf");

        Assert.Multiple(() =>
        {
            Assert.That(resultado.IsIndexable, Is.False);
            Assert.That(resultado.Reason, Is.EqualTo(DocumentTextExtractor.MensajePdfSinTexto));
            Assert.That(resultado.Markdown, Is.Empty);
        });
    }

    [Test]
    public async Task Pdf_Danado_NoLanzaExcepcionYNoEsIndexable()
    {
        DocumentTextResult? resultado = null;

        Assert.DoesNotThrowAsync(async () => resultado = await Extraer(SampleFiles.PdfDanado(), ".pdf"));

        Assert.Multiple(() =>
        {
            Assert.That(resultado!.IsIndexable, Is.False);
            Assert.That(resultado.Reason, Is.EqualTo(DocumentTextExtractor.MensajePdfDanado));
        });
    }

    [Test]
    public async Task Pdf_Escaneado_NoEsIndexableYPideUnaVersionConTexto()
    {
        var resultado = await Extraer(SampleFiles.PdfEscaneado(), ".pdf");

        Assert.Multiple(() =>
        {
            Assert.That(resultado.IsIndexable, Is.False);
            Assert.That(resultado.Reason, Is.EqualTo(DocumentTextExtractor.MensajePdfEscaneado));
        });
    }

    [Test]
    public void Pdf_Truncado_NuncaLanzaExcepcion()
    {
        var completo = SampleFiles.PdfTextoNormal();
        var truncado = completo.Take(completo.Length / 2).ToArray();

        // Según qué parte se pierda, PdfPig puede recuperar algo de texto o no; lo importante es que no falle.
        Assert.DoesNotThrowAsync(() => Extraer(truncado, ".pdf"));
    }

    [Test]
    public async Task Pdf_MarkdownOcupaMenosQueElArchivoOriginal()
    {
        var pdf = SampleFiles.PdfTextoNormal();

        var resultado = await Extraer(pdf, ".pdf");

        Assert.That(Encoding.UTF8.GetByteCount(resultado.Markdown), Is.LessThan(pdf.Length / 2),
            "El objetivo del ticket es que la IA lea mucho menos que el PDF");
    }

    // ---------- DOCX ----------

    [Test]
    public async Task Docx_ConvierteTitulosListasYTablas()
    {
        var resultado = await Extraer(SampleFiles.DocxCompleto(), ".docx");

        Assert.That(resultado.IsIndexable, Is.True, resultado.Reason);
        Assert.That(resultado.Markdown, Is.EqualTo(
            "# Politica de Vacaciones\n\n" +
            "Cada empleado tiene derecho a 14 dias al año.\n\n" +
            "## Requisitos\n\n" +
            "- Solicitar con dos semanas de anticipacion\n" +
            "- Tener la aprobacion del supervisor\n" +
            "\n" +
            "| Antiguedad | Dias |\n" +
            "| --- | --- |\n" +
            "| 1 a 5 años | 14 |\n" +
            "| Mas de 5 años | 18 |"));
    }

    [Test]
    public async Task Docx_Danado_NoLanzaExcepcionYNoEsIndexable()
    {
        var resultado = await Extraer(SampleFiles.DocxDanado(), ".docx");

        Assert.Multiple(() =>
        {
            Assert.That(resultado.IsIndexable, Is.False);
            Assert.That(resultado.Reason, Is.EqualTo(DocumentTextExtractor.MensajeDocxDanado));
        });
    }

    // ---------- TXT y MD ----------

    [Test]
    public async Task Txt_SeNormalizaSinBomSinEspaciosFinalesNiLineasEnBlancoDeMas()
    {
        var bytes = Encoding.UTF8.GetPreamble()
            .Concat(Encoding.UTF8.GetBytes("Línea uno   \r\nLínea dos\r\n\r\n\r\n\r\nPárrafo final\t\r\n"))
            .ToArray();

        var resultado = await Extraer(bytes, ".txt");

        Assert.That(resultado.Markdown, Is.EqualTo("Línea uno\nLínea dos\n\nPárrafo final"));
    }

    [Test]
    public async Task Md_ConservaElMarkdownOriginal()
    {
        const string markdown = "# Título\n\nTexto con **negrita**.\n\n| A | B |\n| --- | --- |\n| 1 | 2 |";

        var resultado = await Extraer(Encoding.UTF8.GetBytes(markdown), ".md");

        Assert.That(resultado.Markdown, Is.EqualTo(markdown));
    }

    [TestCase(".txt")]
    [TestCase(".md")]
    public async Task TextoSoloConEspacios_NoEsIndexable(string extension)
    {
        var resultado = await Extraer(Encoding.UTF8.GetBytes("  \r\n\t \n  "), extension);

        Assert.Multiple(() =>
        {
            Assert.That(resultado.IsIndexable, Is.False);
            Assert.That(resultado.Reason, Is.EqualTo(DocumentTextExtractor.MensajeArchivoVacio));
        });
    }

    // ---------- Comportamiento general ----------

    [Test]
    public async Task FormatoNoSoportado_NoEsIndexable()
    {
        var resultado = await Extraer(new byte[] { 1, 2, 3 }, ".xlsx");

        Assert.That(resultado.Reason, Is.EqualTo(DocumentTextExtractor.MensajeFormatoNoSoportado));
    }

    [Test]
    public async Task ExtensionEnMayusculas_SeReconoce()
    {
        var resultado = await Extraer(SampleFiles.PdfConTabla(), ".PDF");

        Assert.That(resultado.IsIndexable, Is.True);
    }

    [Test]
    public async Task NoCierraElStreamRecibido()
    {
        // DocumentService sigue usando el stream para guardar el archivo después de convertirlo.
        var stream = new MemoryStream(SampleFiles.PdfTextoNormal());

        await _extractor.ExtractAsync(stream, ".pdf");

        Assert.That(stream.CanRead, Is.True);
    }

    [Test]
    public void Cancelacion_SePropaga()
    {
        using var cancelado = new CancellationTokenSource();
        cancelado.Cancel();

        Assert.CatchAsync<OperationCanceledException>(() =>
            _extractor.ExtractAsync(new MemoryStream(SampleFiles.PdfTextoNormal()), ".pdf", cancelado.Token));
    }
}
