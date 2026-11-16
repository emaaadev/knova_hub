namespace KnovaHub.ApplicationLayer.DTOs;

public class AiSourceDto
{
    public int DocumentId { get; set; }
    public string Title { get; set; } = string.Empty;
}
public class AiAskResponseDto
{
    public string Answer { get; set; } = string.Empty;
    public List<AiSourceDto> Sources {get; set;} = new();
}