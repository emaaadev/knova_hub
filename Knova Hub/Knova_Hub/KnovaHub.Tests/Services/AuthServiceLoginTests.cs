using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using KnovaHub.ApplicationLayer.DTOs;
using KnovaHub.ApplicationLayer.Services;
using KnovaHub.DomainLayer.Entities;
using KnovaHub.DomainLayer.Repository;
using KnovaHub.InfrastructureLayer.Exceptions;
using KnovaHub.Tests.Helpers;
using Moq;

namespace KnovaHub.Tests.Services;

/// <summary>
/// Pruebas unitarias de AuthService.LoginAsync con el repositorio simulado.
/// </summary>
[TestFixture]
public class AuthServiceLoginTests
{
    // BCrypt es lento a propósito; se calcula una sola vez para todo el fixture.
    private static readonly string HashValido = BCrypt.Net.BCrypt.HashPassword(TestData.PasswordValida);

    private Mock<IUserRepository> _repo = null!;
    private JwtSettings _jwt = null!;
    private AuthService _service = null!;

    [SetUp]
    public void SetUp()
    {
        _repo = new Mock<IUserRepository>();
        _jwt = TestData.JwtSettings();
        _service = new AuthService(_repo.Object, _jwt);
    }

    private static LoginRequestDto Login(string identificador = "contacto@empresa.com", string password = TestData.PasswordValida)
        => new() { Identifier = identificador, Password = password };

    private void RepositorioDevuelve(User? usuario)
        => _repo.Setup(r => r.GetByEmailOrRncAsync(It.IsAny<string>())).ReturnsAsync(usuario);

    [Test]
    public void Login_UsuarioNoExiste_LanzaInvalidCredentials()
    {
        RepositorioDevuelve(null);

        Assert.ThrowsAsync<InvalidCredentialsException>(() => _service.LoginAsync(Login("noexiste@empresa.com")));
    }

    [Test]
    public void Login_ContrasenaIncorrecta_LanzaInvalidCredentials()
    {
        RepositorioDevuelve(TestData.UsuarioAdmin(HashValido));

        Assert.ThrowsAsync<InvalidCredentialsException>(() => _service.LoginAsync(Login(password: "Incorrecta1!")));
    }

    [Test]
    public void Login_UsuarioInactivo_LanzaInvalidCredentials()
    {
        var usuario = TestData.UsuarioAdmin(HashValido);
        usuario.IsActive = false;
        RepositorioDevuelve(usuario);

        Assert.ThrowsAsync<InvalidCredentialsException>(() => _service.LoginAsync(Login()));
    }

    [Test]
    public void Login_EmpresaInactiva_LanzaInvalidCredentials()
    {
        var usuario = TestData.UsuarioAdmin(HashValido);
        usuario.Company!.IsActive = false;
        RepositorioDevuelve(usuario);

        Assert.ThrowsAsync<InvalidCredentialsException>(() => _service.LoginAsync(Login()));
    }

    [Test]
    public void Login_UsuarioSinEmpresa_LanzaInvalidCredentials()
    {
        var usuario = TestData.UsuarioAdmin(HashValido);
        usuario.Company = null;
        RepositorioDevuelve(usuario);

        Assert.ThrowsAsync<InvalidCredentialsException>(() => _service.LoginAsync(Login()));
    }

    [Test]
    public void Login_ErrorDeCredenciales_NoRevelaSiFalloElUsuarioOLaContrasena()
    {
        RepositorioDevuelve(null);
        var exUsuario = Assert.ThrowsAsync<InvalidCredentialsException>(() => _service.LoginAsync(Login()));

        RepositorioDevuelve(TestData.UsuarioAdmin(HashValido));
        var exPassword = Assert.ThrowsAsync<InvalidCredentialsException>(() => _service.LoginAsync(Login(password: "Incorrecta1!")));

        Assert.That(exUsuario!.Message, Is.EqualTo(exPassword!.Message));
    }

    [TestCase("contacto@empresa.com", TestName = "Login_ConEmail_DevuelveRespuestaValida")]
    [TestCase("131234567", TestName = "Login_ConRnc_DevuelveRespuestaValida")]
    public async Task Login_CredencialesCorrectas_DevuelveRespuestaValida(string identificador)
    {
        RepositorioDevuelve(TestData.UsuarioAdmin(HashValido));

        var respuesta = await _service.LoginAsync(Login(identificador));

        Assert.Multiple(() =>
        {
            Assert.That(respuesta.Token, Is.Not.Empty);
            Assert.That(respuesta.UserId, Is.EqualTo(10));
            Assert.That(respuesta.CompanyId, Is.EqualTo(5));
            Assert.That(respuesta.Role, Is.EqualTo(SystemRoles.AdminRoleName));
            Assert.That(respuesta.Rnc, Is.EqualTo("131234567"));
        });
        _repo.Verify(r => r.GetByEmailOrRncAsync(identificador), Times.Once);
    }

    [Test]
    public async Task Login_CredencialesCorrectas_TokenContieneClaimsEmisorYExpiracion()
    {
        RepositorioDevuelve(TestData.UsuarioAdmin(HashValido));

        var respuesta = await _service.LoginAsync(Login());
        var token = new JwtSecurityTokenHandler().ReadJwtToken(respuesta.Token);

        Assert.Multiple(() =>
        {
            Assert.That(token.Issuer, Is.EqualTo(_jwt.Issuer));
            Assert.That(token.Audiences, Does.Contain(_jwt.Audience));
            Assert.That(token.Claims.First(c => c.Type == ClaimTypes.NameIdentifier).Value, Is.EqualTo("10"));
            Assert.That(token.Claims.First(c => c.Type == ClaimTypes.Email).Value, Is.EqualTo("contacto@empresa.com"));
            Assert.That(token.Claims.First(c => c.Type == ClaimTypes.Role).Value, Is.EqualTo(SystemRoles.AdminRoleName));
            Assert.That(token.Claims.First(c => c.Type == "companyId").Value, Is.EqualTo("5"));
            Assert.That(respuesta.ExpiresAt,
                Is.EqualTo(DateTime.UtcNow.AddMinutes(_jwt.ExpiresMinutes)).Within(TimeSpan.FromSeconds(10)));
        });
    }

    [Test]
    public async Task Login_UsuarioSinRolCargado_UsaRolUserPorDefecto()
    {
        var usuario = TestData.UsuarioAdmin(HashValido);
        usuario.Role = null;
        RepositorioDevuelve(usuario);

        var respuesta = await _service.LoginAsync(Login());

        Assert.That(respuesta.Role, Is.EqualTo(SystemRoles.UserRoleName));
    }
}
