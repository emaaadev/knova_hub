using KnovaHub.DomainLayer.Entities;

namespace KnovaHub.DomainLayer.Repository;

public interface IRegistrationRequestRepository
{
    /// <summary>Solicitudes de una empresa (opcionalmente filtradas por estado).</summary>
    Task<List<UserRegistrationRequest>> GetByCompanyAsync(int companyId, RegistrationRequestStatus? status = null);

    /// <summary>Solicitudes hechas por un Supervisor (opcionalmente filtradas por estado).</summary>
    Task<List<UserRegistrationRequest>> GetBySupervisorAsync(int supervisorId, RegistrationRequestStatus? status = null);

    /// <summary>Busca una solicitud solo dentro de la empresa indicada (null si es de otra empresa).</summary>
    Task<UserRegistrationRequest?> GetByIdAsync(int companyId, int requestId);

    Task<UserRegistrationRequest> AddAsync(UserRegistrationRequest request);

    /// <summary>
    /// Marca la solicitud como aprobada y crea el Usuario en la misma transacción.
    /// Devuelve el usuario creado (con su Id).
    /// </summary>
    Task<User> ApproveAsync(UserRegistrationRequest request, string passwordHash, int adminId);

    /// <summary>Marca la solicitud como rechazada.</summary>
    Task RejectAsync(UserRegistrationRequest request, int adminId);

    /// <summary>True si ya existe una solicitud Pendiente con ese correo en cualquier empresa.</summary>
    Task<bool> PendingEmailExistsAsync(string email);
}
