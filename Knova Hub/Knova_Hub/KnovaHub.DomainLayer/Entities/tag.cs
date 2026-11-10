namespace KnovaHub.DomainLayer.Entities;

/// <summary>Etiqueta de conocimiento administrada dentro de una empresa.</summary>
public class Tag
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public int CompanyId { get; set; }
    public Company? Company { get; set; }

    public ICollection<DocumentTag> DocumentTags { get; set; } = new List<DocumentTag>();
}