using KnovaHub.ApplicationLayer.Contract;
using KnovaHub.ApplicationLayer.DTOs;
using KnovaHub.API.Authorization;
using KnovaHub.DomainLayer.Exceptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KnovaHub.API.Controllers;

[ApiController]
[Route("api/ai")]
[Authorize]
public class AiController : ControllerBase
{
    private readonly IQueryService _queryService;

    public AiController(IQueryService queryService)
    {
        _queryService = queryService;
    }

    private Actor? CurrentActor => User.ToActor();

    private bool TryGetActor(out Actor actor)
    {
        var current = CurrentActor;

        if (current == null)
        {
            actor = null!;
            return false;
        }

        actor = current;
        return true;
    }

    [HttpPost("ask")]
    public async Task<ActionResult<AiAskResponseDto>> Ask([FromBody] AiAskRequestDto request)
    {
        if (!TryGetActor(out var actor))
            return Unauthorized();

        if (string.IsNullOrWhiteSpace(request.Question))
            return BadRequest("La pregunta es obligatoria.");

        var response = await _queryService.AskAsync(actor, request.Question);

        return Ok(response);
    }
}
