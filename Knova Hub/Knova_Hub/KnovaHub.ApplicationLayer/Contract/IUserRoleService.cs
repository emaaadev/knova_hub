using KnovaHub.ApplicationLayer.DTOs;

namespace KnovaHub.ApplicationLayer.Contract;

public interface IUserRoleService
{
    Task<UserRoleDto> GetUserAsync(int companyId, int userId);

    /// <summary>
    /// Cambia el rol de un usuario de la empresa del actor. Reglas: no se asigna AdminPlataforma,
    /// el AdminEmpresa no puede quitarse su propio rol y la empresa mantiene al menos un AdminEmpresa activo.
    /// </summary>
    Task<UserRoleDto> ChangeRoleAsync(Actor actor, int userId, ChangeRoleDto dto);

    /// <summary>Mueve a un Usuario a otro departamento de la misma empresa.</summary>
    Task<UserRoleDto> ChangeDepartmentAsync(int companyId, int userId, int departmentId);
}
