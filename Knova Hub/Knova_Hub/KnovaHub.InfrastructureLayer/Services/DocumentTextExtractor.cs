using System.Text;
using System.Text.RegularExpressions;
using KnovaHub.ApplicationLayer.Contract;
using KnovaHub.ApplicationLayer.DTOs;
using KnovaHub.InfrastructureLayer.Services.TextExtraction;
using Microsoft.Extensions.Logging;
using UglyToad.PdfPig.Exceptions;

namespace KnovaHub.InfrastructureLayer.Services;

/// <summary>
/// Implementación de IDocumentTextExtractor. Convierte PDF (PdfPig), DOCX (OpenXml),
/// TXT y MD a Markdown compacto para que la IA gaste menos tokens que leyendo el archivo.
/// Un archivo sin texto o dañado nunca provoca un error 500: se devuelve como no indexable.
/// </summary>
public partial class DocumentTextExtractor : IDocumentTextExtractor
{
    public const string MensajePdfEscaneado =
        "El PDF parece escaneado: solo contiene imágenes, sin texto seleccionable. La IA no podrá leerlo; sube una versión con texto.";
    public const string MensajePdfSinTexto = "El PDF no contiene texto.";
    public const string MensajePdfProtegido = "El PDF está protegido con contraseña y no se puede leer.";
    public const string MensajePdfDanado = "El PDF está dañado o no se puede leer.";
    public const string MensajeDocxDanado = "El DOCX está dañado o no se puede leer.";
    public const string MensajeArchivoVacio = "El archivo no contiene texto.";
    public const string MensajeFormatoNoSoportado = "El formato del archivo no se puede convertir a texto.";

    private readonly ILogger<DocumentTextExtractor> _logger;

    public DocumentTextExtractor(ILogger<DocumentTextExtractor> logger)
    {
        _logger = logger;
    }

    public async Task<DocumentTextResult> ExtractAsync(
        Stream content,
        string fileExtension,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(content);

        // Se copia sin cerrar el stream: quien llama lo sigue usando para guardar el archivo.
        using var copy = new MemoryStream();
        await content.CopyToAsync(copy, cancellationToken);
        byte[] bytes = copy.ToArray();

        string extension = (fileExtension ?? string.Empty).ToLowerInvariant();

        try
        {
            var result = extension switch
            {
                ".pdf" => FromPdf(bytes, cancellationToken),
                ".docx" => FromMarkdown(DocxMarkdownConverter.Convert(bytes)),
                ".txt" or ".md" => FromMarkdown(DecodeUtf8(bytes)),
                _ => DocumentTextResult.NotIndexable(MensajeFormatoNoSoportado)
            };

            if (!result.IsIndexable)
                _logger.LogInformation("Archivo {Extension} no indexable: {Reason}", extension, result.Reason);

            return result;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (PdfDocumentEncryptedException)
        {
            return DocumentTextResult.NotIndexable(MensajePdfProtegido);
        }
        catch (Exception ex)
        {
            // El detalle técnico solo va al log; al usuario se le da un motivo claro.
            _logger.LogWarning(ex, "No se pudo convertir un archivo {Extension} a Markdown.", extension);
            return DocumentTextResult.NotIndexable(extension == ".pdf" ? MensajePdfDanado : MensajeDocxDanado);
        }
    }

    private static DocumentTextResult FromPdf(byte[] bytes, CancellationToken cancellationToken)
    {
        var pdf = PdfMarkdownConverter.Convert(bytes, cancellationToken);

        if (!pdf.HasText)
            return DocumentTextResult.NotIndexable(pdf.HasImages ? MensajePdfEscaneado : MensajePdfSinTexto);

        return FromMarkdown(pdf.Markdown);
    }

    private static DocumentTextResult FromMarkdown(string markdown)
    {
        string normalized = Normalize(markdown);
        return normalized.Length == 0
            ? DocumentTextResult.NotIndexable(MensajeArchivoVacio)
            : DocumentTextResult.Indexed(normalized);
    }

    private static string DecodeUtf8(byte[] bytes)
    {
        // La subida ya validó que el archivo es UTF-8; aquí solo se quita el BOM si lo trae.
        var preamble = Encoding.UTF8.GetPreamble();
        int offset = bytes.AsSpan().StartsWith(preamble) ? preamble.Length : 0;
        return Encoding.UTF8.GetString(bytes, offset, bytes.Length - offset);
    }

    /// <summary>
    /// Deja el Markdown compacto: saltos de línea uniformes, sin espacios al final de línea
    /// y como máximo una línea en blanco seguida. Cada carácter de más son tokens pagados.
    /// </summary>
    internal static string Normalize(string text)
    {
        string unified = text.Replace("\r\n", "\n").Replace('\r', '\n').Replace(' ', ' ');
        string noTrailing = TrailingSpaces().Replace(unified, string.Empty);
        return ExtraBlankLines().Replace(noTrailing, "\n\n").Trim();
    }

    [GeneratedRegex(@"[ \t]+(?=\n|$)")]
    private static partial Regex TrailingSpaces();

    [GeneratedRegex(@"\n{3,}")]
    private static partial Regex ExtraBlankLines();
}
