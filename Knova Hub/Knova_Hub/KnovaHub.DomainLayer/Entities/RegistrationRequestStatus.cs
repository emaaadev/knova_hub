namespace KnovaHub.DomainLayer.Entities;

/// <summary>
/// Estado de una solicitud de alta de empleado. Solo las solicitudes Pendientes
/// se pueden aprobar o rechazar.
/// </summary>
public enum RegistrationRequestStatus
{
    Pending = 0,
    Approved = 1,
    Rejected = 2
}
