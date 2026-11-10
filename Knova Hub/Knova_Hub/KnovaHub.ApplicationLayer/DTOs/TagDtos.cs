namespace KnovaHub.ApplicationLayer.DTOs;

/// <summary>
/// Información de una etiqueta devuelta al cliente.
/// </summary>
public class TagDto
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public bool IsActive { get; set; }

    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// Datos necesarios para crear una etiqueta.
/// </summary>
public class CreateTagDto
{
    public string Name { get; set; } = string.Empty;
}

/// <summary>
/// Datos para actualizar una etiqueta existente.
/// </summary>
public class UpdateTagDto
{
    public string Name { get; set; } = string.Empty;

    public bool? IsActive { get; set; }
}