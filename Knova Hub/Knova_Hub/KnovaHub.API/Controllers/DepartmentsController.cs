using KnovaHub.API.Authorization;
using KnovaHub.ApplicationLayer.Contract;
using KnovaHub.ApplicationLayer.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KnovaHub.API.Controllers;

[ApiController]
[Route("api/departments")]
[Authorize(Policy = Policies.GestionDepartamentos)]
public class DepartmentsController : ControllerBase
{
    private readonly IDepartmentService _departments;
    private readonly IAuthorizationService _authorization;

    public DepartmentsController(IDepartmentService departments, IAuthorizationService authorization)
    {
        _departments = departments;
        _authorization = authorization;
    }

    // Las políticas garantizan que el actor existe y tiene companyId.
    private Actor Actor => User.ToActor()!;
    private int CompanyId => Actor.CompanyId!.Value;

    /// <summary>AdminEmpresa ve todos los de su empresa; Supervisor solo los suyos.</summary>
    [HttpGet]
    public async Task<ActionResult<List<DepartmentDto>>> GetAll()
    {
        return Ok(await _departments.GetVisibleAsync(Actor));
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<DepartmentDto>> Get(int id)
    {
        var department = await _departments.GetAsync(CompanyId, id);

        var acceso = await _authorization.AuthorizeAsync(User, new DepartmentResource(id), Policies.AccesoDepartamento);
        if (!acceso.Succeeded)
            return Forbid();

        return Ok(department);
    }

    [HttpPost]
    [Authorize(Policy = Policies.AdminEmpresa)]
    public async Task<ActionResult<DepartmentDto>> Create(CreateDepartmentDto dto)
    {
        var created = await _departments.CreateAsync(CompanyId, dto);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    [HttpPut("{id:int}")]
    [Authorize(Policy = Policies.AdminEmpresa)]
    public async Task<ActionResult<DepartmentDto>> Update(int id, UpdateDepartmentDto dto)
    {
        return Ok(await _departments.UpdateAsync(CompanyId, id, dto));
    }

    /// <summary>Reemplaza los departamentos asignados a un Supervisor.</summary>
    [HttpPut("~/api/supervisors/{supervisorId:int}/departments")]
    [Authorize(Policy = Policies.AdminEmpresa)]
    public async Task<ActionResult<List<DepartmentDto>>> SetSupervisorDepartments(
        int supervisorId, AssignSupervisorDepartmentsDto dto)
    {
        return Ok(await _departments.SetSupervisorDepartmentsAsync(CompanyId, supervisorId, dto));
    }
}
