using KnovaHub.ApplicationLayer.Services;
using KnovaHub.DomainLayer.Entities;
using Microsoft.AspNetCore.Authorization;

namespace KnovaHub.API.Authorization;

/// <summary>
/// Políticas de autorización de la API. La matriz completa está en docs/matriz-de-permisos.md.
/// Si el usuario está autenticado pero no cumple la política, la API responde 403.
/// </summary>
public static class Policies
{
    /// <summary>Solo AdminPlataforma (rol creado por seed; todavía no tiene panel).</summary>
    public const string AdminPlataforma = nameof(AdminPlataforma);

    /// <summary>Solo AdminEmpresa de una empresa.</summary>
    public const string AdminEmpresa = nameof(AdminEmpresa);

    /// <summary>AdminEmpresa o Supervisor de una empresa.</summary>
    public const string GestionDepartamentos = nameof(GestionDepartamentos);

    /// <summary>
    /// Por recurso (<see cref="DepartmentResource"/>): AdminEmpresa sobre cualquier departamento de su empresa,
    /// Supervisor solo sobre los departamentos que tiene asignados.
    /// </summary>
    public const string AccesoDepartamento = nameof(AccesoDepartamento);

    public static IServiceCollection AddKnovaAuthorization(this IServiceCollection services)
    {
        services.AddScoped<IAuthorizationHandler, DepartmentAccessHandler>();

        services.AddAuthorization(options =>
        {
            options.AddPolicy(AdminPlataforma, p => p
                .RequireAuthenticatedUser()
                .RequireRole(SystemRoles.AdminPlataformaRoleName));

            options.AddPolicy(AdminEmpresa, p => p
                .RequireAuthenticatedUser()
                .RequireRole(SystemRoles.AdminEmpresaRoleName)
                .RequireClaim(KnovaClaims.CompanyId));

            options.AddPolicy(GestionDepartamentos, p => p
                .RequireAuthenticatedUser()
                .RequireRole(SystemRoles.AdminEmpresaRoleName, SystemRoles.SupervisorRoleName)
                .RequireClaim(KnovaClaims.CompanyId));

            options.AddPolicy(AccesoDepartamento, p => p
                .RequireAuthenticatedUser()
                .AddRequirements(new DepartmentAccessRequirement()));
        });

        return services;
    }
}
