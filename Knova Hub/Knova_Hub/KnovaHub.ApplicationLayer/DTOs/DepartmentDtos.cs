namespace KnovaHub.ApplicationLayer.DTOs;

public class DepartmentDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class CreateDepartmentDto
{
    public string Name { get; set; } = string.Empty;
}

public class UpdateDepartmentDto
{
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
}

/// <summary>Lista completa de departamentos que tendrá asignados un Supervisor (reemplaza la anterior).</summary>
public class AssignSupervisorDepartmentsDto
{
    public List<int> DepartmentIds { get; set; } = new();
}
