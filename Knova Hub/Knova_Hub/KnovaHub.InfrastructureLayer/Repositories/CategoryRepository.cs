
using KnovaHub.DomainLayer.Entities;
using KnovaHub.DomainLayer.Repository;
using KnovaHub.InfrastructureLayer.Data;
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
        await _context.SaveChangesAsync();

        return category;
    }

    public Task UpdateAsync(Category category)
    {
        _context.Categories.Update(category);
        return _context.SaveChangesAsync();
    }
}
