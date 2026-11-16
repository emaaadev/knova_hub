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
    public Task<List<string>> GetRecentQuestionsAsync(
        int companyId, int userId, DateTime since, int limit, CancellationToken cancellationToken)
    {
        return _context.QueryLogs
            .Where(log => log.CompanyId == companyId && log.UserId == userId && log.AskedAt >= since)
            .OrderByDescending(log => log.AskedAt)
            .ThenByDescending(log => log.Id)
            .Take(limit)
            .Select(log => log.Question)
            .ToListAsync(cancellationToken);
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