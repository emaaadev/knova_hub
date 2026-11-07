using KnovaHub.DomainLayer.Entities;

namespace KnovaHub.DomainLayer.Repository;

public interface IDepartmentRepository
{
    /// <summary>Departamentos de una empresa (todos, activos o no).</summary>
    Task<List<Department>> GetByCompanyAsync(int companyId);

    /// <summary>Departamentos asignados a un Supervisor.</summary>
    Task<List<Department>> GetSupervisedAsync(int supervisorId);

    /// <summary>Busca un departamento solo dentro de la empresa indicada (null si es de otra empresa).</summary>
    Task<Department?> GetByIdAsync(int companyId, int departmentId);

    Task<bool> IsSupervisorOfAsync(int supervisorId, int departmentId);

    Task<Department> AddAsync(Department department);
    Task UpdateAsync(Department department);

    /// <summary>Reemplaza la lista completa de departamentos que supervisa un usuario.</summary>
    Task SetSupervisorDepartmentsAsync(int supervisorId, IReadOnlyCollection<int> departmentIds);
}
