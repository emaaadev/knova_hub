namespace KnovaHub.DomainLayer.Entities;

/// <summary>Categoría de conocimiento administrada dentro de una empresa.</summary>
public class Category
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    public int CompanyId { get; set; }
    public Company? Company { get; set; }

    public ICollection<Document> Documents { get; set; } = new List<Document>();
}