namespace KnovaHub.ApplicationLayer.Services;

/// <summary>
/// Configuración de la IA (sección "Anthropic").
/// La clave (ApiKey) NO va en appsettings.json: se guarda en User Secrets
/// o en la variable de entorno Anthropic__ApiKey.
/// </summary>
public class AiSettings
{
    public string ApiKey { get; set; } = string.Empty;
    public string BaseUrl { get; set; } = "https://api.anthropic.com/";
    public string ApiVersion { get; set; } = "2023-06-01";

    /// <summary>Modelo a usar. Se cambia aquí sin tocar el código.</summary>
    public string Model { get; set; } = "claude-haiku-5-5";

    public int TimeoutSeconds { get; set; } = 30;

    /// <summary>Máximo de caracteres de pregunta.</summary>
    public int MaxQuestionCharacters { get; set; } = 1000;

    /// <summary>Máximo de caracteres de documentos que se envían a la IA en una consulta.</summary>
    public int MaxInputCharacters { get; set; } = 24000;

    /// <summary>Máximo de tokens que puede generar la IA en una respuesta.</summary>
    public int MaxOutputTokens { get; set; } = 1024;

    /// <summary>Consultas por día por usuario. 0 = sin límite.</summary>
    public int MaxQueriesPerUserPerDay { get; set; } = 30;

    /// <summary>Consultas por día por empresa. 0 = sin límite.</summary>
    public int MaxQueriesPerCompanyPerDay { get; set; } = 300;
}
