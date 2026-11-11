using KnovaHub.API.Authorization;
using KnovaHub.ApplicationLayer.Contract;
using KnovaHub.ApplicationLayer.DTOs;
using KnovaHub.DomainLayer.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KnovaHub.API.Controllers;

[ApiController]
[Route("api/dashboard")]
[Authorize(Roles = SystemRoles.AdminEmpresaRoleName + "," +
    SystemRoles.SupervisorRoleName + "," + SystemRoles.UsuarioRoleName)]
public class DashboardController(IDocumentService documents, IUserService users) : ControllerBase
{
    [HttpGet("stats")]
    public async Task<ActionResult<DashboardDto>> GetStats()
    {
        if (User.ToActor() is not { CompanyId: > 0 } actor)
            return Forbid();

        var summary = await documents.GetSummaryAsync(actor);
        int? userCount = actor.Role == SystemRoles.UsuarioRoleName
            ? null
            : (await users.GetVisibleAsync(actor)).Count;

        var activity = summary.RecentActivity
            .Select(d => new DashboardActivityDto(
                d.Id, d.Title, d.Type,
                DateTime.SpecifyKind(d.UpdatedAt ?? d.CreatedAt, DateTimeKind.Utc),
                d.UpdatedAt.HasValue))
            .ToList();

        return Ok(new DashboardDto(
            summary.Documents, summary.Wikis, summary.Minutes, userCount, activity));
    }
}
