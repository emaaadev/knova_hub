using KnovaHub.API.Authorization;
using KnovaHub.ApplicationLayer.Contract;
using KnovaHub.ApplicationLayer.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KnovaHub.API.Controllers;

/// <summary>
/// Solicitudes de alta de empleados. El Supervisor las crea; el AdminEmpresa las aprueba o rechaza.
/// </summary>
[ApiController]
[Route("api/users/requests")]
[Authorize(Policy = Policies.GestionUsuarios)]
public class RegistrationRequestsController : ControllerBase
{
    private readonly IRegistrationRequestService _requests;

    public RegistrationRequestsController(IRegistrationRequestService requests)
    {
        _requests = requests;
    }

    private Actor Actor => User.ToActor()!;

    /// <summary>AdminEmpresa: solicitudes de su empresa. Supervisor: sus propias solicitudes.</summary>
    [HttpGet]
    public async Task<ActionResult<List<RegistrationRequestDto>>> GetAll()
    {
        return Ok(await _requests.GetVisibleAsync(Actor));
    }

    [HttpPost]
    [Authorize(Policy = Policies.Supervisor)]
    public async Task<ActionResult<RegistrationRequestDto>> Create(CreateRegistrationRequestDto dto)
    {
        var created = await _requests.CreateAsync(Actor, dto);
        return StatusCode(StatusCodes.Status201Created, created);
    }

    [HttpPost("{id:int}/approve")]
    [Authorize(Policy = Policies.AdminEmpresa)]
    public async Task<ActionResult<ApprovedRequestDto>> Approve(int id)
    {
        return Ok(await _requests.ApproveAsync(Actor, id));
    }

    [HttpPost("{id:int}/reject")]
    [Authorize(Policy = Policies.AdminEmpresa)]
    public async Task<ActionResult<RegistrationRequestDto>> Reject(int id)
    {
        return Ok(await _requests.RejectAsync(Actor, id));
    }
}
