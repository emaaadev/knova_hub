using KnovaHub.DomainLayer.Entities;
using KnovaHub.DomainLayer.Repository;
using KnovaHub.InfrastructureLayer.Data;
using KnovaHub.InfrastructureLayer.Exceptions;
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

        // Si solo trae números es un RNC: entra la cuenta Admin de esa empresa.
        if (value.Length > 0 && value.All(char.IsDigit))
        {
            return await query
                .Where(u => u.Company!.Rnc == value && u.RoleId == SystemRoles.AdminRoleId)
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
}