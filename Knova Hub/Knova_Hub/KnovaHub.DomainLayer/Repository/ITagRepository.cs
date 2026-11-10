
using KnovaHub.DomainLayer.Entities;

namespace KnovaHub.DomainLayer.Repository;

public interface ITagRepository
{
    /// <summary>
    /// Obtiene todas las etiquetas de una empresa.
    /// Permite incluir o excluir las etiquetas inactivas.
    /// </summary>
    Task<List<Tag>> GetByCompanyAsync(
        int companyId,
        bool includeInactive = false);

    Task<Tag?> GetByIdAsync(int companyId, int tagId);

    Task<List<Tag>> GetByDocumentAsync(int companyId, int documentId);

    Task<Tag> AddAsync(Tag tag);

    Task UpdateAsync(Tag tag);
}
