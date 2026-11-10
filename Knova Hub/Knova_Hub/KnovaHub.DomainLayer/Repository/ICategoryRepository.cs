
using KnovaHub.DomainLayer.Entities;

namespace KnovaHub.DomainLayer.Repository;

public interface ICategoryRepository
{
    /// <summary>
    /// Obtiene todas las categorías de una empresa.
    /// Permite incluir o excluir las categorías inactivas.
    /// </summary>
    Task<List<Category>> GetByCompanyAsync(
        int companyId,
        bool includeInactive = false);

    Task<Category?> GetByIdAsync(int companyId, int categoryId);

    Task<Category> AddAsync(Category category);

    Task UpdateAsync(Category category);
}
