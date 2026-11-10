
using System.IO.Compression;
using System.Text;
using KnovaHub.ApplicationLayer.Contract;
using KnovaHub.ApplicationLayer.DTOs;
using KnovaHub.DomainLayer.Entities;
using KnovaHub.DomainLayer.Exceptions;
using KnovaHub.DomainLayer.Repository;

namespace KnovaHub.ApplicationLayer.Services;

public class DocumentService : IDocumentService
{
    private readonly IDocumentRepository _documents;
    private readonly ICategoryRepository _categories;
    private readonly ITagRepository _tags;
    private readonly IDepartmentRepository _departments;
    private readonly IUserRepository _users;
    private readonly IFileStorage _fileStorage;

    // Tamaño máximo permitido por archivo: 20 MB
    private const long MaxFileSizeBytes = 20L * 1024 * 1024;

    public DocumentService(
    IDocumentRepository documents,
    ICategoryRepository categories,
    ITagRepository tags,
    IDepartmentRepository departments,
    IUserRepository users,
    IFileStorage fileStorage)
    {
        _documents = documents;
        _categories = categories;
        _tags = tags;
        _departments = departments;
        _users = users;
        _fileStorage = fileStorage;
    }

    public async Task<List<DocumentDto>> GetAllAsync(
        Actor actor,
        DocumentFilterDto? filters = null)
    {
        int companyId = GetCompanyId(actor);
        await GetActiveUserAsync(actor, companyId);

        filters ??= new DocumentFilterDto();

        var documents = await _documents.GetByCompanyAsync(
            companyId,
            filters.Type,
            filters.CategoryId,
            filters.TagId,
            filters.DepartmentId,
            filters.Search);

        var visible = new List<Document>();

        foreach (var document in documents)
        {
            if (await CanReadAsync(actor, document))
                visible.Add(document);
        }

        return visible.Select(ToDto).ToList();
    }

    public async Task<DocumentDto> GetByIdAsync(
        Actor actor,
        int documentId)
    {
        int companyId = GetCompanyId(actor);
        await GetActiveUserAsync(actor, companyId);

        var document = await FindAsync(companyId, documentId);

        if (!await CanReadAsync(actor, document))
            throw new NotFoundException(
                "El documento no existe o no tienes acceso.");

        return ToDto(document);
    }

    public async Task<DocumentDto> CreateAsync(
        Actor actor,
        CreateDocumentDto dto)
    {
        int companyId = GetCompanyId(actor);
        await GetActiveUserAsync(actor, companyId);

        await ValidateDocumentAsync(
            actor,
            companyId,
            dto.Type,
            dto.Visibility,
            dto.DepartmentId,
            dto.CategoryId,
            dto.TagIds);

        var document = new Document
        {
            CompanyId = companyId,
            AuthorId = actor.UserId,
            Title = ValidTitle(dto.Title),
            Description = dto.Description?.Trim(),
            Content = dto.Content,
            Type = dto.Type,
            Visibility = dto.Visibility,
            DepartmentId = dto.DepartmentId,
            CategoryId = dto.CategoryId,
            CreatedAt = DateTime.UtcNow
        };

        var tagIds = (dto.TagIds ?? new List<int>())
            .Distinct()
            .ToList();

        foreach (int tagId in tagIds)
        {
            document.DocumentTags.Add(new DocumentTag
            {
                TagId = tagId,
                CompanyId = companyId
            });
        }

        var created = await _documents.AddAsync(document);

        // Recargar las relaciones para construir el DTO completo.
        var result = await FindAsync(companyId, created.Id);

        return ToDto(result);
    }

