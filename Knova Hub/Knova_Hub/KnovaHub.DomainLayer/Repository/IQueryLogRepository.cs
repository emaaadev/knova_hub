using KnovaHub.DomainLayer.Entities;
using System.Collections.Generic;

namespace KnovaHub.DomainLayer.Repository;

public interface IQueryLogRepository
{
    Task<QueryLog> AddAsync(QueryLog queryLog);

    Task<List<string>> GetRecentQuestionsAsync(
        int companyId, int userId, DateTime since, int limit, CancellationToken cancellationToken);

    Task<List<QueryLog>> GetByCompanyAsync(int companyId);
}