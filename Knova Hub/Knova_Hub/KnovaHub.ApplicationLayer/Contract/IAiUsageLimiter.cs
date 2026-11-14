namespace KnovaHub.ApplicationLayer.Contract;

/// <summary>
/// Controla cuántas consultas a la IA puede hacer cada usuario y cada empresa por día,
/// para controlar el gasto.
/// </summary>
public interface IAiUsageLimiter
{
    /// <summary>
    /// Registra una consulta. Lanza AiRateLimitException si el usuario o la empresa
    /// ya alcanzaron su límite diario; en ese caso no cuenta la consulta.
    /// </summary>
    void Acquire(int userId, int companyId);
}
