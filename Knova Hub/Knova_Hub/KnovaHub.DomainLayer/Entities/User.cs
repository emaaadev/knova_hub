namespace KnovaHub.DomainLayer.Entities;

public class User
{
    public int Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;

    /// <summary>True cuando el usuario entró con una contraseña temporal y debe cambiarla.</summary>
    public bool MustChangePassword { get; set; }

    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Opcional solo para AdminPlataforma; el resto de roles siempre pertenece a una empresa.</summary>
    public int? CompanyId { get; set; }
    public Company? Company { get; set; }

    public int RoleId { get; set; }
    public Role? Role { get; set; }

    /// <summary>Departamento al que pertenece (obligatorio para el rol Usuario).</summary>
    public int? DepartmentId { get; set; }
    public Department? Department { get; set; }

    /// <summary>Departamentos que supervisa (solo para el rol Supervisor).</summary>
    public ICollection<SupervisorDepartment> SupervisedDepartments { get; set; } = new List<SupervisorDepartment>();

    /// <summary>Documentos creados por este usuario.</summary>
    public ICollection<Document> AuthoredDocuments { get; set; } = new List<Document>();
}
