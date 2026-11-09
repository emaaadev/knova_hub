using KnovaHub.API.Authorization;
using KnovaHub.ApplicationLayer.Contract;
using KnovaHub.ApplicationLayer.DTOs;
using KnovaHub.DomainLayer.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KnovaHub.API.Controllers;

/// <summary>
/// Rol y departamento de un usuario. La gestión general de usuarios (alta, listado, activar)
/// es del módulo Usuarios; aquí solo va lo de roles y departamentos.
/// </summary>
[ApiController]
[Route("api/users/{userId:int}")]
public class UserRolesController : ControllerBase
{
    private readonly IUserRoleService _roles;
    private readonly IAuthorizationService _authorization;

    public UserRolesController(IUserRoleService roles, IAuthorizationService authorization)
    {
        _roles = roles;
        _authorization = authorization;
    }

    private Actor Actor => User.ToActor()!;

    [HttpPut("role")]
    [Authorize(Policy = Policies.AdminEmpresa)]
    public async Task<ActionResult<UserRoleDto>> ChangeRole(int userId, ChangeRoleDto dto)
    {
        return Ok(await _roles.ChangeRoleAsync(Actor, userId, dto));
    }

    /// <summary>
    /// Mueve a un Usuario de departamento. El Supervisor solo puede hacerlo si supervisa
    /// tanto el departamento actual del usuario como el nuevo.
    /// </summary>
    [HttpPut("department")]
    [Authorize(Policy = Policies.GestionDepartamentos)]
    public async Task<ActionResult<UserRoleDto>> ChangeDepartment(int userId, ChangeUserDepartmentDto dto)
    {
        var companyId = Actor.CompanyId!.Value;
        var target = await _roles.GetUserAsync(companyId, userId);

        if (!await PuedeActuarSobre(dto.DepartmentId))
            return Forbid();

        if (target.DepartmentId is int actual)
        {
            if (!await PuedeActuarSobre(actual))
                return Forbid();
        }
        else if (Actor.Role != SystemRoles.AdminEmpresaRoleName)
        {
            // Un usuario sin departamento no es de ningún Supervisor.
            return Forbid();
        }

        return Ok(await _roles.ChangeDepartmentAsync(companyId, userId, dto.DepartmentId));
    }

    private async Task<bool> PuedeActuarSobre(int departmentId)
    {
        var result = await _authorization.AuthorizeAsync(
            User, new DepartmentResource(departmentId), Policies.AccesoDepartamento);
        return result.Succeeded;
    }
}
