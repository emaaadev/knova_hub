using KnovaHub.ApplicationLayer.Services;
using KnovaHub.DomainLayer.Exceptions;
using KnovaHub.InfrastructureLayer.Services;

namespace KnovaHub.Tests.Services;

[TestFixture]
public class InMemoryAiUsageLimiterTests
{
    private sealed class RelojDePrueba : TimeProvider
    {
        public DateTimeOffset Ahora { get; set; } = new(2026, 11, 14, 12, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => Ahora;
    }

    private RelojDePrueba _reloj = null!;
    private AiSettings _settings = null!;
    private InMemoryAiUsageLimiter _limiter = null!;

    [SetUp]
    public void SetUp()
    {
        _reloj = new RelojDePrueba();
        _settings = new AiSettings { MaxQueriesPerUserPerDay = 2, MaxQueriesPerCompanyPerDay = 3 };
        _limiter = new InMemoryAiUsageLimiter(_settings, _reloj);
    }

    [Test]
    public void Acquire_DentroDelLimite_NoLanza()
    {
        Assert.DoesNotThrow(() =>
        {
            _limiter.Acquire(10, 5);
            _limiter.Acquire(10, 5);
        });
    }

    [Test]
    public void Acquire_UsuarioSuperaSuLimite_LanzaAiRateLimit()
    {
        _limiter.Acquire(10, 5);
        _limiter.Acquire(10, 5);

        var ex = Assert.Throws<AiRateLimitException>(() => _limiter.Acquire(10, 5));

        Assert.That(ex!.Message, Does.Contain("límite diario"));
    }

    [Test]
    public void Acquire_OtroUsuarioNoSeAfectaPorElLimiteDeUnUsuario()
    {
        _limiter.Acquire(10, 5);
        _limiter.Acquire(10, 5);

        Assert.DoesNotThrow(() => _limiter.Acquire(11, 5));
    }

    [Test]
    public void Acquire_EmpresaSuperaSuLimite_LanzaAiRateLimitAunqueCadaUsuarioNo()
    {
        _limiter.Acquire(10, 5);
        _limiter.Acquire(11, 5);
        _limiter.Acquire(12, 5);

        var ex = Assert.Throws<AiRateLimitException>(() => _limiter.Acquire(13, 5));

        Assert.That(ex!.Message, Does.Contain("empresa"));
    }

    [Test]
    public void Acquire_OtraEmpresaNoSeAfectaPorElLimiteDeUnaEmpresa()
    {
        _limiter.Acquire(10, 5);
        _limiter.Acquire(11, 5);
        _limiter.Acquire(12, 5);

        Assert.DoesNotThrow(() => _limiter.Acquire(20, 6));
    }

    [Test]
    public void Acquire_ConsultaRechazada_NoCuentaParaElUsuarioNiLaEmpresa()
    {
        // La empresa 5 llega a su límite (3) con tres usuarios distintos.
        _limiter.Acquire(10, 5);
        _limiter.Acquire(11, 5);
        _limiter.Acquire(12, 5);
        Assert.Throws<AiRateLimitException>(() => _limiter.Acquire(13, 5));

        // El usuario 13 no gastó ninguna consulta: puede usar otra empresa dos veces.
        Assert.DoesNotThrow(() =>
        {
            _limiter.Acquire(13, 6);
            _limiter.Acquire(13, 6);
        });
    }

    [Test]
    public void Acquire_AlCambiarElDia_SeReiniciaElContador()
    {
        _limiter.Acquire(10, 5);
        _limiter.Acquire(10, 5);
        Assert.Throws<AiRateLimitException>(() => _limiter.Acquire(10, 5));

        _reloj.Ahora = _reloj.Ahora.AddDays(1);

        Assert.DoesNotThrow(() => _limiter.Acquire(10, 5));
    }

    [Test]
    public void Acquire_LimiteEnCero_SignificaSinLimite()
    {
        _settings.MaxQueriesPerUserPerDay = 0;
        _settings.MaxQueriesPerCompanyPerDay = 0;

        Assert.DoesNotThrow(() =>
        {
            for (var i = 0; i < 100; i++)
                _limiter.Acquire(10, 5);
        });
    }
}
