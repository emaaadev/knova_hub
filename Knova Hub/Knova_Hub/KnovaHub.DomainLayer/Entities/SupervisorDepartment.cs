namespace KnovaHub.DomainLayer.Entities;

/// <summary>
/// Asignación de un departamento a un Supervisor (relación muchos a muchos).
/// </summary>
public class SupervisorDepartment
{
    public int SupervisorId { get; set; }
    public User? Supervisor { get; set; }

    public int DepartmentId { get; set; }
    public Department? Department { get; set; }
}
