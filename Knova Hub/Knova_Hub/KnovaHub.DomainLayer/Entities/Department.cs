namespace KnovaHub.DomainLayer.Entities;

/// <summary>
/// Departamento de una empresa. Lo crea el AdminEmpresa; cada Usuario pertenece a uno
/// y un Supervisor puede tener asignados uno o varios.
/// </summary>
public class Department
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public int CompanyId { get; set; }
    public Company? Company { get; set; }

    /// <summary>Usuarios que pertenecen a este departamento.</summary>
    public ICollection<User> Members { get; set; } = new List<User>();

    /// <summary>Supervisores que tienen asignado este departamento.</summary>
    public ICollection<SupervisorDepartment> Supervisors { get; set; } = new List<SupervisorDepartment>();

    /// <summary>Documentos visibles únicamente para este departamento y el AdminEmpresa.</summary>
    public ICollection<Document> Documents { get; set; } = new List<Document>();
}
