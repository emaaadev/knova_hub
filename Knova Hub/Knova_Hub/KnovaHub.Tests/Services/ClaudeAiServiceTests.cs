using System.Net;
using System.Text;
using KnovaHub.ApplicationLayer.Contract;
using KnovaHub.ApplicationLayer.DTOs;
using KnovaHub.ApplicationLayer.Services;
using KnovaHub.DomainLayer.Exceptions;
using KnovaHub.InfrastructureLayer.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace KnovaHub.Tests.Services;

/// <summary>
/// Pruebas de ClaudeAiService con un manejador HTTP simulado: nunca se llama a la API real.
/// </summary>
[TestFixture]
public class ClaudeAiServiceTests
{
    private const string RespuestaOk =
        "{\"content\":[{\"type\":\"text\",\"text\":\"Hola desde la IA\"}],\"usage\":{\"input_tokens\":10,\"output_tokens\":5}}";

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly Func<CancellationToken, Task<HttpResponseMessage>> _respond;

        public int Calls { get; private set; }
        public List<string?> Bodies { get; } = new();
        public string? LastBody { get; private set; }
        public string? ApiKeyHeader { get; private set; }
        public string? VersionHeader { get; private set; }

        public StubHandler(Func<CancellationToken, Task<HttpResponseMessage>> respond) => _respond = respond;

