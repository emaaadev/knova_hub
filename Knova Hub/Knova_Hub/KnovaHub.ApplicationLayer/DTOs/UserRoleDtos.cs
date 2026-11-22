namespace KnovaHub.ApplicationLayer.DTOs;

public class UserRoleDto
{
    public int Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public int RoleId { get; set; }
    public string Role { get; set; } = string.Empty;
    public int? DepartmentId { get; set; }
}

public class ChangeRoleDto
{
    public int RoleId { get; set; }

    /// <summary>Obligatorio para Usuario, opcional para AdminEmpresa. Las asignaciones de Supervisor se gestionan aparte.</summary>
    public int? DepartmentId { get; set; }
}

public class ChangeUserDepartmentDto
{
    public int DepartmentId { get; set; }
}
