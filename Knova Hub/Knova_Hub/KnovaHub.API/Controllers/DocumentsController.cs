
using KnovaHub.API.Authorization;
using KnovaHub.ApplicationLayer.Contract;
using KnovaHub.ApplicationLayer.DTOs;
using KnovaHub.DomainLayer.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KnovaHub.API.Controllers;

[ApiController]
[Route("api/documents")]
[Authorize]
public class DocumentsController : ControllerBase
{
    private readonly IDocumentService _documents;

    public DocumentsController(IDocumentService documents)
    {
        _documents = documents;
    }

    private Actor? CurrentActor => User.ToActor();

    private bool TryGetActor(out Actor actor)
    {
        var current = CurrentActor;

        if (current == null)
        {
            actor = null!;
            return false;
        }

        actor = current;
        return true;
    }

    // Obtener documentos visibles para el usuario
    [HttpGet]
    public async Task<ActionResult<List<DocumentDto>>> GetAll(
        [FromQuery] DocumentFilterDto? filters)
    {
        if (!TryGetActor(out var actor))
            return Unauthorized();

        return Ok(await _documents.GetAllAsync(actor, filters));
    }

    // Obtener documento por ID
    [HttpGet("{id:int}")]
    public async Task<ActionResult<DocumentDto>> Get(int id)
    {
        if (!TryGetActor(out var actor))
            return Unauthorized();

        return Ok(await _documents.GetByIdAsync(actor, id));
    }

    // Crear documento
    [HttpPost]
    public async Task<ActionResult<DocumentDto>> Create(
        [FromBody] CreateDocumentDto dto)
    {
        if (!TryGetActor(out var actor))
            return Unauthorized();

        var created = await _documents.CreateAsync(actor, dto);

        return CreatedAtAction(
            nameof(Get),
            new { id = created.Id },
            created);
    }

    // Actualizar documento
    [HttpPut("{id:int}")]
    public async Task<ActionResult<DocumentDto>> Update(
        int id,
        [FromBody] UpdateDocumentDto dto)
    {
        if (!TryGetActor(out var actor))
            return Unauthorized();

        return Ok(await _documents.UpdateAsync(actor, id, dto));
    }

    // Eliminar documento
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        if (!TryGetActor(out var actor))
            return Unauthorized();

        await _documents.DeleteAsync(actor, id);

        return NoContent();
    }

    // Subir o reemplazar archivo adjunto
    [HttpPost("{id:int}/file")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(21 * 1024 * 1024)]
    [RequestFormLimits(
        MultipartBodyLengthLimit = 21 * 1024 * 1024)]
    public async Task<ActionResult<DocumentDto>> UploadFile(
        int id,
        IFormFile file,
        CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actor))
            return Unauthorized();

        if (file == null || file.Length == 0)
        {
            return BadRequest(
                "Debes seleccionar un archivo válido.");
        }

        const long maxFileSize = 20L * 1024 * 1024;

        if (file.Length > maxFileSize)
        {
            return BadRequest(
                "El archivo supera el límite de 20 MB.");
        }

        await using var stream = file.OpenReadStream();

        var dto = new UploadDocumentFileDto
        {
            FileName = file.FileName,
            ContentType = file.ContentType,
            FileSizeBytes = file.Length,
            Content = stream
        };

        var result = await _documents.UploadFileAsync(
            actor,
            id,
            dto,
            cancellationToken);

        return Ok(result);
    }

    // Descargar archivo adjunto
    [HttpGet("{id:int}/file")]
    public async Task<IActionResult> DownloadFile(
        int id,
        CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actor))
            return Unauthorized();

        var file = await _documents.DownloadFileAsync(
            actor,
            id,
            cancellationToken);

        return File(
            file.Content,
            file.ContentType,
            file.FileName);
    }

    // Eliminar archivo adjunto
    [HttpDelete("{id:int}/file")]
    public async Task<IActionResult> DeleteFile(
        int id,
        CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actor))
            return Unauthorized();

        await _documents.DeleteFileAsync(
            actor,
            id,
            cancellationToken);

        return NoContent();
    }
}
