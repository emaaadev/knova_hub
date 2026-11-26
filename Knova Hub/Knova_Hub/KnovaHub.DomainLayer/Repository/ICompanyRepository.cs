using KnovaHub.DomainLayer.Entities;

namespace KnovaHub.DomainLayer.Repository;

public interface ICompanyRepository
{
    /// <summary>Busca una empresa por Id (null si no existe).</summary>
    Task<Company?> GetByIdAsync(int companyId);

    /// <summary>
    /// Guarda los cambios de la empresa. Lanza DuplicateEntityException si el RNC o el correo
    /// ya pertenecen a otra empresa.
    /// </summary>
    Task UpdateAsync(Company company);
}
