using KnovaHub.DomainLayer.Entities;
using KnovaHub.DomainLayer.Exceptions;
using KnovaHub.DomainLayer.Repository;
using KnovaHub.InfrastructureLayer.Data;
using Microsoft.EntityFrameworkCore;

namespace KnovaHub.InfrastructureLayer.Repositories;

public class QueryLogRepository : IQueryLogRepository
{
    private readonly KnovaHubDbContext _context;

    public QueryLogRepository(KnovaHubDbContext context)
    {
        _context = context;
    }

    public Task<List<QueryLog>> GetByCompanyAsync(int companyId)
    {
         return _context.QueryLogs
            .AsNoTracking()
            .Where(q => q.CompanyId == companyId)
            .ToListAsync();
    }
    public async Task<QueryLog> AddAsync(QueryLog log)
    {
        await _context.QueryLogs.AddAsync(log);
        await SaveAsync();

        return log;
    }
    
    private async Task SaveAsync()
    {
        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new BusinessRuleException (
                "No se pudo guardar el registro de la consulta. Por favor, inténtelo de nuevo.");
        }
    }
}