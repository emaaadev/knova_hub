namespace KnovaHub.DomainLayer.Entities;

/// <summary>
/// Texto del archivo adjunto convertido a Markdown. Es lo que lee la IA en lugar del archivo original.
/// Vive en su propia tabla para que los listados de documentos no carguen el texto completo.
/// Solo existe cuando el documento está Indexado.
/// </summary>
public class DocumentText
{
    public int DocumentId { get; set; }
    public int CompanyId { get; set; }
    public Document? Document { get; set; }

    public string Markdown { get; set; } = string.Empty;
    public int CharacterCount { get; set; }
    public DateTime ProcessedAt { get; set; } = DateTime.UtcNow;
}
