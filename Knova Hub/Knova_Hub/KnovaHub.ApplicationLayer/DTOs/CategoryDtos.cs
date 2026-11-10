namespace KnovaHub.ApplicationLayer.DTOs;

/// <summary>
/// Información de una categoría devuelta al cliente.
/// </summary>
public class CategoryDto
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public bool IsActive { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }
}

/// <summary>
/// Datos necesarios para crear una categoría.
/// </summary>
public class CreateCategoryDto
{
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }
}

/// <summary>
/// Datos para actualizar una categoría existente.
/// </summary>
public class UpdateCategoryDto
{
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public bool? IsActive { get; set; }
}