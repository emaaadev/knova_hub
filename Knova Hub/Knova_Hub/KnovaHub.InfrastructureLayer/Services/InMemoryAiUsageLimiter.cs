using KnovaHub.ApplicationLayer.Contract;
using KnovaHub.ApplicationLayer.Services;
using KnovaHub.DomainLayer.Exceptions;

namespace KnovaHub.InfrastructureLayer.Services;

/// <summary>
/// Límite diario de consultas a la IA guardado en memoria. El contador se reinicia
/// cada día (UTC) y cuando se reinicia la API. Debe registrarse como Singleton.
/// </summary>
public class InMemoryAiUsageLimiter : IAiUsageLimiter
{
    private readonly AiSettings _settings;
    private readonly TimeProvider _time;
    private readonly object _lock = new();
    private readonly Dictionary<int, int> _byUser = new();
    private readonly Dictionary<int, int> _byCompany = new();
    private DateOnly _day;

    public InMemoryAiUsageLimiter(AiSettings settings, TimeProvider time)
    {
        _settings = settings;
        _time = time;
    }

    public void Acquire(int userId, int companyId)
    {
        lock (_lock)
        {
            var today = DateOnly.FromDateTime(_time.GetUtcNow().UtcDateTime);
            if (today != _day)
            {
                _day = today;
                _byUser.Clear();
                _byCompany.Clear();
            }

            _byUser.TryGetValue(userId, out var userCount);
            _byCompany.TryGetValue(companyId, out var companyCount);

            if (_settings.MaxQueriesPerUserPerDay > 0 && userCount >= _settings.MaxQueriesPerUserPerDay)
                throw new AiRateLimitException(
                    "Alcanzaste el límite diario de consultas a la IA. Intenta de nuevo mañana.");

            if (_settings.MaxQueriesPerCompanyPerDay > 0 && companyCount >= _settings.MaxQueriesPerCompanyPerDay)
                throw new AiRateLimitException(
                    "Tu empresa alcanzó el límite diario de consultas a la IA. Intenta de nuevo mañana.");

            _byUser[userId] = userCount + 1;
            _byCompany[companyId] = companyCount + 1;
        }
    }
}
