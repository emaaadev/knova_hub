
namespace KnovaHub.ApplicationLayer.DTOs;

/// <summary>
/// Información de un archivo que se desea adjuntar
/// a un documento existente.
/// </summary>
public class UploadDocumentFileDto
{
    public string FileName { get; set; } = string.Empty;

    public string ContentType { get; set; } = string.Empty;

    public long FileSizeBytes { get; set; }

    public Stream Content { get; set; } = Stream.Null;
}

/// <summary>
/// Resultado de una descarga autorizada.
/// El consumidor debe liberar el Stream después de utilizarlo.
/// </summary>
public class DownloadDocumentFileDto
{
    public string FileName { get; set; } = string.Empty;

    public string ContentType { get; set; } = string.Empty;

    public Stream Content { get; set; } = Stream.Null;
}
