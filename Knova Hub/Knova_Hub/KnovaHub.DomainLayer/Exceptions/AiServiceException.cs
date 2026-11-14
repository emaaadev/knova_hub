namespace KnovaHub.DomainLayer.Exceptions;

/// <summary>
/// La IA no pudo responder (sin configurar, sin saldo, caída o tiempo agotado).
/// El mensaje es seguro para mostrarlo al usuario: no contiene detalles internos.
/// StatusCode indica el código HTTP con que la API debe responder (502, 503 o 504).
/// </summary>
public class AiServiceException : Exception
{
    public int StatusCode { get; }

    public AiServiceException(string message, int statusCode = 502) : base(message)
    {
        StatusCode = statusCode;
    }
}
