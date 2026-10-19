using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using KnovaHub.ApplicationLayer.DTOs;
using KnovaHub.ApplicationLayer.Services;
using KnovaHub.InfrastructureLayer.Exceptions;
using KnovaHub.Tests.Helpers;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace KnovaHub.Tests.Endpoints;

/// <summary>
/// Pruebas de los endpoints de /api/auth: se hacen peticiones HTTP reales contra la API en memoria
/// y se valida el código de respuesta y el cuerpo.
/// </summary>
[TestFixture]
public class AuthEndpointTests
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
        _factory.AuthService.Reset();
        _client.DefaultRequestHeaders.Authorization = null;
    }

    private static AuthResponseDto RespuestaExitosa() => new()
    {
        Token = "token-de-prueba",
        UserId = 10,
        Email = "contacto@empresa.com",
        Role = "Admin",
        CompanyId = 5,
        CompanyName = "Empresa de Prueba SRL",
        Rnc = "131234567"
    };

    private static async Task<JsonElement> LeerJson(HttpResponseMessage respuesta)
        => await respuesta.Content.ReadFromJsonAsync<JsonElement>();

    private static StringContent JsonCrudo(string json) => new(json, Encoding.UTF8, "application/json");

    // ---------- POST /api/auth/register ----------

    [Test]
    public async Task Register_DatosValidos_Devuelve201ConToken()
    {
        _factory.AuthService.Setup(s => s.RegisterAsync(It.IsAny<RegisterRequestDto>()))
            .ReturnsAsync(RespuestaExitosa());

        var respuesta = await _client.PostAsJsonAsync("/api/auth/register", TestData.RegistroValido());
        var cuerpo = await respuesta.Content.ReadFromJsonAsync<AuthResponseDto>();

        Assert.Multiple(() =>
        {
            Assert.That(respuesta.StatusCode, Is.EqualTo(HttpStatusCode.Created));
            Assert.That(cuerpo!.Token, Is.EqualTo("token-de-prueba"));
            Assert.That(cuerpo.Rnc, Is.EqualTo("131234567"));
        });
    }

    [Test]
    public async Task Register_EnviaAlServicioLosDatosRecibidos()
    {
        RegisterRequestDto? recibido = null;
        _factory.AuthService.Setup(s => s.RegisterAsync(It.IsAny<RegisterRequestDto>()))
            .Callback<RegisterRequestDto>(dto => recibido = dto)
            .ReturnsAsync(RespuestaExitosa());

        await _client.PostAsJsonAsync("/api/auth/register", TestData.RegistroValido());

        Assert.Multiple(() =>
        {
            Assert.That(recibido!.Rnc, Is.EqualTo("131234567"));
            Assert.That(recibido.Email, Is.EqualTo("contacto@empresa.com"));
            Assert.That(recibido.ContactName, Is.EqualTo("Juan Pérez"));
        });
    }

    [TestCase("rnc", "Ese RNC ya está registrado.", TestName = "Register_RncDuplicado_Devuelve409")]
    [TestCase("email", "Ese correo ya está registrado.", TestName = "Register_EmailDuplicado_Devuelve409")]
    public async Task Register_Duplicado_Devuelve409ConCampo(string campo, string mensaje)
    {
        _factory.AuthService.Setup(s => s.RegisterAsync(It.IsAny<RegisterRequestDto>()))
            .ThrowsAsync(new DuplicateEntityException(campo, mensaje));

        var respuesta = await _client.PostAsJsonAsync("/api/auth/register", TestData.RegistroValido());
        var cuerpo = await LeerJson(respuesta);

        Assert.Multiple(() =>
        {
            Assert.That(respuesta.StatusCode, Is.EqualTo(HttpStatusCode.Conflict));
            Assert.That(cuerpo.GetProperty("field").GetString(), Is.EqualTo(campo));
            Assert.That(cuerpo.GetProperty("message").GetString(), Is.EqualTo(mensaje));
        });
    }

    [Test]
    public async Task Register_DatosRechazadosPorValidacion_Devuelve400ConMensaje()
    {
        _factory.AuthService.Setup(s => s.RegisterAsync(It.IsAny<RegisterRequestDto>()))
            .ThrowsAsync(new DataValidationException("Las contraseñas no coinciden."));

        var respuesta = await _client.PostAsJsonAsync("/api/auth/register", TestData.RegistroValido());
        var cuerpo = await LeerJson(respuesta);

        Assert.Multiple(() =>
        {
            Assert.That(respuesta.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
            Assert.That(cuerpo.GetProperty("message").GetString(), Is.EqualTo("Las contraseñas no coinciden."));
        });
    }

    [Test]
    public async Task Register_JsonMalFormado_Devuelve400SinLlamarAlServicio()
    {
        var respuesta = await _client.PostAsync("/api/auth/register", JsonCrudo("{ esto no es json"));

        Assert.That(respuesta.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        _factory.AuthService.Verify(s => s.RegisterAsync(It.IsAny<RegisterRequestDto>()), Times.Never);
    }

    [Test]
    public async Task Register_CampoObligatorioEnNull_Devuelve400SinLlamarAlServicio()
    {
        var respuesta = await _client.PostAsync("/api/auth/register",
            JsonCrudo("""{ "rnc": null, "companyName": "Empresa", "email": "a@b.com", "password": "Segura123!", "confirmPassword": "Segura123!" }"""));

        Assert.That(respuesta.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        _factory.AuthService.Verify(s => s.RegisterAsync(It.IsAny<RegisterRequestDto>()), Times.Never);
    }

    [Test]
    public async Task Register_SinCuerpo_Devuelve4xxSinLlamarAlServicio()
    {
        var respuesta = await _client.PostAsync("/api/auth/register", null);

        Assert.That((int)respuesta.StatusCode, Is.InRange(400, 499));
        _factory.AuthService.Verify(s => s.RegisterAsync(It.IsAny<RegisterRequestDto>()), Times.Never);
    }

    [Test]
    public async Task Register_MetodoGet_Devuelve405()
    {
        var respuesta = await _client.GetAsync("/api/auth/register");

        Assert.That(respuesta.StatusCode, Is.EqualTo(HttpStatusCode.MethodNotAllowed));
    }

    // ---------- POST /api/auth/login ----------

    [Test]
    public async Task Login_CredencialesCorrectas_Devuelve200ConToken()
    {
        _factory.AuthService.Setup(s => s.LoginAsync(It.IsAny<LoginRequestDto>()))
            .ReturnsAsync(RespuestaExitosa());

        var respuesta = await _client.PostAsJsonAsync("/api/auth/login",
            new LoginRequestDto { Identifier = "contacto@empresa.com", Password = TestData.PasswordValida });
        var cuerpo = await respuesta.Content.ReadFromJsonAsync<AuthResponseDto>();

        Assert.Multiple(() =>
        {
            Assert.That(respuesta.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(cuerpo!.Token, Is.EqualTo("token-de-prueba"));
        });
    }

    [Test]
    public async Task Login_CredencialesIncorrectas_Devuelve401ConMensaje()
    {
        _factory.AuthService.Setup(s => s.LoginAsync(It.IsAny<LoginRequestDto>()))
            .ThrowsAsync(new InvalidCredentialsException());

        var respuesta = await _client.PostAsJsonAsync("/api/auth/login",
            new LoginRequestDto { Identifier = "contacto@empresa.com", Password = "Incorrecta1!" });
        var cuerpo = await LeerJson(respuesta);

        Assert.Multiple(() =>
        {
            Assert.That(respuesta.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
            Assert.That(cuerpo.GetProperty("message").GetString(), Is.EqualTo("Usuario o contraseña incorrectos."));
        });
    }

    [Test]
    public async Task Login_JsonMalFormado_Devuelve400SinLlamarAlServicio()
    {
        var respuesta = await _client.PostAsync("/api/auth/login", JsonCrudo("identifier=x&password=y"));

        Assert.That(respuesta.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        _factory.AuthService.Verify(s => s.LoginAsync(It.IsAny<LoginRequestDto>()), Times.Never);
    }

    [Test]
    public async Task Login_IdentificadorEnNull_Devuelve400SinLlamarAlServicio()
    {
        var respuesta = await _client.PostAsync("/api/auth/login",
            JsonCrudo("""{ "identifier": null, "password": "Segura123!" }"""));

        Assert.That(respuesta.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        _factory.AuthService.Verify(s => s.LoginAsync(It.IsAny<LoginRequestDto>()), Times.Never);
    }

    // ---------- GET /api/auth/me (protegido con JWT) ----------

    [Test]
    public async Task Me_SinToken_Devuelve401()
    {
        var respuesta = await _client.GetAsync("/api/auth/me");

        Assert.That(respuesta.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
    }

    [Test]
    public async Task Me_ConTokenValido_Devuelve200ConDatosDelUsuario()
    {
        var token = TestData.GenerarToken(_jwt, DateTime.UtcNow.AddMinutes(30));
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var respuesta = await _client.GetAsync("/api/auth/me");
        var cuerpo = await LeerJson(respuesta);

        Assert.Multiple(() =>
        {
            Assert.That(respuesta.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(cuerpo.GetProperty("userId").GetString(), Is.EqualTo("10"));
            Assert.That(cuerpo.GetProperty("email").GetString(), Is.EqualTo("contacto@empresa.com"));
            Assert.That(cuerpo.GetProperty("role").GetString(), Is.EqualTo("Admin"));
            Assert.That(cuerpo.GetProperty("companyId").GetString(), Is.EqualTo("5"));
        });
    }

    [Test]
    public async Task Me_ConTokenExpirado_Devuelve401()
    {
        var token = TestData.GenerarToken(_jwt, DateTime.UtcNow.AddMinutes(-1));
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var respuesta = await _client.GetAsync("/api/auth/me");

        Assert.That(respuesta.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
    }

    [Test]
    public async Task Me_ConTokenFirmadoConOtraClave_Devuelve401()
    {
        var token = TestData.GenerarToken(_jwt, DateTime.UtcNow.AddMinutes(30),
            claveFirma: "otra-clave-que-no-es-la-de-la-api-pero-igual-de-larga-1234");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var respuesta = await _client.GetAsync("/api/auth/me");

        Assert.That(respuesta.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
    }

    [Test]
    public async Task Me_ConTokenMalFormado_Devuelve401()
    {
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "esto.no.es-un-jwt");

        var respuesta = await _client.GetAsync("/api/auth/me");

        Assert.That(respuesta.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
    }

    // ---------- General ----------

    [Test]
    public async Task RutaInexistente_Devuelve404()
    {
        var respuesta = await _client.GetAsync("/api/no-existe");

        Assert.That(respuesta.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }
}
