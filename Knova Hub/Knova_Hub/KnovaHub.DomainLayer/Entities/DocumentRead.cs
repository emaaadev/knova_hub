namespace KnovaHub.DomainLayer.Entities;

public class DocumentRead
{
    public long Id { get; set; }
    public int CompanyId { get; set; }
    public int UserId { get; set; }
    public int DocumentId { get; set; }
    public DateTime ReadAt { get; set; }
}
