
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
        string? search = null,
        int[]? visibleDepartmentIds = null);

    Task<DocumentSummary> GetSummaryAsync(int companyId, int[]? visibleDepartmentIds);

    Task<Document?> GetByIdAsync(
        int companyId,
        int documentId);

    Task<List<Document>> GetByDepartmentsAsync(
        int companyId,
        IReadOnlyCollection<int> departmentIds);

    Task<Document> AddAsync(Document document);

    Task UpdateAsync(Document document);

    /// <summary>
    /// Guarda los cambios del documento y reemplaza su texto en una sola transacción.
    /// Con text = null se elimina el texto guardado (archivo quitado o no indexable).
    /// </summary>
    Task UpdateWithTextAsync(Document document, DocumentText? text);

    /// <summary>
    /// Devuelve el Markdown de los documentos indexados que el usuario puede ver.
    /// visibleDepartmentIds sigue la misma regla que GetByCompanyAsync (null = todos).
    /// Es la fuente que debe usar la IA en lugar del archivo original.
    /// </summary>
    Task<List<IndexedDocumentText>> GetIndexedTextsAsync(
        int companyId,
        int[]? visibleDepartmentIds);

    Task<List<DiscoveryDocument>> GetDiscoveryDocumentsAsync(
        int companyId, int[]? departmentIds, bool includeReadCounts, CancellationToken cancellationToken);

    Task<List<int>> GetRecentlyReadIdsAsync(
        int companyId, int userId, DateTime since, int limit, CancellationToken cancellationToken);

    Task RecordReadAsync(DocumentRead read);

    // El texto guardado se borra en cascada desde la base de datos.
    Task DeleteAsync(Document document);
}

public record IndexedDocumentText(
    int DocumentId,
    string Title,
    string Markdown);

public record DocumentSummary(
    int Documents,
    int Wikis,
    int Minutes,
    IReadOnlyList<RecentDocument> RecentActivity);

public record RecentDocument(
    int Id,
    string Title,
    DocumentType Type,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

public record DiscoveryDocument(
    int Id,
    string Title,
    string? Description,
    DocumentType Type,
    int? CategoryId,
    int[] TagIds,
    string? Content,
    string? Markdown,
    DateTime ActivityAt,
    long ReadCount);
