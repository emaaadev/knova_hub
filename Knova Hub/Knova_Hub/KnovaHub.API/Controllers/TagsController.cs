
using KnovaHub.API.Authorization;
using KnovaHub.ApplicationLayer.Contract;
using KnovaHub.ApplicationLayer.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KnovaHub.API.Controllers;

[ApiController]
[Route("api/tags")]
[Authorize]
public class TagsController : ControllerBase
{
    private readonly ITagService _tags;

    public TagsController(ITagService tags)
    {
        _tags = tags;
    }

    private Actor? CurrentActor => User.ToActor();

    // Obtener etiquetas de la empresa
    [HttpGet]
    public async Task<ActionResult<List<TagDto>>> GetAll()
    {
        if (CurrentActor is not { } actor)
            return Unauthorized();

        return Ok(await _tags.GetAllAsync(actor));
    }

    // Obtener etiqueta por ID
    [HttpGet("{id:int}")]
    public async Task<ActionResult<TagDto>> Get(int id)
    {
        if (CurrentActor is not { } actor)
            return Unauthorized();

        return Ok(await _tags.GetByIdAsync(actor, id));
    }

    // Crear etiqueta (solo AdminEmpresa)
    [HttpPost]
    [Authorize(Policy = Policies.AdminEmpresa)]
    public async Task<ActionResult<TagDto>> Create(
        [FromBody] CreateTagDto dto)
    {
        if (CurrentActor is not { } actor)
            return Unauthorized();

        var created = await _tags.CreateAsync(actor, dto);

        return CreatedAtAction(
            nameof(Get),
            new { id = created.Id },
            created);
    }

    // Actualizar o desactivar etiqueta
    [HttpPut("{id:int}")]
    [Authorize(Policy = Policies.AdminEmpresa)]
    public async Task<ActionResult<TagDto>> Update(
        int id,
        [FromBody] UpdateTagDto dto)
    {
        if (CurrentActor is not { } actor)
            return Unauthorized();

        return Ok(await _tags.UpdateAsync(actor, id, dto));
    }
}
