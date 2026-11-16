using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using KnovaHub.ApplicationLayer.DTOs;
using KnovaHub.ApplicationLayer.Services;
using KnovaHub.DomainLayer.Entities;
using KnovaHub.DomainLayer.Exceptions;
using KnovaHub.Tests.Helpers;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace KnovaHub.Tests.Endpoints;

/// <summary>
/// Pruebas de los endpoints de /api/ai/ask: se hacen peticiones HTTP reales contra la API en memoria
/// y se valida el código de respuesta y el cuerpo.
/// </summary>
[TestFixture]
public class AiEndpointTests
{
    private KnovaHubApiFactory _factory = null!;
    private HttpClient _client = null!;
    private JwtSettings _jwt = null!;

    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        _factory = new KnovaHubApiFactory();
        _client = _factory.CreateClient();
        _jwt = _factory.Services.GetRequiredService<JwtSettings>();
    }

    [OneTimeTearDown]
    public void OneTimeTearDown()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    [SetUp]
    public void SetUp()
    {
        _factory.ResetMocks();
        _client.DefaultRequestHeaders.Authorization = null;
    }

    private static StringContent JsonCrudo(string json) => new(json, Encoding.UTF8, "application/json");

    private static async Task<JsonElement> LeerJson(HttpResponseMessage respuesta)
        => await respuesta.Content.ReadFromJsonAsync<JsonElement>();

    // ---------- POST /api/ai/ask ----------

    [Test]
    public async Task Ask_ConTokenValidoYPreguntaValida_Devuelve200ConRespuestaYFuentes()
    {
        // Simula que el servicio de IA responde exitosamente.
        _factory.QueryService.Setup(q => q.AskAsync(It.IsAny<Actor>(), It.IsAny<string>()))
            .ReturnsAsync(new AiAskResponseDto
            {
                Answer = "Las vacaciones son 14 días.",
                Sources = new List<AiSourceDto>
                {
                    new() { DocumentId = 1, Title = "Manual RRHH" }
                }
            });

        var token = TestData.GenerarToken(_jwt, DateTime.UtcNow.AddMinutes(30),
            userId: 1, rol: "AdminEmpresa", companyId: 5);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var respuesta = await _client.PostAsJsonAsync("/api/ai/ask", new { question = "¿Cuántos días de vacaciones hay?" });
        var cuerpo = await LeerJson(respuesta);

        Assert.Multiple(() =>
        {
            Assert.That(respuesta.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(cuerpo.GetProperty("answer").GetString(), Is.EqualTo("Las vacaciones son 14 días."));
            Assert.That(cuerpo.GetProperty("sources")[0].GetProperty("documentId").GetInt32(), Is.EqualTo(1));
            Assert.That(cuerpo.GetProperty("sources")[0].GetProperty("title").GetString(), Is.EqualTo("Manual RRHH"));
        });
    }

    [Test]
    public async Task Ask_ConPreguntaVacia_Devuelve400()
    {
        var token = TestData.GenerarToken(_jwt, DateTime.UtcNow.AddMinutes(30),
            userId: 1, rol: "AdminEmpresa", companyId: 5);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var respuesta = await _client.PostAsJsonAsync("/api/ai/ask", new { question = "" });

        Assert.That(respuesta.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [Test]
    public async Task Ask_SinToken_Devuelve401()
    {
        var respuesta = await _client.PostAsJsonAsync("/api/ai/ask", new { question = "¿Cuántos días de vacaciones hay?" });

        Assert.That(respuesta.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
    }

    [Test]
    public async Task Ask_ConTokenExpirado_Devuelve401()
    {
        var token = TestData.GenerarToken(_jwt, DateTime.UtcNow.AddMinutes(-1),
            userId: 1, rol: "AdminEmpresa", companyId: 5);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var respuesta = await _client.PostAsJsonAsync("/api/ai/ask", new { question = "¿Cuántos días de vacaciones hay?" });

        Assert.That(respuesta.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
    }

    [Test]
    public async Task Ask_LimiteDeUsoSuperado_Devuelve429()
    {
        // Simula que el servicio de IA lanza AiRateLimitException.
        _factory.QueryService.Setup(q => q.AskAsync(It.IsAny<Actor>(), It.IsAny<string>()))
            .Throws(new AiRateLimitException("Límite de consultas alcanzado."));

        var token = TestData.GenerarToken(_jwt, DateTime.UtcNow.AddMinutes(30),
            userId: 1, rol: "AdminEmpresa", companyId: 5);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var respuesta = await _client.PostAsJsonAsync("/api/ai/ask", new { question = "¿Cuántos días de vacaciones hay?" });

        Assert.That(respuesta.StatusCode, Is.EqualTo(HttpStatusCode.TooManyRequests));
    }

    [Test]
    public async Task Ask_AislamientoOtraEmpresa_Devuelve403()
    {
        // Simula que el servicio rechaza el acceso por aislamiento de empresa.
        _factory.QueryService.Setup(q => q.AskAsync(It.IsAny<Actor>(), It.IsAny<string>()))
            .Throws(new UnauthorizedAccessException("No tienes acceso a documentos de otra empresa."));

        var token = TestData.GenerarToken(_jwt, DateTime.UtcNow.AddMinutes(30),
            userId: 1, rol: "Usuario", companyId: 5);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var respuesta = await _client.PostAsJsonAsync("/api/ai/ask", new { question = "¿Información de otra empresa?" });

        Assert.That(respuesta.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
    }

    [Test]
    public async Task Ask_RutaInexistente_Devuelve404()
    {
        var token = TestData.GenerarToken(_jwt, DateTime.UtcNow.AddMinutes(30),
            userId: 1, rol: "AdminEmpresa", companyId: 5);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var respuesta = await _client.PostAsJsonAsync("/api/ai/no-existe", new { question = "prueba" });

        Assert.That(respuesta.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }
}
