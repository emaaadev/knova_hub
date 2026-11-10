namespace KnovaHub.ApplicationLayer.Contract;

/// <summary>
/// Define las operaciones para almacenar, recuperar
/// y eliminar archivos del módulo de conocimiento.
/// </summary>
public interface IFileStorage
{
    /// <summary>
    /// Guarda un archivo y devuelve su clave interna de almacenamiento.
    /// La extensión debe haber sido validada previamente.
    /// </summary>
    Task<string> SaveAsync(
        Stream fileStream,
        string fileExtension,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Recupera el contenido de un archivo mediante su clave interna.
    /// Devuelve null si el archivo no existe.
    /// </summary>
    Task<Stream?> OpenReadAsync(
        string storageKey,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Elimina un archivo mediante su clave interna.
    /// </summary>
    Task DeleteAsync(
        string storageKey,
        CancellationToken cancellationToken = default);
}