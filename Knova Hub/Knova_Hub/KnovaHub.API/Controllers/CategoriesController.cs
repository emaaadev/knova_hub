
using KnovaHub.API.Authorization;
using KnovaHub.ApplicationLayer.Contract;
using KnovaHub.ApplicationLayer.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KnovaHub.API.Controllers;

[ApiController]
[Route("api/categories")]
[Authorize]
public class CategoriesController : ControllerBase
{
    private readonly ICategoryService _categories;

    public CategoriesController(ICategoryService categories)
    {
        _categories = categories;
    }

    private Actor? CurrentActor => User.ToActor();

    // Obtener todas las categorías de la empresa
    [HttpGet]
    public async Task<ActionResult<List<CategoryDto>>> GetAll()
    {
        if (CurrentActor is not { } actor)
            return Unauthorized();

        return Ok(await _categories.GetAllAsync(actor));
    }

    // Obtener categoría por ID
    [HttpGet("{id:int}")]
    public async Task<ActionResult<CategoryDto>> Get(int id)
    {
        if (CurrentActor is not { } actor)
            return Unauthorized();

        return Ok(await _categories.GetByIdAsync(actor, id));
    }

    // Crear categoría (solo AdminEmpresa)
    [HttpPost]
    [Authorize(Policy = Policies.AdminEmpresa)]
    public async Task<ActionResult<CategoryDto>> Create(
        [FromBody] CreateCategoryDto dto)
    {
        if (CurrentActor is not { } actor)
            return Unauthorized();

        var created = await _categories.CreateAsync(actor, dto);

        return CreatedAtAction(
            nameof(Get),
            new { id = created.Id },
            created);
    }

    // Actualizar o desactivar categoría
    [HttpPut("{id:int}")]
    [Authorize(Policy = Policies.AdminEmpresa)]
    public async Task<ActionResult<CategoryDto>> Update(
        int id,
        [FromBody] UpdateCategoryDto dto)
    {
        if (CurrentActor is not { } actor)
            return Unauthorized();

        return Ok(await _categories.UpdateAsync(actor, id, dto));
    }


    // Eliminación lógica de categoría (solo AdminEmpresa)
    [HttpDelete("{id:int}")]
    [Authorize(Policy = Policies.AdminEmpresa)]
    public async Task<IActionResult> Delete(int id)
    {
        if (CurrentActor is not { } actor)
            return Unauthorized();

        await _categories.DeactivateAsync(actor, id);

        return NoContent();
    }

}