    public async Task<DocumentDto> UpdateAsync(
        Actor actor,
        int documentId,
        UpdateDocumentDto dto)
    {
        int companyId = GetCompanyId(actor);
        await GetActiveUserAsync(actor, companyId);

        var document = await FindAsync(companyId, documentId);

        await EnsureCanWriteAsync(actor, document);

        await ValidateDocumentAsync(
            actor,
            companyId,
            dto.Type,
            dto.Visibility,
            dto.DepartmentId,
            dto.CategoryId,
            dto.TagIds);

        document.Title = ValidTitle(dto.Title);
        document.Description = dto.Description?.Trim();
        document.Content = dto.Content;
        document.Type = dto.Type;
        document.Visibility = dto.Visibility;
        document.DepartmentId = dto.DepartmentId;
        document.CategoryId = dto.CategoryId;
        document.UpdatedAt = DateTime.UtcNow;

        var requestedIds = (dto.TagIds ?? new List<int>())
            .Distinct()
            .ToHashSet();

        var removed = document.DocumentTags
            .Where(dt => !requestedIds.Contains(dt.TagId))
            .ToList();

        foreach (var relation in removed)
            document.DocumentTags.Remove(relation);

        var existingIds = document.DocumentTags
            .Select(dt => dt.TagId)
            .ToHashSet();

        foreach (int tagId in requestedIds.Except(existingIds))
        {
            document.DocumentTags.Add(new DocumentTag
            {
                DocumentId = document.Id,
                TagId = tagId,
                CompanyId = companyId
            });
        }

        await _documents.UpdateAsync(document);

        return ToDto(await FindAsync(companyId, documentId));
    }

    public async Task DeleteAsync(
        Actor actor,
        int documentId)
    {
        int companyId = GetCompanyId(actor);
        await GetActiveUserAsync(actor, companyId);

        var document = await FindAsync(companyId, documentId);

        await EnsureCanWriteAsync(actor, document);

        // La coordinación de eliminación de archivos físicos
        // se integrará antes de habilitar este método en la API.
        if (!string.IsNullOrWhiteSpace(document.StorageKey))
        {
            throw new DataValidationException(
                "La eliminación de documentos con archivos adjuntos " +
                "todavía no está habilitada.");
        }

        await _documents.DeleteAsync(document);
    }

