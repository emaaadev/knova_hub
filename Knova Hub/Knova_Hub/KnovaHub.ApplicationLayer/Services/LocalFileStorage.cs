using KnovaHub.ApplicationLayer.Contract;

namespace KnovaHub.InfrastructureLayer.Services;

/// <summary>
/// Implementación de almacenamiento local para archivos
/// del módulo de conocimiento.
/// </summary>
public class LocalFileStorage : IFileStorage
{
    private readonly string _storagePath;

    public LocalFileStorage(string storagePath)
    {
        if (string.IsNullOrWhiteSpace(storagePath))
            throw new ArgumentException(
                "La ruta de almacenamiento es obligatoria.",
                nameof(storagePath));

        _storagePath = Path.GetFullPath(storagePath);

        Directory.CreateDirectory(_storagePath);
    }

    public async Task<string> SaveAsync(
        Stream fileStream,
        string fileExtension,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(fileStream);

        if (!fileStream.CanRead)
            throw new ArgumentException(
                "El archivo debe permitir lectura.",
                nameof(fileStream));

        var extension = fileExtension.Trim().ToLowerInvariant();

        if (!extension.StartsWith('.'))
            extension = "." + extension;

        string[] allowedExtensions =
        {
            ".pdf",
            ".docx",
            ".txt",
            ".md"
        };

        if (!allowedExtensions.Contains(extension))
            throw new ArgumentException(
                "La extensión del archivo no está permitida.");

        var storageKey = $"{Guid.NewGuid():N}{extension}";

        var filePath = Path.Combine(_storagePath, storageKey);

        try
        {
            await using var outputStream = new FileStream(
                filePath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 81920,
                useAsync: true);

            await fileStream.CopyToAsync(
                outputStream,
                cancellationToken);

            return storageKey;
        }
        catch
        {
            if (File.Exists(filePath))
                File.Delete(filePath);

            throw;
        }
    }

    public Task<Stream?> OpenReadAsync(
        string storageKey,
        CancellationToken cancellationToken = default)
    {
        var filePath = GetSafePath(storageKey);

        if (!File.Exists(filePath))
            return Task.FromResult<Stream?>(null);

        Stream stream = new FileStream(
            filePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 81920,
            useAsync: true);

        return Task.FromResult<Stream?>(stream);
    }

    public Task DeleteAsync(
        string storageKey,
        CancellationToken cancellationToken = default)
    {
        var filePath = GetSafePath(storageKey);

        if (File.Exists(filePath))
            File.Delete(filePath);

        return Task.CompletedTask;
    }

    /// <summary>
    /// Impide que una clave de almacenamiento permita
    /// acceder a archivos fuera del directorio configurado.
    /// </summary>
    private string GetSafePath(string storageKey)
    {
        if (string.IsNullOrWhiteSpace(storageKey))
            throw new ArgumentException(
                "La clave de almacenamiento es obligatoria.");

        if (Path.GetFileName(storageKey) != storageKey ||
            storageKey.Contains('/') ||
            storageKey.Contains('\\'))
        {
            throw new ArgumentException(
                "La clave de almacenamiento no es válida.");
        }

        return Path.Combine(_storagePath, storageKey);
    }
}