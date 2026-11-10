
using KnovaHub.ApplicationLayer.DTOs;
using KnovaHub.DomainLayer.Entities;

namespace KnovaHub.ApplicationLayer.Contract;

public interface IDocumentService
{
    // Obtener documentos con filtros
    Task<List<DocumentDto>> GetAllAsync(
        Actor actor,
        DocumentFilterDto? filters = null);

    // Obtener un documento por ID
    Task<DocumentDto> GetByIdAsync(
        Actor actor,
        int documentId);

    // Crear documento
    Task<DocumentDto> CreateAsync(
        Actor actor,
        CreateDocumentDto dto);

    // Actualizar documento
    Task<DocumentDto> UpdateAsync(
        Actor actor,
        int documentId,
        UpdateDocumentDto dto);

    // Eliminar documento
    Task DeleteAsync(
        Actor actor,
        int documentId);

    // Subir o reemplazar archivo
    Task<DocumentDto> UploadFileAsync(
        Actor actor,
        int documentId,
        UploadDocumentFileDto file,
        CancellationToken cancellationToken = default);

    // Descargar archivo
    Task<DownloadDocumentFileDto> DownloadFileAsync(
        Actor actor,
        int documentId,
        CancellationToken cancellationToken = default);

    // Eliminar archivo adjunto
    Task<DocumentDto> DeleteFileAsync(
        Actor actor,
        int documentId,
        CancellationToken cancellationToken = default);
}
