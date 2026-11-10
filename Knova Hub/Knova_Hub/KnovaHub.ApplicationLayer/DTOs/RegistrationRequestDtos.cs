namespace KnovaHub.ApplicationLayer.DTOs;

public class RegistrationRequestDto
{
    public int Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int DepartmentId { get; set; }
    public string? DepartmentName { get; set; }
    public string RequestedByName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public string? ReviewedByName { get; set; }
    public DateTime? ReviewedAt { get; set; }
}

public class CreateRegistrationRequestDto
{
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public int DepartmentId { get; set; }
}

/// <summary>Resultado de aprobar una solicitud: incluye la contraseña temporal del nuevo usuario.</summary>
public class ApprovedRequestDto
{
    public RegistrationRequestDto Request { get; set; } = new();
    public int UserId { get; set; }

    /// <summary>Contraseña temporal en texto plano, para entregarla al empleado.</summary>
    public string TemporaryPassword { get; set; } = string.Empty;
}
