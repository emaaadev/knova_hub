using KnovaHub.DomainLayer.Entities;
using KnovaHub.DomainLayer.Repository;
using KnovaHub.InfrastructureLayer.Data;
using KnovaHub.InfrastructureLayer.Exceptions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace KnovaHub.InfrastructureLayer.Repositories;

public class DepartmentRepository : IDepartmentRepository
{
    private readonly KnovaHubDbContext _context;

    public DepartmentRepository(KnovaHubDbContext context)
    {
        _context = context;
    }

    public Task<List<Department>> GetByCompanyAsync(int companyId)
    {
        return _context.Departments
            .AsNoTracking()
            .Where(d => d.CompanyId == companyId)
            .OrderBy(d => d.Name)
            .ToListAsync();
    }

    public Task<List<Department>> GetSupervisedAsync(int supervisorId)
    {
        return _context.SupervisorDepartments
            .AsNoTracking()
            .Where(sd => sd.SupervisorId == supervisorId)
            .Select(sd => sd.Department!)
            .OrderBy(d => d.Name)
            .ToListAsync();
    }

    public Task<Department?> GetByIdAsync(int companyId, int departmentId)
    {
        return _context.Departments
            .FirstOrDefaultAsync(d => d.Id == departmentId && d.CompanyId == companyId);
    }

    public Task<bool> IsSupervisorOfAsync(int supervisorId, int departmentId)
    {
        return _context.SupervisorDepartments
            .AnyAsync(sd => sd.SupervisorId == supervisorId && sd.DepartmentId == departmentId);
    }

    public async Task<Department> AddAsync(Department department)
    {
        _context.Departments.Add(department);
        await SaveAsync();
        return department;
    }

    public Task UpdateAsync(Department department)
    {
        _context.Departments.Update(department);
        return SaveAsync();
    }

    public async Task SetSupervisorDepartmentsAsync(int supervisorId, IReadOnlyCollection<int> departmentIds)
    {
        var actuales = await _context.SupervisorDepartments
            .Where(sd => sd.SupervisorId == supervisorId)
            .ToListAsync();

        _context.SupervisorDepartments.RemoveRange(actuales.Where(sd => !departmentIds.Contains(sd.DepartmentId)));

        var nuevos = departmentIds
            .Except(actuales.Select(sd => sd.DepartmentId))
            .Select(id => new SupervisorDepartment { SupervisorId = supervisorId, DepartmentId = id });
        _context.SupervisorDepartments.AddRange(nuevos);

        await _context.SaveChangesAsync();
    }

    private async Task SaveAsync()
    {
        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 2601 or 2627 })
        {
            throw new DuplicateEntityException("name", "Ya existe un departamento con ese nombre en la empresa.");
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 547 or 2628 or 8152 })
        {
            throw new DataValidationException("El nombre del departamento no es válido (vacío o de más de 100 caracteres).");
        }
    }
}
