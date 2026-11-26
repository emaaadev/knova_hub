using KnovaHub.ApplicationLayer.DTOs;

namespace KnovaHub.ApplicationLayer.Contract;

public interface ICompanyService
{
    /// <summary>Datos de la empresa (null si no existe).</summary>
    Task<CompanyDto?> GetAsync(int companyId);

    /// <summary>
    /// Actualiza los datos de la empresa indicada (null si no existe). Lanza DataValidationException
    /// si algún campo no es válido y DuplicateEntityException si el RNC o el correo ya están en uso.
    /// </summary>
    Task<CompanyDto?> UpdateAsync(int companyId, UpdateCompanyDto dto);
}