        public static StubHandler Responde(HttpStatusCode status, string body) =>
            new(_ => Task.FromResult(new HttpResponseMessage(status)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            }));

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Calls++;
            LastBody = request.Content is null ? null : await request.Content.ReadAsStringAsync(ct);
            Bodies.Add(LastBody);
            ApiKeyHeader = request.Headers.TryGetValues("x-api-key", out var k) ? k.FirstOrDefault() : null;
            VersionHeader = request.Headers.TryGetValues("anthropic-version", out var v) ? v.FirstOrDefault() : null;
            return await _respond(ct);
        }
    }

    private Mock<IAiUsageLimiter> _limiter = null!;
    private AiSettings _settings = null!;

    [SetUp]
    public void SetUp()
    {
        _limiter = new Mock<IAiUsageLimiter>();
        _settings = new AiSettings { ApiKey = "clave-de-prueba", Model = "modelo-de-prueba", TimeoutSeconds = 30 };
    }

    private ClaudeAiService Crear(StubHandler handler) =>
        new(new HttpClient(handler) { BaseAddress = new Uri("https://api.prueba.test/") },
            _settings, _limiter.Object, NullLogger<ClaudeAiService>.Instance);

    private static AiRequestDto Pregunta(string texto = "¿Cuál es la política de vacaciones?") => new()
    {
        UserId = 10,
        CompanyId = 5,
        Question = texto,
        Context = new List<AiContextChunkDto>
        {
            new() { DocumentId = 1, Title = "Manual RRHH", Content = "Las vacaciones son 14 días." }
        }
    };

    // ---------- Respuesta correcta ----------

    [Test]
    public async Task Ask_RespuestaValida_DevuelveTextoModeloYTokens()
    {
        var handler = StubHandler.Responde(HttpStatusCode.OK, RespuestaOk);

        var r = await Crear(handler).AskAsync(Pregunta());

        Assert.Multiple(() =>
        {
            Assert.That(r.Answer, Is.EqualTo("Hola desde la IA"));
            Assert.That(r.Model, Is.EqualTo("modelo-de-prueba"));
            Assert.That(r.InputTokens, Is.EqualTo(10));
            Assert.That(r.OutputTokens, Is.EqualTo(5));
        });
    }

    [Test]
    public async Task Ask_EnviaClaveVersionModeloYContexto()
    {
        var handler = StubHandler.Responde(HttpStatusCode.OK, RespuestaOk);

        await Crear(handler).AskAsync(Pregunta());

        Assert.Multiple(() =>
        {
            Assert.That(handler.ApiKeyHeader, Is.EqualTo("clave-de-prueba"));
            Assert.That(handler.VersionHeader, Is.EqualTo(_settings.ApiVersion));
            Assert.That(handler.LastBody, Does.Contain("modelo-de-prueba"));
            Assert.That(handler.LastBody, Does.Contain("Manual RRHH"));
            Assert.That(handler.LastBody, Does.Contain("Las vacaciones son 14 días."));
        });
    }

    [Test]
    public async Task Ask_RegistraUnaConsultaEnElLimiteConUsuarioYEmpresa()
    {
        var handler = StubHandler.Responde(HttpStatusCode.OK, RespuestaOk);

        await Crear(handler).AskAsync(Pregunta());

        _limiter.Verify(l => l.Acquire(10, 5), Times.Once);
    }

    private static StubHandler RespondeEnOrden(params string[] respuestas)
    {
        var pendientes = new Queue<string>(respuestas);
        return new StubHandler(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(System.Text.Json.JsonSerializer.Serialize(new
            {
                content = new[] { new { type = "text", text = pendientes.Dequeue() } }
            }))
        }));
    }

    private static AiRequestDto ConsultaRestringida(bool supervisor = false) => new()
    {
        UserId = 10,
        CompanyId = 5,
        Question = "¿Qué debo pegar al entrar al portal?",
        EnforceSensitiveContentPolicy = true,
        CanRequestSensitiveContent = supervisor,
        Context = new()
        {
            new() { DocumentId = 1, Title = "Horario", Content = "Abrimos a las 8." },
            new() { DocumentId = 2, Title = "Acceso FIN-DEMO", Content = "En la segunda casilla pega FIN-DEMO." },
            new() { DocumentId = 3, Title = "Operaciones", Content = "En la segunda casilla pega OPS-DEMO.",
                CanReadSensitiveContent = supervisor }
        }
    };

    [TestCase("""{"sensitiveQuestion":true,"safeDocumentIds":[1]}""")]
    [TestCase("resultado sin formato")]
    public async Task Ask_UsuarioOSalidaDeClasificacionInvalida_DeniegaSinGenerar(string clasificacion)
    {
        var handler = RespondeEnOrden(clasificacion);

        var resultado = await Crear(handler).AskAsync(ConsultaRestringida());

        Assert.That(resultado.Answer, Does.Contain("Solicítala directamente al supervisor"));
        Assert.That(resultado.SourceDocumentIds, Is.Empty);
        Assert.That(handler.Calls, Is.EqualTo(1));
    }

    [TestCase("Para Operaciones, pega OPS-DEMO.", true)]
    [TestCase("INFORMACION_SENSIBLE_NO_DISPONIBLE", false)]
    public async Task Ask_Supervisor_SoloEnviaDatosSensiblesDeSuDepartamento(string respuestaIa, bool permitido)
    {
        var handler = RespondeEnOrden("""{"sensitiveQuestion":true,"safeDocumentIds":[1]}""", respuestaIa);

        var resultado = await Crear(handler).AskAsync(ConsultaRestringida(supervisor: true));

        Assert.That(handler.LastBody, Does.Contain("OPS-DEMO").And.Not.Contain("FIN-DEMO"));
        Assert.That(resultado.Answer, Does.Contain(permitido ? "OPS-DEMO" : "Solicítala directamente al supervisor"));
        Assert.That(resultado.SourceDocumentIds, Is.EqualTo(permitido ? new[] { 3 } : Array.Empty<int>()));
        _limiter.Verify(l => l.Acquire(10, 5), Times.Once);
    }

    [TestCase(true)]
    [TestCase(false)]
    public async Task Ask_Usuario_ExcluyeFuentesSensiblesYValidaRespuesta(bool respuestaSegura)
    {
        var handler = RespondeEnOrden(
            """{"sensitiveQuestion":false,"safeDocumentIds":[1]}""",
            respuestaSegura ? "Abrimos a las 8." : "El dato de acceso es FIN-DEMO.",
            respuestaSegura ? """{"sensitiveQuestion":false,"safeDocumentIds":[1]}"""
                : """{"sensitiveQuestion":false,"safeDocumentIds":[]}""");

        var resultado = await Crear(handler).AskAsync(ConsultaRestringida());

        Assert.That(resultado.Answer, Does.Contain(respuestaSegura ? "Abrimos a las 8." : "Solicítala directamente al supervisor"));
        Assert.That(resultado.SourceDocumentIds, Is.EqualTo(respuestaSegura ? new[] { 1 } : Array.Empty<int>()));
        Assert.That(handler.Bodies[1], Does.Contain("Horario").And.Not.Contain("FIN-DEMO").And.Not.Contain("OPS-DEMO"));
    }

    // ---------- Validaciones (no llaman a la API) ----------

    [TestCase("", TestName = "Ask_PreguntaVacia_Rechazada")]
    [TestCase("   ", TestName = "Ask_PreguntaSoloEspacios_Rechazada")]
    public void Ask_PreguntaInvalida_LanzaDataValidationSinLlamarALaApi(string pregunta)
    {
        var handler = StubHandler.Responde(HttpStatusCode.OK, RespuestaOk);

        Assert.ThrowsAsync<DataValidationException>(() => Crear(handler).AskAsync(Pregunta(pregunta)));

        Assert.That(handler.Calls, Is.EqualTo(0));
        _limiter.Verify(l => l.Acquire(It.IsAny<int>(), It.IsAny<int>()), Times.Never);
    }

    [Test]
    public void Ask_PreguntaMasLargaQueElMaximo_LanzaDataValidation()
    {
        _settings.MaxQuestionCharacters = 20;
        var handler = StubHandler.Responde(HttpStatusCode.OK, RespuestaOk);

        Assert.ThrowsAsync<DataValidationException>(() => Crear(handler).AskAsync(Pregunta(new string('a', 21))));

        Assert.That(handler.Calls, Is.EqualTo(0));
    }

    // ---------- Configuración ----------

    [Test]
    public void Ask_SinClave_Lanza503SinLlamarALaApi()
    {
        _settings.ApiKey = "";
        var handler = StubHandler.Responde(HttpStatusCode.OK, RespuestaOk);

        var ex = Assert.ThrowsAsync<AiServiceException>(() => Crear(handler).AskAsync(Pregunta()));

        Assert.Multiple(() =>
        {
            Assert.That(ex!.StatusCode, Is.EqualTo(503));
            Assert.That(ex.Message, Is.EqualTo(ClaudeAiService.MensajeSinConfigurar));
            Assert.That(handler.Calls, Is.EqualTo(0));
        });
    }

    // ---------- Límites de uso ----------

    [Test]
    public void Ask_LimiteDeUsoSuperado_LanzaAiRateLimitSinLlamarALaApi()
    {
        _limiter.Setup(l => l.Acquire(10, 5)).Throws(new AiRateLimitException("Límite alcanzado."));
        var handler = StubHandler.Responde(HttpStatusCode.OK, RespuestaOk);

        Assert.ThrowsAsync<AiRateLimitException>(() => Crear(handler).AskAsync(Pregunta()));

        Assert.That(handler.Calls, Is.EqualTo(0));
    }

    [Test]
    public void Ask_ApiResponde429_LanzaAiRateLimit()
    {
        var handler = StubHandler.Responde((HttpStatusCode)429, "{\"error\":{\"type\":\"rate_limit_error\"}}");

        var ex = Assert.ThrowsAsync<AiRateLimitException>(() => Crear(handler).AskAsync(Pregunta()));

        Assert.That(ex!.Message, Is.EqualTo(ClaudeAiService.MensajeMuchasPeticiones));
    }

    // ---------- Errores de la API externa ----------

    [Test]
    public void Ask_ApiResponde401_Lanza503SinExponerDetalles()
    {
        var handler = StubHandler.Responde(HttpStatusCode.Unauthorized,
            "{\"error\":{\"message\":\"invalid x-api-key\"}}");

        var ex = Assert.ThrowsAsync<AiServiceException>(() => Crear(handler).AskAsync(Pregunta()));

        Assert.Multiple(() =>
        {
            Assert.That(ex!.StatusCode, Is.EqualTo(503));
            Assert.That(ex.Message, Is.EqualTo(ClaudeAiService.MensajeNoDisponible));
            Assert.That(ex.Message, Does.Not.Contain("x-api-key"));
            Assert.That(ex.Message, Does.Not.Contain("clave-de-prueba"));
        });
    }

    [Test]
    public void Ask_ApiSinSaldo_Lanza503ConMensajeDeSaldo()
    {
        var handler = StubHandler.Responde(HttpStatusCode.BadRequest,
            "{\"error\":{\"message\":\"Your credit balance is too low to access the Anthropic API.\"}}");

        var ex = Assert.ThrowsAsync<AiServiceException>(() => Crear(handler).AskAsync(Pregunta()));

        Assert.Multiple(() =>
        {
            Assert.That(ex!.StatusCode, Is.EqualTo(503));
            Assert.That(ex.Message, Is.EqualTo(ClaudeAiService.MensajeSinSaldo));
        });
    }

    [TestCase(500, TestName = "Ask_ApiResponde500_Lanza502")]
    [TestCase(529, TestName = "Ask_ApiSobrecargada529_Lanza502")]
    public void Ask_ApiConErrorDelServidor_Lanza502(int status)
    {
        var handler = StubHandler.Responde((HttpStatusCode)status, "{\"error\":{\"message\":\"interno\"}}");

        var ex = Assert.ThrowsAsync<AiServiceException>(() => Crear(handler).AskAsync(Pregunta()));

        Assert.Multiple(() =>
        {
            Assert.That(ex!.StatusCode, Is.EqualTo(502));
            Assert.That(ex.Message, Does.Not.Contain("interno"));
        });
    }

    [Test]
    public void Ask_ErrorDeRed_Lanza502()
    {
        var handler = new StubHandler(_ => throw new HttpRequestException("sin conexión"));

        var ex = Assert.ThrowsAsync<AiServiceException>(() => Crear(handler).AskAsync(Pregunta()));

        Assert.Multiple(() =>
        {
            Assert.That(ex!.StatusCode, Is.EqualTo(502));
            Assert.That(ex.Message, Does.Not.Contain("sin conexión"));
        });
    }

    [Test]
    public void Ask_TiempoAgotado_Lanza504()
    {
        _settings.TimeoutSeconds = 1;
        var handler = new StubHandler(async ct =>
        {
            await Task.Delay(Timeout.Infinite, ct);
            return new HttpResponseMessage(HttpStatusCode.OK);
        });

        var ex = Assert.ThrowsAsync<AiServiceException>(() => Crear(handler).AskAsync(Pregunta()));

        Assert.Multiple(() =>
        {
            Assert.That(ex!.StatusCode, Is.EqualTo(504));
            Assert.That(ex.Message, Is.EqualTo(ClaudeAiService.MensajeTiempoAgotado));
        });
    }

    [Test]
    public void Ask_RespuestaSinTexto_Lanza502()
    {
        var handler = StubHandler.Responde(HttpStatusCode.OK, "{\"content\":[],\"usage\":{}}");

        var ex = Assert.ThrowsAsync<AiServiceException>(() => Crear(handler).AskAsync(Pregunta()));

        Assert.That(ex!.StatusCode, Is.EqualTo(502));
    }

    [Test]
    public void Ask_RespuestaQueNoEsJson_Lanza502()
    {
        var handler = StubHandler.Responde(HttpStatusCode.OK, "esto no es json");

        var ex = Assert.ThrowsAsync<AiServiceException>(() => Crear(handler).AskAsync(Pregunta()));

        Assert.That(ex!.StatusCode, Is.EqualTo(502));
    }

    // ---------- Tamaño máximo de entrada ----------

    [Test]
    public async Task Ask_ContextoMasGrandeQueElMaximo_SeRecorta()
    {
        _settings.MaxInputCharacters = 50;
        var handler = StubHandler.Responde(HttpStatusCode.OK, RespuestaOk);
        var request = Pregunta();
        request.Context = new List<AiContextChunkDto>
        {
            new() { DocumentId = 1, Title = "Largo", Content = new string('a', 200) },
            new() { DocumentId = 2, Title = "Otro", Content = "NO-DEBE-ENVIARSE" }
        };

        await Crear(handler).AskAsync(request);

        Assert.Multiple(() =>
        {
            Assert.That(handler.LastBody, Does.Contain(new string('a', 50)));
            Assert.That(handler.LastBody, Does.Not.Contain(new string('a', 51)));
            Assert.That(handler.LastBody, Does.Not.Contain("NO-DEBE-ENVIARSE"));
        });
    }

    [Test]
    public async Task Ask_SinContexto_EnviaSoloLaPregunta()
    {
        var handler = StubHandler.Responde(HttpStatusCode.OK, RespuestaOk);
        var request = Pregunta("¿Hola?");
        request.Context = new List<AiContextChunkDto>();

        await Crear(handler).AskAsync(request);

        Assert.Multiple(() =>
        {
            Assert.That(handler.LastBody, Does.Contain("¿Hola?"));
            Assert.That(handler.LastBody, Does.Not.Contain("<documento id="));
        });
    }
}
