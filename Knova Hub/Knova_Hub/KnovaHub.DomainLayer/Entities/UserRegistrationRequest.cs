namespace KnovaHub.DomainLayer.Entities;

/// <summary>
/// Solicitud de alta de un empleado hecha por un Supervisor. Mientras está Pendiente
/// el usuario no existe ni puede entrar. Al aprobarla, el sistema crea el Usuario
/// con una contraseña temporal y lo obliga a cambiarla en su primer inicio de sesión.
/// </summary>
public class UserRegistrationRequest
{
    public int Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;

    public RegistrationRequestStatus Status { get; set; } = RegistrationRequestStatus.Pending;

    public int CompanyId { get; set; }
    public Company? Company { get; set; }

    public int DepartmentId { get; set; }
    public Department? Department { get; set; }

    /// <summary>Supervisor que hizo la solicitud.</summary>
    public int RequestedById { get; set; }
    public User? RequestedBy { get; set; }

    /// <summary>AdminEmpresa que la aprobó o rechazó (null mientras esté pendiente).</summary>
    public int? ReviewedById { get; set; }
    public User? ReviewedBy { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ReviewedAt { get; set; }
}
