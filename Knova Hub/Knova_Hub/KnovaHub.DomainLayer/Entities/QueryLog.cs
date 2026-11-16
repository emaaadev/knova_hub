namespace KnovaHub.DomainLayer.Entities;

public class QueryLog
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public int CompanyId { get; set; }
    public string Question { get; set; } = string.Empty;
    public string DocumentIds { get; set; } = string.Empty;
    public DateTime AskedAt { get; set; }

}