
namespace KnovaHub.DomainLayer.Entities;

/// <summary>
/// Recurso de conocimiento perteneciente a una empresa. Un documento General no tiene
/// DepartmentId; uno Departamental debe pertenecer a un departamento de la misma empresa.
/// </summary>
public class Document
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Content { get; set; }

    public DocumentType Type { get; set; }
    public DocumentVisibility Visibility { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    public int CompanyId { get; set; }
    public Company? Company { get; set; }

    public int AuthorId { get; set; }
    public User? Author { get; set; }

    public int? CategoryId { get; set; }
    public Category? Category { get; set; }

    public int? DepartmentId { get; set; }
    public Department? Department { get; set; }

    // Metadatos del archivo adjunto.
    // Son null cuando el recurso no tiene archivo.
    // StorageKey es una clave generada por el servidor.
    public string? OriginalFileName { get; set; }
    public string? StorageKey { get; set; }
    public string? ContentType { get; set; }
    public string? FileExtension { get; set; }
    public long? FileSizeBytes { get; set; }

    public ICollection<DocumentTag> DocumentTags { get; set; }
        = new List<DocumentTag>();
}
