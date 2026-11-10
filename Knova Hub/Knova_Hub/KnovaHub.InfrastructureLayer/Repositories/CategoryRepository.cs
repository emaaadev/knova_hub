
using KnovaHub.DomainLayer.Entities;
using KnovaHub.DomainLayer.Exceptions;
using KnovaHub.DomainLayer.Repository;
using KnovaHub.InfrastructureLayer.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace KnovaHub.InfrastructureLayer.Repositories;

public class CategoryRepository : ICategoryRepository
{
    private readonly KnovaHubDbContext _context;

    public CategoryRepository(KnovaHubDbContext context)
    {
        _context = context;
    }

    public Task<List<Category>> GetByCompanyAsync(
        int companyId,
        bool includeInactive = false)
    {
        var query = _context.Categories
            .AsNoTracking()
            .Where(c => c.CompanyId == companyId);

        if (!includeInactive)
            query = query.Where(c => c.IsActive);

        return query
            .OrderBy(c => c.Name)
            .ToListAsync();
    }

    public Task<Category?> GetByIdAsync(int companyId, int categoryId)
    {
        return _context.Categories
            .FirstOrDefaultAsync(c =>
                c.Id == categoryId &&
                c.CompanyId == companyId);
    }

    public async Task<Category> AddAsync(Category category)
    {
        _context.Categories.Add(category);
        await SaveAsync();

        return category;
    }

    public Task UpdateAsync(Category category)
    {
        _context.Categories.Update(category);
        return SaveAsync();
    }

    private async Task SaveAsync()
    {
        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 2601 or 2627 })
        {
            throw new DuplicateEntityException("name", "Ya existe una categoría con ese nombre en la empresa.");
        }
    }
}
