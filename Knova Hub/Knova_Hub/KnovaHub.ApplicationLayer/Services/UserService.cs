using System.ComponentModel.DataAnnotations;
using KnovaHub.ApplicationLayer.Contract;
using KnovaHub.ApplicationLayer.DTOs;
using KnovaHub.DomainLayer.Entities;
using KnovaHub.DomainLayer.Exceptions;
using KnovaHub.DomainLayer.Repository;

namespace KnovaHub.ApplicationLayer.Services;

public class UserService : IUserService
{
    public const string MensajeDesactivarseASiMismo = "No puedes desactivarte a ti mismo.";

    private static readonly Dictionary<int, string> NombresDeRol = new()
    {
        [SystemRoles.AdminEmpresaRoleId] = SystemRoles.AdminEmpresaRoleName,
        [SystemRoles.SupervisorRoleId] = SystemRoles.SupervisorRoleName,
        [SystemRoles.UsuarioRoleId] = SystemRoles.UsuarioRoleName,
        [SystemRoles.AdminPlataformaRoleId] = SystemRoles.AdminPlataformaRoleName
    };

    private readonly IUserRepository _users;
    private readonly IDepartmentRepository _departments;

    public UserService(IUserRepository users, IDepartmentRepository departments)
    {
        _users = users;
        _departments = departments;
    }

    public async Task<List<UserDto>> GetVisibleAsync(Actor actor)
    {
        if (actor.CompanyId is not int companyId)
            return new List<UserDto>();

        var usuarios = actor.Role switch
        {
            SystemRoles.AdminEmpresaRoleName => await _users.GetByCompanyAsync(companyId),
            SystemRoles.SupervisorRoleName => await SupervisorUsersAsync(actor.UserId),
            _ => new List<User>()
        };

        return usuarios.Select(ToDto).ToList();
    }

    public async Task<UserDto> GetAsync(Actor actor, int userId)
    {
        if (actor.CompanyId is not int companyId)
            throw new NotFoundException("El usuario no existe en tu empresa.");

        var user = await _users.GetByIdInCompanyAsync(companyId, userId)
            ?? throw new NotFoundException("El usuario no existe en tu empresa.");

        if (actor.Role == SystemRoles.SupervisorRoleName && !await EsDelSupervisorAsync(actor.UserId, user))
            throw new NotFoundException("El usuario no existe en tu empresa.");

        return ToDto(user);
    }

    public async Task<CreatedUserDto> CreateAsync(int companyId, CreateUserDto dto)
    {
        var fullName = ValidFullName(dto.FullName);
        var email = ValidEmail(dto.Email);

        if (!SystemRoles.AsignablesPorEmpresa.Contains(dto.RoleId))
            throw new DataValidationException("Rol no válido. Solo se puede asignar AdminEmpresa, Supervisor o Usuario.");

        if (await _users.EmailExistsAsync(email))
            throw new DuplicateEntityException("email", "Ese correo ya está registrado.");

        int? departmentId = null;
        if (dto.RoleId == SystemRoles.UsuarioRoleId)
        {
            if (dto.DepartmentId is not int id)
                throw new DataValidationException("Para el rol Usuario hay que indicar el departamento.");

            await EnsureActiveDepartmentAsync(companyId, id);
            departmentId = id;
        }

        var temporaryPassword = PasswordPolicy.GenerateTemporary();

        var user = new User
        {
            CompanyId = companyId,
            FullName = fullName,
            Email = email,
            RoleId = dto.RoleId,
            DepartmentId = departmentId,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(temporaryPassword),
            IsActive = true,
            MustChangePassword = true
        };

        var created = await _users.AddAsync(user);

        return new CreatedUserDto
        {
            User = ToDto(created),
            TemporaryPassword = temporaryPassword
        };
    }

    public async Task<UserDto> UpdateAsync(int companyId, int userId, UpdateUserDto dto)
    {
        var user = await FindAsync(companyId, userId);

        var email = ValidEmail(dto.Email);
        if (!string.Equals(email, user.Email, StringComparison.OrdinalIgnoreCase)
            && await _users.EmailExistsAsync(email))
            throw new DuplicateEntityException("email", "Ese correo ya está registrado.");

        user.FullName = ValidFullName(dto.FullName);
        user.Email = email;

        await _users.UpdateAsync(user);
        return ToDto(user);
    }

    public async Task<UserDto> SetActiveAsync(Actor actor, int userId, bool isActive)
    {
        if (actor.CompanyId is not int companyId)
            throw new NotFoundException("El usuario no existe en tu empresa.");

        var user = await FindAsync(companyId, userId);

        if (!isActive && actor.UserId == userId)
            throw new BusinessRuleException(MensajeDesactivarseASiMismo);

        user.IsActive = isActive;
        await _users.UpdateAsync(user);
        return ToDto(user);
    }

    private async Task<User> FindAsync(int companyId, int userId)
    {
        return await _users.GetByIdInCompanyAsync(companyId, userId)
            ?? throw new NotFoundException("El usuario no existe en tu empresa.");
    }

    private async Task<List<User>> SupervisorUsersAsync(int supervisorId)
    {
        var departamentos = await _departments.GetSupervisedAsync(supervisorId);
        if (departamentos.Count == 0)
            return new List<User>();

        return await _users.GetByDepartmentIdsAsync(departamentos.Select(d => d.Id).ToList());
    }

    private async Task<bool> EsDelSupervisorAsync(int supervisorId, User user)
    {
        if (user.DepartmentId is not int departmentId)
            return false;

        return await _departments.IsSupervisorOfAsync(supervisorId, departmentId);
    }

    private async Task EnsureActiveDepartmentAsync(int companyId, int departmentId)
    {
        var department = await _departments.GetByIdAsync(companyId, departmentId);
        if (department is null || !department.IsActive)
            throw new DataValidationException("El departamento no existe en tu empresa o está inactivo.");
    }

    private static string ValidFullName(string? name)
    {
        var value = name?.Trim() ?? string.Empty;
        if (value.Length == 0)
            throw new DataValidationException("fullName", "El nombre es obligatorio.");
        if (value.Length > 100)
            throw new DataValidationException("fullName", "El nombre no puede tener más de 100 caracteres.");
        return value;
    }

    private static string ValidEmail(string? email)
    {
        var value = email?.Trim().ToLowerInvariant() ?? string.Empty;
        if (value.Length == 0 || !new EmailAddressAttribute().IsValid(value))
            throw new DataValidationException("email", "El correo electrónico debe tener un formato válido.");
        return value;
    }

    private static UserDto ToDto(User u) => new()
    {
        Id = u.Id,
        FullName = u.FullName,
        Email = u.Email,
        IsActive = u.IsActive,
        RoleId = u.RoleId,
        Role = u.Role?.Name ?? NombresDeRol.GetValueOrDefault(u.RoleId, string.Empty),
        DepartmentId = u.DepartmentId,
        DepartmentName = u.Department?.Name
    };
}
