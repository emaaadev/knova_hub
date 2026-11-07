using KnovaHub.DomainLayer.Entities;
using KnovaHub.DomainLayer.Repository;
using Microsoft.AspNetCore.Authorization;

namespace KnovaHub.API.Authorization;

/// <summary>Departamento sobre el que se quiere actuar (recurso de la política AccesoDepartamento).</summary>
public record DepartmentResource(int DepartmentId);

public class DepartmentAccessRequirement : IAuthorizationRequirement
{
}

/// <summary>
/// AdminEmpresa: pasa si el departamento es de su empresa.
/// Supervisor: pasa solo si el departamento lo tiene asignado (se consulta en la BD en cada petición,
/// así que si le quitan un departamento pierde el acceso al momento).
/// Cualquier otro caso no pasa y la API responde 403.
/// </summary>
public class DepartmentAccessHandler : AuthorizationHandler<DepartmentAccessRequirement, DepartmentResource>
{
    private readonly IDepartmentRepository _departments;

    public DepartmentAccessHandler(IDepartmentRepository departments)
    {
        _departments = departments;
    }

    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        DepartmentAccessRequirement requirement,
        DepartmentResource resource)
    {
        var actor = context.User.ToActor();
        if (actor is null || actor.CompanyId is not int companyId)
            return;

        var permitido = actor.Role switch
        {
            SystemRoles.AdminEmpresaRoleName =>
                await _departments.GetByIdAsync(companyId, resource.DepartmentId) is not null,
            SystemRoles.SupervisorRoleName =>
                await _departments.IsSupervisorOfAsync(actor.UserId, resource.DepartmentId),
            _ => false
        };

        if (permitido)
            context.Succeed(requirement);
    }
}
