namespace KnovaHub.ApplicationLayer.DTOs;

/// <summary>Fragmento de un documento que se envía a la IA como contexto.</summary>
public class AiContextChunkDto
{
    public int DocumentId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public bool CanReadSensitiveContent { get; init; }
}

/// <summary>Consulta a la IA. UserId y CompanyId salen del token, nunca del cliente.</summary>
public class AiRequestDto
{
    public int UserId { get; set; }
    public int CompanyId { get; set; }
    public string Question { get; set; } = string.Empty;
    public List<AiContextChunkDto> Context { get; set; } = new();
    public bool EnforceSensitiveContentPolicy { get; init; }
    public bool CanRequestSensitiveContent { get; init; }

    /// <summary>Instrucciones del sistema. Si va vacío se usa el texto por defecto de Knova Hub.</summary>
    public string? SystemPrompt { get; set; }
}

/// <summary>Respuesta de la IA con los tokens consumidos.</summary>
public class AiAnswerDto
{
    public string Answer { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public int InputTokens { get; set; }
    public int OutputTokens { get; set; }
    public IReadOnlyList<int>? SourceDocumentIds { get; set; }
}
