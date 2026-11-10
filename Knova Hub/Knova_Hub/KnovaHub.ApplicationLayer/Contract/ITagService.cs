using KnovaHub.ApplicationLayer.DTOs;

namespace KnovaHub.ApplicationLayer.Contract;

/// <summary>
/// Define las operaciones de gestión de etiquetas
/// del módulo de conocimiento.
/// </summary>
public interface ITagService
{
    /// <summary>
    /// Obtiene las etiquetas de la empresa del usuario.
    /// </summary>
    Task<List<TagDto>> GetAllAsync(Actor actor);

    /// <summary>
    /// Obtiene una etiqueta por su identificador.
    /// </summary>
    Task<TagDto> GetByIdAsync(Actor actor, int tagId);

    /// <summary>
    /// Crea una etiqueta para la empresa del usuario.
    /// </summary>
    Task<TagDto> CreateAsync(
        Actor actor,
        CreateTagDto dto);

    /// <summary>
    /// Actualiza una etiqueta existente.
    /// </summary>
    Task<TagDto> UpdateAsync(
        Actor actor,
        int tagId,
        UpdateTagDto dto);
}