namespace KnovaHub.ApplicationLayer.DTOs;

public class UserDto
{
    public int Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public int RoleId { get; set; }
    public string Role { get; set; } = string.Empty;
    public int? DepartmentId { get; set; }
    public string? DepartmentName { get; set; }
}

/// <summary>Resultado de crear un usuario: incluye la contraseña temporal generada.</summary>
public class CreatedUserDto
{
    public UserDto User { get; set; } = new();

    /// <summary>Contraseña temporal en texto plano, para entregarla al empleado.</summary>
    public string TemporaryPassword { get; set; } = string.Empty;
}

public class CreateUserDto
{
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public int RoleId { get; set; }

    /// <summary>Obligatorio cuando el rol es Usuario; se ignora para los demás roles.</summary>
    public int? DepartmentId { get; set; }
}

public class UpdateUserDto
{
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
}

public class SetUserActiveDto
{
    public bool IsActive { get; set; }
}
