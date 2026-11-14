namespace KnovaHub.DomainLayer.Exceptions;

/// <summary>
/// Se superó el límite de consultas a la IA (por usuario, por empresa o por la API externa).
/// La API responde 429.
/// </summary>
public class AiRateLimitException : Exception
{
    public AiRateLimitException(string message) : base(message)
    {
    }
}
