using KnovaHub.ApplicationLayer.DTOs;


namespace KnovaHub.ApplicationLayer.Contract;

public interface IQueryService
{
    Task<AiAskResponseDto> AskAsync(Actor actor, string question);
}