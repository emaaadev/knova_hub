using KnovaHub.ApplicationLayer.DTOs;

namespace KnovaHub.ApplicationLayer.Contract;

public interface IUserService
{
    /// <summary>AdminEmpresa: todos los de su empresa. Supervisor: solo los de sus departamentos.</summary>
    Task<List<UserDto>> GetVisibleAsync(Actor actor);

    /// <summary>AdminEmpresa ve cualquier usuario de su empresa; Supervisor solo a los de sus departamentos.</summary>
    Task<UserDto> GetAsync(Actor actor, int userId);

    /// <summary>El AdminEmpresa crea un usuario directamente. Genera una contraseña temporal.</summary>
    Task<CreatedUserDto> CreateAsync(int companyId, CreateUserDto dto);

    Task<UserDto> UpdateAsync(int companyId, int userId, UpdateUserDto dto);

    Task<UserDto> SetActiveAsync(Actor actor, int userId, bool isActive);
}
