using KnovaHub.ApplicationLayer.DTOs;

namespace KnovaHub.ApplicationLayer.Contract;

public interface IRegistrationRequestService
{
    /// <summary>AdminEmpresa: solicitudes de su empresa. Supervisor: sus propias solicitudes.</summary>
    Task<List<RegistrationRequestDto>> GetVisibleAsync(Actor actor);

    /// <summary>Un Supervisor solicita el alta de un empleado de uno de sus departamentos.</summary>
    Task<RegistrationRequestDto> CreateAsync(Actor actor, CreateRegistrationRequestDto dto);

    /// <summary>El AdminEmpresa aprueba una solicitud y crea el usuario con contraseña temporal.</summary>
    Task<ApprovedRequestDto> ApproveAsync(Actor actor, int requestId);

    Task<RegistrationRequestDto> RejectAsync(Actor actor, int requestId);
}
