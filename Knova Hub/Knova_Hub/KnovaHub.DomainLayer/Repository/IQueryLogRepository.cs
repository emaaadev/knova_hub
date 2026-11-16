using KnovaHub.DomainLayer.Entities;
using System.Collections.Generic;

namespace KnovaHub.DomainLayer.Repository;

public interface IQueryLogRepository
{
    Task<QueryLog> AddAsync(QueryLog queryLog);

    Task<List<QueryLog>> GetByCompanyAsync(int companyId);
}