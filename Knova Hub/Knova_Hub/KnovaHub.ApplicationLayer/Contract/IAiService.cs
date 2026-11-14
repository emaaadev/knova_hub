using KnovaHub.ApplicationLayer.DTOs;

namespace KnovaHub.ApplicationLayer.Contract;

/// <summary>
/// Servicio de inteligencia artificial. La capa de Application solo conoce este
/// contrato; la implementación (API de Claude) vive en Infrastructure.
/// </summary>
public interface IAiService
{
    /// <summary>
    /// Envía una pregunta y los fragmentos de documentos relevantes a la IA y devuelve su respuesta.
    /// Lanza DataValidationException (pregunta inválida), AiRateLimitException (límite de uso)
    /// o AiServiceException (la IA no está disponible).
    /// </summary>
    Task<AiAnswerDto> AskAsync(AiRequestDto request, CancellationToken cancellationToken = default);
}
