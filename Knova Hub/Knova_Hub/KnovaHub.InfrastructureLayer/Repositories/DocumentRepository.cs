
using KnovaHub.DomainLayer.Entities;
using KnovaHub.DomainLayer.Exceptions;
using KnovaHub.DomainLayer.Repository;
using KnovaHub.InfrastructureLayer.Data;
using Microsoft.EntityFrameworkCore;

namespace KnovaHub.InfrastructureLayer.Repositories;

public class DocumentRepository : IDocumentRepository
{
    private readonly KnovaHubDbContext _context;

    public DocumentRepository(KnovaHubDbContext context)
    {
        _context = context;
    }

    // Obtener documentos de una empresa con filtros
    public async Task<List<Document>> GetByCompanyAsync(
        int companyId,
        DocumentType? type = null,
        int? categoryId = null,
        int? tagId = null,
        int? departmentId = null,
        string? search = null,
        int[]? visibleDepartmentIds = null)
    {
        IQueryable<Document> query = VisibleDocuments(companyId, visibleDepartmentIds)
            .AsNoTracking()
            .Include(d => d.Author)
            .Include(d => d.Category)
            .Include(d => d.Department)
            .Include(d => d.DocumentTags)
                .ThenInclude(dt => dt.Tag);

        if (type.HasValue)
        {
            query = query.Where(d => d.Type == type.Value);
        }

        if (categoryId.HasValue)
        {
            query = query.Where(
                d => d.CategoryId == categoryId.Value);
        }

        if (tagId.HasValue)
        {
            query = query.Where(d =>
                d.DocumentTags.Any(dt =>
                    dt.TagId == tagId.Value));
        }

        if (departmentId.HasValue)
        {
            query = query.Where(
                d => d.DepartmentId == departmentId.Value);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            string term = search.Trim();

            query = query.Where(d =>
                d.Title.Contains(term) ||
                (d.Description != null &&
                 d.Description.Contains(term)) ||
                (d.Content != null &&
                 d.Content.Contains(term)));
        }

        return await query
            .OrderByDescending(d => d.CreatedAt)
            .AsSplitQuery()
            .ToListAsync();
    }

    public async Task<DocumentSummary> GetSummaryAsync(int companyId, int[]? visibleDepartmentIds)
    {
        var query = VisibleDocuments(companyId, visibleDepartmentIds);
        var counts = await query.GroupBy(d => d.Type)
            .Select(group => new { Type = group.Key, Count = group.Count() })
            .ToDictionaryAsync(row => row.Type, row => row.Count);
        var activity = await query
            .OrderByDescending(d => d.UpdatedAt ?? d.CreatedAt)
            .ThenByDescending(d => d.Id)
            .Take(8)
            .Select(d => new RecentDocument(d.Id, d.Title, d.Type, d.CreatedAt, d.UpdatedAt))
            .ToListAsync();

        return new DocumentSummary(
            counts.GetValueOrDefault(DocumentType.Documento),
            counts.GetValueOrDefault(DocumentType.Wiki),
            counts.GetValueOrDefault(DocumentType.Minuta),
            activity);
    }

    private IQueryable<Document> VisibleDocuments(int companyId, int[]? departmentIds)
    {
        var company = _context.Documents.Where(d => d.CompanyId == companyId);
        return departmentIds switch
        {
            null => company,
            { Length: 0 } => company.Where(d => d.Visibility == DocumentVisibility.General),
            _ => company.Where(d => d.Visibility == DocumentVisibility.General ||
                (d.DepartmentId.HasValue && departmentIds.Contains(d.DepartmentId.Value)))
        };
    }

    // Obtener documento por ID y empresa
    public async Task<Document?> GetByIdAsync(
        int companyId,
        int documentId)
    {
        return await _context.Documents
            .Include(d => d.Author)
            .Include(d => d.Category)
            .Include(d => d.Department)
            .Include(d => d.DocumentTags)
                .ThenInclude(dt => dt.Tag)
            .AsSplitQuery()
            .FirstOrDefaultAsync(d =>
                d.Id == documentId &&
                d.CompanyId == companyId);
    }

    // Obtener documentos de departamentos específicos
    public async Task<List<Document>> GetByDepartmentsAsync(
        int companyId,
        IReadOnlyCollection<int> departmentIds)
    {
        if (departmentIds.Count == 0)
        {
            return new List<Document>();
        }

        return await _context.Documents
            .AsNoTracking()
            .Include(d => d.Author)
            .Include(d => d.Category)
            .Include(d => d.Department)
            .Include(d => d.DocumentTags)
                .ThenInclude(dt => dt.Tag)
            .Where(d =>
                d.CompanyId == companyId &&
                d.DepartmentId.HasValue &&
                departmentIds.Contains(
                    d.DepartmentId.Value))
            .OrderByDescending(d => d.CreatedAt)
            .AsSplitQuery()
            .ToListAsync();
    }

    // Crear documento
    public async Task<Document> AddAsync(Document document)
    {
        await _context.Documents.AddAsync(document);

        await _context.SaveChangesAsync();

        return document;
    }

    public Task UpdateAsync(Document document)
    {
        // GetByIdAsync devuelve una entidad con seguimiento y su StorageKey original.
        return SaveChangesAsync();
    }

    public Task DeleteAsync(Document document)
    {
        _context.Documents.Remove(document);
        return SaveChangesAsync();
    }

    private async Task SaveChangesAsync()
    {
        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new BusinessRuleException(
                "Otro usuario modificó el archivo o eliminó el recurso. Vuelve a abrirlo e inténtalo de nuevo.");
        }
    }
}
