using KnovaHub.ApplicationLayer.DTOs;

namespace KnovaHub.ApplicationLayer.Contract;

/// <summary>
/// Define las operaciones de gestión de categorías
/// del módulo de conocimiento.
/// </summary>
public interface ICategoryService
{
    /// <summary>
    /// Obtiene las categorías de la empresa del usuario.
    /// </summary>
    Task<List<CategoryDto>> GetAllAsync(Actor actor);

    /// <summary>
    /// Obtiene una categoría por su identificador.
    /// </summary>
    Task<CategoryDto> GetByIdAsync(Actor actor, int categoryId);

    /// <summary>
    /// Crea una categoría para la empresa del usuario.
    /// </summary>
    Task<CategoryDto> CreateAsync(
        Actor actor,
        CreateCategoryDto dto);

    /// <summary>
    /// Actualiza una categoría existente.
    /// </summary>
    Task<CategoryDto> UpdateAsync(
        Actor actor,
        int categoryId,
        UpdateCategoryDto dto);

    Task DeactivateAsync(Actor actor, int categoryId);
}