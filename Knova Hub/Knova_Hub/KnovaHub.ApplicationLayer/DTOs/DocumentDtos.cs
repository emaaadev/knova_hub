
using KnovaHub.DomainLayer.Entities;

namespace KnovaHub.ApplicationLayer.DTOs;

/// <summary>
/// Información de un documento devuelta al cliente.
/// No expone la ruta física ni la clave interna del archivo.
/// </summary>
public class DocumentDto
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Content { get; set; }

    public DocumentType Type { get; set; }
    public DocumentVisibility Visibility { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public int AuthorId { get; set; }
    public string AuthorName { get; set; } = string.Empty;

    public int? CategoryId { get; set; }
    public string? CategoryName { get; set; }

    public int? DepartmentId { get; set; }
    public string? DepartmentName { get; set; }

    public List<TagDto> Tags { get; set; } = new();

    public string? OriginalFileName { get; set; }
    public string? ContentType { get; set; }
    public string? FileExtension { get; set; }
    public long? FileSizeBytes { get; set; }

    public bool HasFile => !string.IsNullOrWhiteSpace(OriginalFileName);
}

/// <summary>Datos para crear un documento.</summary>
public class CreateDocumentDto
{
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Content { get; set; }

    public DocumentType Type { get; set; }
    public DocumentVisibility Visibility { get; set; }

    public int? CategoryId { get; set; }
    public int? DepartmentId { get; set; }

    public List<int> TagIds { get; set; } = new();
}

/// <summary>Datos para actualizar un documento.</summary>
public class UpdateDocumentDto
{
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Content { get; set; }

    public DocumentType Type { get; set; }
    public DocumentVisibility Visibility { get; set; }

    public int? CategoryId { get; set; }
    public int? DepartmentId { get; set; }

    public List<int> TagIds { get; set; } = new();
}

/// <summary>Filtros disponibles para consultar documentos.</summary>
public class DocumentFilterDto
{
    public DocumentType? Type { get; set; }
    public int? CategoryId { get; set; }
    public int? TagId { get; set; }
    public int? DepartmentId { get; set; }
    public string? Search { get; set; }
}