    // Adjunta o reemplaza el archivo de un documento existente.
    public async Task<DocumentDto> UploadFileAsync(
        Actor actor,
        int documentId,
        UploadDocumentFileDto file,
        CancellationToken cancellationToken = default)
    {
        int companyId = GetCompanyId(actor);
        await GetActiveUserAsync(actor, companyId);
        var document = await FindAsync(companyId, documentId);
        await EnsureCanWriteAsync(actor, document);

        if (file == null || file.Content == null || !file.Content.CanRead)
            throw new DataValidationException("Debes seleccionar un archivo válido.");

        if (file.FileSizeBytes <= 0 || file.FileSizeBytes > MaxFileSizeBytes)
            throw new DataValidationException("El archivo debe tener un tamaño máximo de 20 MB.");

        string originalName = Path.GetFileName(
            (file.FileName ?? string.Empty).Replace('\\', '/'));
        if (string.IsNullOrWhiteSpace(originalName) || originalName.Length > 255)
            throw new DataValidationException("El nombre del archivo no es válido.");

        string extension = Path.GetExtension(originalName).ToLowerInvariant();
        string contentType = extension switch
        {
            ".pdf" => "application/pdf",
            ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            ".txt" => "text/plain",
            ".md" => "text/markdown",
            _ => throw new DataValidationException(
                "Formato no permitido. Usa PDF, DOCX, TXT o MD.")
        };

        // Se limita la lectura, aunque el tamaño declarado sea incorrecto.
        using var buffer = new MemoryStream();
        byte[] chunk = new byte[81920];
        long totalBytes = 0;
        int bytesRead;
        while ((bytesRead = await file.Content.ReadAsync(
            chunk.AsMemory(0, chunk.Length), cancellationToken)) > 0)
        {
            totalBytes += bytesRead;
            if (totalBytes > MaxFileSizeBytes)
                throw new DataValidationException("El archivo supera el límite de 20 MB.");
            await buffer.WriteAsync(chunk.AsMemory(0, bytesRead), cancellationToken);
        }

        if (totalBytes == 0 || totalBytes != file.FileSizeBytes)
            throw new DataValidationException("El tamaño real del archivo no coincide con el declarado.");

        buffer.Position = 0;
        if (extension == ".pdf")
        {
            byte[] signature = new byte[5];
            int read = await buffer.ReadAsync(signature.AsMemory(), cancellationToken);
            if (read != 5 || !signature.SequenceEqual(new byte[] { 0x25, 0x50, 0x44, 0x46, 0x2D }))
                throw new DataValidationException("El archivo no tiene la firma de un PDF.");
        }
        else if (extension == ".docx")
        {
            try
            {
                using var archive = new ZipArchive(buffer, ZipArchiveMode.Read, leaveOpen: true);
                if (archive.GetEntry("[Content_Types].xml") == null ||
                    archive.GetEntry("word/document.xml") == null)
                    throw new DataValidationException("El DOCX no tiene la estructura esperada.");
            }
            catch (InvalidDataException)
            {
                throw new DataValidationException("El archivo no tiene un formato DOCX válido.");
            }
        }
        else
        {
            try
            {
                var utf8 = new UTF8Encoding(false, true);
                string content = utf8.GetString(buffer.ToArray());
                if (content.IndexOf('\0') >= 0)
                    throw new DataValidationException("El archivo de texto contiene caracteres no permitidos.");
            }
            catch (DecoderFallbackException)
            {
                throw new DataValidationException("El archivo de texto debe estar codificado en UTF-8.");
            }
        }

        buffer.Position = 0;
        string newStorageKey = await _fileStorage.SaveAsync(buffer, extension, cancellationToken);
        string? oldStorageKey = document.StorageKey;

        try
        {
            document.OriginalFileName = originalName;
            document.StorageKey = newStorageKey;
            document.ContentType = contentType;
            document.FileExtension = extension;
            document.FileSizeBytes = totalBytes;
            document.UpdatedAt = DateTime.UtcNow;
            await _documents.UpdateAsync(document);
        }
        catch
        {
            // Evita ocultar el error original si también falla la limpieza.
            try { await _fileStorage.DeleteAsync(newStorageKey, CancellationToken.None); }
            catch (IOException) { /* Requiere limpieza posterior. */ }
            throw;
        }

        if (!string.IsNullOrWhiteSpace(oldStorageKey))
        {
            try { await _fileStorage.DeleteAsync(oldStorageKey, CancellationToken.None); }
            catch (IOException) { /* Requiere limpieza posterior. */ }
        }

        return ToDto(await FindAsync(companyId, documentId));
    }

    // Solo devuelve el archivo después de verificar el acceso al documento.
    public async Task<DownloadDocumentFileDto> DownloadFileAsync(
        Actor actor,
        int documentId,
        CancellationToken cancellationToken = default)
    {
        int companyId = GetCompanyId(actor);
        await GetActiveUserAsync(actor, companyId);
        var document = await FindAsync(companyId, documentId);

        if (!await CanReadAsync(actor, document))
            throw new NotFoundException("El documento no existe o no tienes acceso.");
        if (string.IsNullOrWhiteSpace(document.StorageKey))
            throw new NotFoundException("Este documento no tiene un archivo adjunto.");

        var stream = await _fileStorage.OpenReadAsync(document.StorageKey, cancellationToken);
        if (stream == null)
            throw new NotFoundException("El archivo adjunto no está disponible.");

        return new DownloadDocumentFileDto
        {
            FileName = document.OriginalFileName ?? "documento" + (document.FileExtension ?? ""),
            ContentType = document.ContentType ?? "application/octet-stream",
            Content = stream
        };
    }

