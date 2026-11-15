using KnovaHub.ApplicationLayer.DTOs;

namespace KnovaHub.ApplicationLayer.Contract;

/// <summary>
/// Convierte el archivo adjunto de un documento (PDF, DOCX, TXT o MD) a Markdown,
/// que es el formato que lee la IA. La implementación vive en Infrastructure.
/// </summary>
public interface IDocumentTextExtractor
{
    /// <summary>
    /// Nunca lanza por un archivo escaneado, vacío o dañado: en esos casos devuelve
    /// un resultado no indexable con el motivo. Solo lanza si se cancela la operación.
    /// </summary>
    Task<DocumentTextResult> ExtractAsync(
        Stream content,
        string fileExtension,
        CancellationToken cancellationToken = default);
}
