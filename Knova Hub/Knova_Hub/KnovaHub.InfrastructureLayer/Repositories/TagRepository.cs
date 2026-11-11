
using KnovaHub.DomainLayer.Entities;
using KnovaHub.DomainLayer.Exceptions;
using KnovaHub.DomainLayer.Repository;
using KnovaHub.InfrastructureLayer.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace KnovaHub.InfrastructureLayer.Repositories;

public class TagRepository : ITagRepository
{
    private readonly KnovaHubDbContext _context;

    public TagRepository(KnovaHubDbContext context)
    {
        _context = context;
    }

    public Task<List<Tag>> GetByCompanyAsync(
        int companyId,
        bool includeInactive = false)
    {
        var query = _context.Tags
            .AsNoTracking()
            .Where(t => t.CompanyId == companyId);

        if (!includeInactive)
            query = query.Where(t => t.IsActive);

        return query
            .OrderBy(t => t.Name)
            .ToListAsync();
    }

    public Task<Tag?> GetByIdAsync(int companyId, int tagId)
    {
        return _context.Tags
            .FirstOrDefaultAsync(t =>
                t.Id == tagId &&
                t.CompanyId == companyId);
    }

    public Task<List<Tag>> GetByDocumentAsync(
        int companyId,
        int documentId)
    {
        return _context.DocumentTags
            .AsNoTracking()
            .Where(dt =>
                dt.CompanyId == companyId &&
                dt.DocumentId == documentId)
            .Select(dt => dt.Tag!)
            .OrderBy(t => t.Name)
            .ToListAsync();
    }

    public async Task<Tag> AddAsync(Tag tag)
    {
        _context.Tags.Add(tag);
        await SaveAsync();

        return tag;
    }

    public Task UpdateAsync(Tag tag)
    {
        _context.Tags.Update(tag);
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
            throw new DuplicateEntityException("name", "Ya existe una etiqueta con ese nombre en la empresa.");
        }
    }
}
