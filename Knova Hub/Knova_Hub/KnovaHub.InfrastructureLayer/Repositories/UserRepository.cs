using System.Data;
using KnovaHub.DomainLayer.Entities;
using KnovaHub.DomainLayer.Repository;
using KnovaHub.InfrastructureLayer.Data;
using KnovaHub.DomainLayer.Exceptions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace KnovaHub.InfrastructureLayer.Repositories;

public class UserRepository : IUserRepository
{
    private readonly KnovaHubDbContext _context;

    public UserRepository(KnovaHubDbContext context)
    {
        _context = context;
    }

    public async Task<User?> GetByEmailOrRncAsync(string identifier)
    {
        var value = identifier.Trim();

        IQueryable<User> query = _context.Users
            .Include(u => u.Role)
            .Include(u => u.Company);

        // Si solo trae números es un RNC: entra la cuenta AdminEmpresa de esa empresa.
        if (value.Length > 0 && value.All(char.IsDigit))
        {
            return await query
                .Where(u => u.Company!.Rnc == value && u.RoleId == SystemRoles.AdminEmpresaRoleId)
                .OrderBy(u => u.Id)
                .FirstOrDefaultAsync();
        }

        return await query.FirstOrDefaultAsync(u => u.Email == value);
    }

    public async Task<User> RegisterCompanyAsync(Company company, User admin)
    {
        // Al enlazar el admin con la empresa, EF guarda las dos en la misma transacción:
        // si una falla, no se guarda ninguna.
        admin.Company = company;
        _context.Users.Add(admin);

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException sql)
        {
            switch (sql.Number)
            {
                // 2601 / 2627: violación de índice UNIQUE
                case 2601:
                case 2627:
                    if (sql.Message.Contains("IX_Companies_Rnc"))
                        throw new DuplicateEntityException("rnc", "Ese RNC ya está registrado.");
                    if (sql.Message.Contains("IX_Companies_Email") || sql.Message.Contains("IX_Users_Email"))
                        throw new DuplicateEntityException("email", "Ese correo ya está registrado.");
                    throw new DuplicateEntityException("registro", "Ya existe un registro con esos datos.");

                // 547: violación de CHECK / FOREIGN KEY, 2628 / 8152: texto demasiado largo
                case 547:
                case 2628:
                case 8152:
                    throw new DataValidationException(
                        "Los datos no cumplen las reglas de la base de datos. Revisa el RNC, el correo y el teléfono.");
            }

            throw;
        }

        await _context.Entry(admin).Reference(u => u.Role).LoadAsync();
        return admin;
    }

    public Task<User?> GetByIdInCompanyAsync(int companyId, int userId)
    {
        return _context.Users
            .Include(u => u.Role)
            .Include(u => u.Department)
            .FirstOrDefaultAsync(u => u.Id == userId && u.CompanyId == companyId);
    }

    public async Task<bool> ChangeRoleKeepingCompanyAdminAsync(int companyId, int userId, int newRoleId, int? departmentId)
    {
        // Se bloquea la fila de la empresa: si dos AdminEmpresa se quitan el rol al mismo tiempo,
        // el segundo espera a que termine el primero y vuelve a contar, así la empresa nunca
        // se queda sin AdminEmpresa.
        await using var tx = await _context.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted);
        await _context.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT Id FROM Companies WITH (UPDLOCK, HOLDLOCK) WHERE Id = {companyId}");

        var user = await _context.Users
            .Include(u => u.SupervisedDepartments)
            .FirstAsync(u => u.Id == userId && u.CompanyId == companyId);

        var dejaDeSerAdmin = user.RoleId == SystemRoles.AdminEmpresaRoleId
            && user.IsActive
            && newRoleId != SystemRoles.AdminEmpresaRoleId;

        if (dejaDeSerAdmin)
        {
            var otrosAdmins = await _context.Users.CountAsync(u =>
                u.CompanyId == companyId
                && u.Id != userId
                && u.IsActive
                && u.RoleId == SystemRoles.AdminEmpresaRoleId);

            if (otrosAdmins == 0)
                return false;
        }

        user.RoleId = newRoleId;
        user.DepartmentId = departmentId;

        if (newRoleId != SystemRoles.SupervisorRoleId)
            user.SupervisedDepartments.Clear();

        await _context.SaveChangesAsync();
        await tx.CommitAsync();
        return true;
    }

    public async Task UpdateDepartmentAsync(int userId, int departmentId)
    {
        var user = await _context.Users.FirstAsync(u => u.Id == userId);
        user.DepartmentId = departmentId;
        await _context.SaveChangesAsync();
    }

    public Task<User?> GetByIdAsync(int userId)
    {
        return _context.Users
            .Include(u => u.Role)
            .Include(u => u.Company)
            .FirstOrDefaultAsync(u => u.Id == userId);
    }

    public Task<List<User>> GetByCompanyAsync(int companyId)
    {
        return _context.Users
            .AsNoTracking()
            .Include(u => u.Role)
            .Include(u => u.Department)
            .Where(u => u.CompanyId == companyId)
            .OrderBy(u => u.FullName)
            .ToListAsync();
    }

    public Task<List<User>> GetByDepartmentIdsAsync(IReadOnlyCollection<int> departmentIds)
    {
        return _context.Users
            .AsNoTracking()
            .Include(u => u.Role)
            .Include(u => u.Department)
            .Where(u => u.DepartmentId != null && departmentIds.Contains(u.DepartmentId.Value))
            .OrderBy(u => u.FullName)
            .ToListAsync();
    }

    public async Task<User> AddAsync(User user)
    {
        _context.Users.Add(user);
        await SaveAsync();
        await _context.Entry(user).Reference(u => u.Role).LoadAsync();
        return user;
    }

    public Task UpdateAsync(User user)
    {
        _context.Users.Update(user);
        return SaveAsync();
    }

    public async Task UpdatePasswordAsync(int userId, string newPasswordHash)
    {
        var user = await _context.Users.FirstAsync(u => u.Id == userId);
        user.PasswordHash = newPasswordHash;
        user.MustChangePassword = false;
        await _context.SaveChangesAsync();
    }

    public Task<bool> EmailExistsAsync(string email)
    {
        return _context.Users.AnyAsync(u => u.Email == email);
    }

    private async Task SaveAsync()
    {
        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 2601 or 2627 })
        {
            throw new DuplicateEntityException("email", "Ese correo ya está registrado.");
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 547 or 2628 or 8152 })
        {
            throw new DataValidationException(
                "Los datos no cumplen las reglas de la base de datos. Revisa el nombre y el correo.");
        }
    }
}