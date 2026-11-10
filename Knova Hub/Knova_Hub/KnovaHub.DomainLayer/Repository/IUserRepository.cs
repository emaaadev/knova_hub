using KnovaHub.DomainLayer.Entities;

namespace KnovaHub.DomainLayer.Repository;

public interface IUserRepository
{
    Task<User?> GetByEmailOrRncAsync(string identifier);
    Task<User> RegisterCompanyAsync(Company company, User admin);

    /// <summary>Busca un usuario solo dentro de la empresa indicada (null si es de otra empresa).</summary>
    Task<User?> GetByIdInCompanyAsync(int companyId, int userId);

    /// <summary>
    /// Cambia el rol (y el departamento) de un usuario dentro de una transacción que bloquea la empresa.
    /// Si el usuario es un AdminEmpresa activo y deja de serlo, solo se guarda si en la empresa
    /// queda al menos otro AdminEmpresa activo. Devuelve false si no se guardó por esa regla.
    /// Al dejar de ser Supervisor se le quitan los departamentos supervisados.
    /// </summary>
    Task<bool> ChangeRoleKeepingCompanyAdminAsync(int companyId, int userId, int newRoleId, int? departmentId);

    Task UpdateDepartmentAsync(int userId, int departmentId);

    /// <summary>Busca un usuario por Id sin filtro de empresa (para cambiar la propia contraseña).</summary>
    Task<User?> GetByIdAsync(int userId);

    /// <summary>Usuarios de una empresa, con su rol y departamento.</summary>
    Task<List<User>> GetByCompanyAsync(int companyId);

    /// <summary>Usuarios que pertenecen a alguno de los departamentos indicados.</summary>
    Task<List<User>> GetByDepartmentIdsAsync(IReadOnlyCollection<int> departmentIds);

    Task<User> AddAsync(User user);
    Task UpdateAsync(User user);

    /// <summary>Actualiza solo el hash de la contraseña (para el cambio de contraseña).</summary>
    Task UpdatePasswordAsync(int userId, string newPasswordHash);

    /// <summary>True si ya existe un usuario con ese correo (en cualquier empresa).</summary>
    Task<bool> EmailExistsAsync(string email);
}
