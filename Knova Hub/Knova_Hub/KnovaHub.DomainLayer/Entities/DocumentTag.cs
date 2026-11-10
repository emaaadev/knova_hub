namespace KnovaHub.DomainLayer.Entities;

/// <summary>
/// Entidad de unión entre documentos y etiquetas.
/// CompanyId permite garantizar el aislamiento multiempresa.
/// </summary>
public class DocumentTag
{
    public int DocumentId { get; set; }
    public Document? Document { get; set; }

    public int TagId { get; set; }
    public Tag? Tag { get; set; }

    public int CompanyId { get; set; }
}