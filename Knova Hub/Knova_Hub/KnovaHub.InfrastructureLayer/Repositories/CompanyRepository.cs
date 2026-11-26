using KnovaHub.DomainLayer.Entities;
using KnovaHub.DomainLayer.Exceptions;
using KnovaHub.DomainLayer.Repository;
using KnovaHub.InfrastructureLayer.Data;
using Microsoft.EntityFrameworkCore;

namespace KnovaHub.InfrastructureLayer.Repositories;

public class CompanyRepository : ICompanyRepository
{
    private readonly KnovaHubDbContext _db;

    public CompanyRepository(KnovaHubDbContext db)
    {
        _db = db;
    }

    public Task<Company?> GetByIdAsync(int companyId) =>
        _db.Companies.FirstOrDefaultAsync(c => c.Id == companyId);

    public async Task UpdateAsync(Company company)
    {
        // Revisión previa para dar un mensaje claro; los índices únicos de la BD siguen siendo la garantía final.
        if (await _db.Companies.AnyAsync(c => c.Id != company.Id && c.Rnc == company.Rnc))
            throw new DuplicateEntityException("rnc", "Ese RNC ya está registrado.");

        if (await _db.Companies.AnyAsync(c => c.Id != company.Id && c.Email == company.Email))
            throw new DuplicateEntityException("email", "Ese correo ya está registrado.");

        try
        {
            _db.Companies.Update(company);
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException ex) when (ex.InnerException?.Message.Contains("IX_Companies_Rnc") == true)
        {
            throw new DuplicateEntityException("rnc", "Ese RNC ya está registrado.");
        }
        catch (DbUpdateException ex) when (ex.InnerException?.Message.Contains("IX_Companies_Email") == true)
        {
            throw new DuplicateEntityException("email", "Ese correo ya está registrado.");
        }
    }
}