    // Quita el adjunto, pero conserva el documento y su contenido.
    public async Task<DocumentDto> DeleteFileAsync(
        Actor actor,
        int documentId,
        CancellationToken cancellationToken = default)
    {
        int companyId = GetCompanyId(actor);
        await GetActiveUserAsync(actor, companyId);
        var document = await FindAsync(companyId, documentId);
        await EnsureCanWriteAsync(actor, document);

        if (string.IsNullOrWhiteSpace(document.StorageKey))
            throw new NotFoundException("Este documento no tiene un archivo adjunto.");

        string oldStorageKey = document.StorageKey;
        document.StorageKey = null;
        document.OriginalFileName = null;
        document.ContentType = null;
        document.FileExtension = null;
        document.FileSizeBytes = null;
        document.UpdatedAt = DateTime.UtcNow;
        await _documents.UpdateAsync(document);

        try { await _fileStorage.DeleteAsync(oldStorageKey, CancellationToken.None); }
        catch (IOException) { /* Requiere limpieza posterior. */ }

        return ToDto(await FindAsync(companyId, documentId));
    }

    private async Task ValidateDocumentAsync(
        Actor actor,
        int companyId,
        DocumentType type,
        DocumentVisibility visibility,
        int? departmentId,
        int? categoryId,
        List<int>? tagIds)
    {
        if (!Enum.IsDefined(typeof(DocumentType), type))
            throw new DataValidationException(
                "El tipo de documento no es válido.");

        if (!Enum.IsDefined(typeof(DocumentVisibility), visibility))
            throw new DataValidationException(
                "La visibilidad del documento no es válida.");

        if (visibility == DocumentVisibility.General)
        {
            if (departmentId.HasValue)
                throw new DataValidationException(
                    "Un documento general no debe tener departamento.");

            if (actor.Role != SystemRoles.AdminEmpresaRoleName)
                throw new UnauthorizedAccessException(
                    "Solo AdminEmpresa puede gestionar documentos generales.");
        }
        else
        {
            if (!departmentId.HasValue)
                throw new DataValidationException(
                    "Los documentos departamentales requieren un departamento.");

            var department = await _departments.GetByIdAsync(
                companyId, departmentId.Value);

            if (department == null || !department.IsActive)
                throw new DataValidationException(
                    "El departamento no existe o está inactivo.");

            if (actor.Role == SystemRoles.SupervisorRoleName)
            {
                bool supervises = await _departments.IsSupervisorOfAsync(
                    actor.UserId, departmentId.Value);

                if (!supervises)
                    throw new UnauthorizedAccessException(
                        "No supervisas el departamento seleccionado.");
            }
            else if (actor.Role != SystemRoles.AdminEmpresaRoleName)
            {
                throw new UnauthorizedAccessException(
                    "No tienes permisos para gestionar documentos.");
            }
        }

        if (categoryId.HasValue)
        {
            var category = await _categories.GetByIdAsync(
                companyId, categoryId.Value);

            if (category == null || !category.IsActive)
                throw new DataValidationException(
                    "La categoría no existe o está inactiva.");
        }

        var distinctIds = (tagIds ?? new List<int>())
            .Distinct()
            .ToList();

        if (distinctIds.Count > 0)
        {
            var availableTags = await _tags.GetByCompanyAsync(companyId);

            var availableIds = availableTags
                .Select(t => t.Id)
                .ToHashSet();

            if (distinctIds.Any(id => !availableIds.Contains(id)))
                throw new DataValidationException(
                    "Una o más etiquetas no existen o están inactivas.");
        }
    }

    private async Task<bool> CanReadAsync(
        Actor actor,
        Document document)
    {
        if (actor.CompanyId != document.CompanyId)
            return false;

        if (actor.Role != SystemRoles.AdminEmpresaRoleName &&
            actor.Role != SystemRoles.SupervisorRoleName &&
            actor.Role != SystemRoles.UsuarioRoleName)
            return false;

        if (actor.Role == SystemRoles.AdminEmpresaRoleName)
            return true;

        if (document.Visibility == DocumentVisibility.General)
            return true;

        if (!document.DepartmentId.HasValue)
            return false;

        if (actor.Role == SystemRoles.SupervisorRoleName)
        {
            return await _departments.IsSupervisorOfAsync(
                actor.UserId, document.DepartmentId.Value);
        }

        var user = await _users.GetByIdInCompanyAsync(
            document.CompanyId, actor.UserId);

        return user != null &&
               user.IsActive &&
               user.DepartmentId == document.DepartmentId;
    }

