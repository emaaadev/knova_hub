using KnovaHub.API.Authorization;
using KnovaHub.ApplicationLayer.Contract;
using KnovaHub.ApplicationLayer.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KnovaHub.API.Controllers;

/// <summary>
/// Módulo Usuarios. El AdminEmpresa lista, crea, ve, edita y activa/desactiva usuarios de su empresa;
/// el Supervisor solo ve a los usuarios de sus departamentos. El cambio de rol y de departamento
/// vive en <see cref="UserRolesController"/>.
/// </summary>
[ApiController]
[Route("api/users")]
[Authorize(Policy = Policies.GestionUsuarios)]
public class UsersController : ControllerBase
{
    private readonly IUserService _users;

    public UsersController(IUserService users)
    {
        _users = users;
    }

    private Actor Actor => User.ToActor()!;

    /// <summary>AdminEmpresa: todos los de su empresa. Supervisor: solo los de sus departamentos.</summary>
    [HttpGet]
    public async Task<ActionResult<List<UserDto>>> GetAll()
    {
        return Ok(await _users.GetVisibleAsync(Actor));
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<UserDto>> Get(int id)
    {
        return Ok(await _users.GetAsync(Actor, id));
    }

    [HttpPost]
    [Authorize(Policy = Policies.AdminEmpresa)]
    public async Task<ActionResult<CreatedUserDto>> Create(CreateUserDto dto)
    {
        var created = await _users.CreateAsync(Actor.CompanyId!.Value, dto);
        return CreatedAtAction(nameof(Get), new { id = created.User.Id }, created);
    }

    [HttpPut("{id:int}")]
    [Authorize(Policy = Policies.AdminEmpresa)]
    public async Task<ActionResult<UserDto>> Update(int id, UpdateUserDto dto)
    {
        return Ok(await _users.UpdateAsync(Actor.CompanyId!.Value, id, dto));
    }

    [HttpPatch("{id:int}/active")]
    [Authorize(Policy = Policies.AdminEmpresa)]
    public async Task<ActionResult<UserDto>> SetActive(int id, SetUserActiveDto dto)
    {
        return Ok(await _users.SetActiveAsync(Actor, id, dto.IsActive));
    }
}
