
using KnovaHub.DomainLayer.Entities;

namespace KnovaHub.DomainLayer.Repository;

public interface IDocumentRepository
{
    Task<List<Document>> GetByCompanyAsync(
        int companyId,
        DocumentType? type = null,
        int? categoryId = null,
        int? tagId = null,
        int? departmentId = null,
        string? search = null);

    Task<Document?> GetByIdAsync(
        int companyId,
        int documentId);

    Task<List<Document>> GetByDepartmentsAsync(
        int companyId,
        IReadOnlyCollection<int> departmentIds);

    Task<Document> AddAsync(Document document);

    Task UpdateAsync(Document document);

    Task DeleteAsync(Document document);
}