    private async Task EnsureCanWriteAsync(
        Actor actor,
        Document document)
    {
        if (actor.CompanyId != document.CompanyId)
            throw new NotFoundException(
                "El documento no existe en tu empresa.");

        if (actor.Role == SystemRoles.AdminEmpresaRoleName)
            return;

        if (actor.Role == SystemRoles.SupervisorRoleName &&
            document.Visibility == DocumentVisibility.Departamento &&
            document.DepartmentId.HasValue &&
            await _departments.IsSupervisorOfAsync(
                actor.UserId, document.DepartmentId.Value))
        {
            return;
        }

        throw new UnauthorizedAccessException(
            "No tienes permisos para modificar este documento.");
    }

    private async Task<User> GetActiveUserAsync(
        Actor actor,
        int companyId)
    {
        var user = await _users.GetByIdInCompanyAsync(
            companyId, actor.UserId);

        if (user == null || !user.IsActive)
            throw new UnauthorizedAccessException(
                "El usuario no está activo o no pertenece a la empresa.");

        var expectedRoleId = actor.Role switch
        {
            SystemRoles.AdminEmpresaRoleName =>
                SystemRoles.AdminEmpresaRoleId,

            SystemRoles.SupervisorRoleName =>
                SystemRoles.SupervisorRoleId,

            SystemRoles.UsuarioRoleName =>
                SystemRoles.UsuarioRoleId,

            _ => 0
        };

        if (expectedRoleId == 0 || user.RoleId != expectedRoleId)
            throw new UnauthorizedAccessException(
                "El rol del usuario no es válido o ha cambiado.");

        return user;
    }

    private async Task<Document> FindAsync(
        int companyId,
        int documentId)
    {
        return await _documents.GetByIdAsync(
            companyId, documentId)
            ?? throw new NotFoundException(
                "El documento no existe en tu empresa.");
    }

    private static int GetCompanyId(Actor actor)
    {
        if (actor.CompanyId is not int companyId || companyId <= 0)
            throw new DataValidationException(
                "El usuario no tiene una empresa válida.");

        return companyId;
    }

    private static string ValidTitle(string? title)
    {
        var value = title?.Trim() ?? string.Empty;

        if (value.Length == 0)
            throw new DataValidationException(
                "El título del documento es obligatorio.");

        if (value.Length > 200)
            throw new DataValidationException(
                "El título no puede superar los 200 caracteres.");

        return value;
    }

    private static DocumentDto ToDto(Document document)
    {
        return new DocumentDto
        {
            Id = document.Id,
            Title = document.Title,
            Description = document.Description,
            Content = document.Content,
            Type = document.Type,
            Visibility = document.Visibility,
            CreatedAt = document.CreatedAt,
            UpdatedAt = document.UpdatedAt,
            AuthorId = document.AuthorId,
            AuthorName = document.Author?.FullName ?? string.Empty,
            CategoryId = document.CategoryId,
            CategoryName = document.Category?.Name,
            DepartmentId = document.DepartmentId,
            DepartmentName = document.Department?.Name,
            OriginalFileName = document.OriginalFileName,
            ContentType = document.ContentType,
            FileExtension = document.FileExtension,
            FileSizeBytes = document.FileSizeBytes,
            Tags = document.DocumentTags
                .Where(dt => dt.Tag != null)
                .Select(dt => new TagDto
                {
                    Id = dt.Tag!.Id,
                    Name = dt.Tag.Name,
                    IsActive = dt.Tag.IsActive,
                    CreatedAt = dt.Tag.CreatedAt
                })
                .ToList()
        };
    }
}
