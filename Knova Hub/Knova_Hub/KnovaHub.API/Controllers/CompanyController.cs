using KnovaHub.API.Authorization;
using KnovaHub.ApplicationLayer.Contract;
using KnovaHub.ApplicationLayer.DTOs;
using KnovaHub.DomainLayer.Exceptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KnovaHub.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CompanyController : ControllerBase
{
    private readonly ICompanyService _companies;

    public CompanyController(ICompanyService companies)
    {
        _companies = companies;
    }

    /// <summary>Datos de la empresa del usuario autenticado (cualquier rol con empresa).</summary>
    [Authorize]
    [HttpGet]
    public async Task<IActionResult> Get()
    {
        if (!TryGetCompanyId(out var companyId))
            return NotFound(new { message = "El usuario no pertenece a ninguna empresa." });

        var company = await _companies.GetAsync(companyId);
        return company is null
            ? NotFound(new { message = "No se encontró la empresa." })
            : Ok(company);
    }

    /// <summary>
    /// Actualiza los datos de la empresa. Solo AdminEmpresa (otros roles: 403).
    /// La empresa sale siempre del token, nunca del cuerpo de la petición.
    /// </summary>
    [Authorize(Policy = Policies.AdminEmpresa)]
    [HttpPut]
    public async Task<IActionResult> Update(UpdateCompanyDto dto)
    {
        if (!TryGetCompanyId(out var companyId))
            return Forbid();

        try
        {
            var company = await _companies.UpdateAsync(companyId, dto);
            return company is null
                ? NotFound(new { message = "No se encontró la empresa." })
                : Ok(company);
        }
        catch (DuplicateEntityException ex)
        {
            return Conflict(new { field = ex.Field, message = ex.Message });
        }
        catch (DataValidationException ex)
        {
            return ex.Field is null
                ? BadRequest(new { message = ex.Message })
                : BadRequest(new { field = ex.Field, message = ex.Message });
        }
    }

    // La empresa viaja en el claim "companyId" del token (el mismo que lee AuthController.Me).
    private bool TryGetCompanyId(out int companyId)
    {
        return int.TryParse(User.FindFirst("companyId")?.Value, out companyId);
    }
}
