using KnovaHub.ApplicationLayer.DTOs;

namespace KnovaHub.ApplicationLayer.Contract;

public interface IDepartmentService
{
    /// <summary>AdminEmpresa: todos los de su empresa. Supervisor: solo los suyos. Otros roles: ninguno.</summary>
    Task<List<DepartmentDto>> GetVisibleAsync(Actor actor);

    Task<DepartmentDto> GetAsync(int companyId, int departmentId);
    Task<DepartmentDto> CreateAsync(int companyId, CreateDepartmentDto dto);
    Task<DepartmentDto> UpdateAsync(int companyId, int departmentId, UpdateDepartmentDto dto);

    Task<List<DepartmentDto>> SetSupervisorDepartmentsAsync(int companyId, int supervisorId, AssignSupervisorDepartmentsDto dto);

    /// <summary>Departamentos asignados a un Supervisor (para el AdminEmpresa, en la pantalla de gestión).</summary>
    Task<List<DepartmentDto>> GetSupervisedDepartmentsAsync(int companyId, int supervisorId);
}
