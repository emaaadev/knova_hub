using KnovaHub.ApplicationLayer.Contract;
using KnovaHub.ApplicationLayer.DTOs;
using KnovaHub.DomainLayer.Entities;
using KnovaHub.DomainLayer.Repository;
using KnovaHub.DomainLayer.Exceptions;

namespace KnovaHub.ApplicationLayer.Services;

public class UserRoleService : IUserRoleService
{
    public const string MensajeQuitarsePropioRol = "No puedes quitarte tu propio rol de AdminEmpresa.";
    public const string MensajeUltimoAdmin = "La empresa debe mantener al menos un AdminEmpresa activo.";

    private static readonly Dictionary<int, string> NombresDeRol = new()
    {
        [SystemRoles.AdminEmpresaRoleId] = SystemRoles.AdminEmpresaRoleName,
        [SystemRoles.SupervisorRoleId] = SystemRoles.SupervisorRoleName,
        [SystemRoles.UsuarioRoleId] = SystemRoles.UsuarioRoleName,
        [SystemRoles.AdminPlataformaRoleId] = SystemRoles.AdminPlataformaRoleName
    };

    private readonly IUserRepository _users;
    private readonly IDepartmentRepository _departments;

    public UserRoleService(IUserRepository users, IDepartmentRepository departments)
    {
        _users = users;
        _departments = departments;
    }

    public async Task<UserRoleDto> GetUserAsync(int companyId, int userId)
    {
        return ToDto(await FindAsync(companyId, userId));
    }

    public async Task<UserRoleDto> ChangeRoleAsync(Actor actor, int userId, ChangeRoleDto dto)
    {
        if (actor.CompanyId is not int companyId)
            throw new NotFoundException("El usuario no existe en tu empresa.");

        if (!SystemRoles.AsignablesPorEmpresa.Contains(dto.RoleId))
            throw new DataValidationException(
                "Rol no válido. Solo se puede asignar AdminEmpresa, Supervisor o Usuario.");

        var user = await FindAsync(companyId, userId);

        if (user.Id == actor.UserId && dto.RoleId != SystemRoles.AdminEmpresaRoleId)
            throw new BusinessRuleException(MensajeQuitarsePropioRol);

        // Cada Usuario pertenece a un departamento; los demás roles no llevan departamento propio.
        int? departmentId = null;
        if (dto.RoleId == SystemRoles.UsuarioRoleId)
        {
            if (dto.DepartmentId is not int id)
                throw new DataValidationException("Para el rol Usuario hay que indicar el departamento.");

            await EnsureActiveDepartmentAsync(companyId, id);
            departmentId = id;
        }

        var guardado = await _users.ChangeRoleKeepingCompanyAdminAsync(companyId, userId, dto.RoleId, departmentId);
        if (!guardado)
            throw new BusinessRuleException(MensajeUltimoAdmin);

        user.RoleId = dto.RoleId;
        user.DepartmentId = departmentId;
        user.Role = null;
        return ToDto(user);
    }

    public async Task<UserRoleDto> ChangeDepartmentAsync(int companyId, int userId, int departmentId)
    {
        var user = await FindAsync(companyId, userId);

        if (user.RoleId != SystemRoles.UsuarioRoleId)
            throw new DataValidationException("Solo los usuarios con rol Usuario pertenecen a un departamento.");

        await EnsureActiveDepartmentAsync(companyId, departmentId);
        await _users.UpdateDepartmentAsync(userId, departmentId);

        user.DepartmentId = departmentId;
        return ToDto(user);
    }

    private async Task<User> FindAsync(int companyId, int userId)
    {
        return await _users.GetByIdInCompanyAsync(companyId, userId)
            ?? throw new NotFoundException("El usuario no existe en tu empresa.");
    }

    private async Task EnsureActiveDepartmentAsync(int companyId, int departmentId)
    {
        var department = await _departments.GetByIdAsync(companyId, departmentId);
        if (department is null || !department.IsActive)
            throw new DataValidationException("El departamento no existe en tu empresa o está inactivo.");
    }

    private static UserRoleDto ToDto(User u) => new()
    {
        Id = u.Id,
        FullName = u.FullName,
        Email = u.Email,
        IsActive = u.IsActive,
        RoleId = u.RoleId,
        Role = u.Role?.Name ?? NombresDeRol.GetValueOrDefault(u.RoleId, string.Empty),
        DepartmentId = u.DepartmentId
    };
}
