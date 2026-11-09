using KnovaHub.ApplicationLayer.Contract;
using KnovaHub.ApplicationLayer.DTOs;
using KnovaHub.DomainLayer.Entities;
using KnovaHub.DomainLayer.Repository;
using KnovaHub.DomainLayer.Exceptions;

namespace KnovaHub.ApplicationLayer.Services;

public class DepartmentService : IDepartmentService
{
    private readonly IDepartmentRepository _departments;
    private readonly IUserRepository _users;

    public DepartmentService(IDepartmentRepository departments, IUserRepository users)
    {
        _departments = departments;
        _users = users;
    }

    public async Task<List<DepartmentDto>> GetVisibleAsync(Actor actor)
    {
        if (actor.CompanyId is not int companyId)
            return new List<DepartmentDto>();

        var lista = actor.Role switch
        {
            SystemRoles.AdminEmpresaRoleName => await _departments.GetByCompanyAsync(companyId),
            SystemRoles.SupervisorRoleName => await _departments.GetSupervisedAsync(actor.UserId),
            _ => new List<Department>()
        };

        return lista.Select(ToDto).ToList();
    }

    public async Task<DepartmentDto> GetAsync(int companyId, int departmentId)
    {
        return ToDto(await FindAsync(companyId, departmentId));
    }

    public async Task<DepartmentDto> CreateAsync(int companyId, CreateDepartmentDto dto)
    {
        var department = new Department
        {
            CompanyId = companyId,
            Name = ValidName(dto.Name)
        };

        return ToDto(await _departments.AddAsync(department));
    }

    public async Task<DepartmentDto> UpdateAsync(int companyId, int departmentId, UpdateDepartmentDto dto)
    {
        var department = await FindAsync(companyId, departmentId);
        department.Name = ValidName(dto.Name);
        department.IsActive = dto.IsActive ?? department.IsActive;

        await _departments.UpdateAsync(department);
        return ToDto(department);
    }

    public async Task<List<DepartmentDto>> SetSupervisorDepartmentsAsync(
        int companyId, int supervisorId, AssignSupervisorDepartmentsDto dto)
    {
        var supervisor = await _users.GetByIdInCompanyAsync(companyId, supervisorId)
            ?? throw new NotFoundException("El usuario no existe en tu empresa.");

        if (supervisor.RoleId != SystemRoles.SupervisorRoleId)
            throw new DataValidationException("Solo se pueden asignar departamentos a un usuario con rol Supervisor.");

        var ids = dto.DepartmentIds.Distinct().ToList();

        // Todos los departamentos tienen que ser de la misma empresa y estar activos.
        var deLaEmpresa = (await _departments.GetByCompanyAsync(companyId))
            .Where(d => d.IsActive)
            .ToDictionary(d => d.Id);

        var invalidos = ids.Where(id => !deLaEmpresa.ContainsKey(id)).ToList();
        if (invalidos.Count > 0)
            throw new DataValidationException(
                $"Estos departamentos no existen en tu empresa o están inactivos: {string.Join(", ", invalidos)}.");

        await _departments.SetSupervisorDepartmentsAsync(supervisorId, ids);

        return ids.Select(id => ToDto(deLaEmpresa[id])).OrderBy(d => d.Name).ToList();
    }

    private async Task<Department> FindAsync(int companyId, int departmentId)
    {
        return await _departments.GetByIdAsync(companyId, departmentId)
            ?? throw new NotFoundException("El departamento no existe en tu empresa.");
    }

    private static string ValidName(string? name)
    {
        var value = name?.Trim() ?? string.Empty;
        if (value.Length == 0)
            throw new DataValidationException("El nombre del departamento es obligatorio.");
        if (value.Length > 100)
            throw new DataValidationException("El nombre del departamento no puede tener más de 100 caracteres.");
        return value;
    }

    private static DepartmentDto ToDto(Department d) => new()
    {
        Id = d.Id,
        Name = d.Name,
        IsActive = d.IsActive,
        CreatedAt = d.CreatedAt
    };
}
