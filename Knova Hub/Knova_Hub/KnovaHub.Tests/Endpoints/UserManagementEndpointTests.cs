using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using KnovaHub.ApplicationLayer.DTOs;
using KnovaHub.ApplicationLayer.Services;
using KnovaHub.DomainLayer.Entities;
using KnovaHub.DomainLayer.Exceptions;
using KnovaHub.Tests.Helpers;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace KnovaHub.Tests.Endpoints;

/// <summary>
/// Endpoints del módulo Usuarios y solicitudes de alta: códigos por rol
/// (200, 201, 401, 403, 404, 409) y el traspaso del actor del token.
/// </summary>
[TestFixture]
public class UserManagementEndpointTests
{
    private const int Empresa = 5;
    private const int SupervisorId = 20;

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

    private void ComoRol(string rol, int userId = 10, int? companyId = Empresa)
    {
        var token = TestData.GenerarToken(_jwt, DateTime.UtcNow.AddMinutes(30), rol: rol, userId: userId, companyId: companyId);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }

    private void ComoAdmin() => ComoRol(SystemRoles.AdminEmpresaRoleName);
    private void ComoSupervisor() => ComoRol(SystemRoles.SupervisorRoleName, SupervisorId);
    private void ComoUsuario() => ComoRol(SystemRoles.UsuarioRoleName, 30);
    private void ComoAdminPlataforma() => ComoRol(SystemRoles.AdminPlataformaRoleName, 1, companyId: null);

    private static UserDto Usuario(int id, int? departmentId = 7) => new()
    {
        Id = id,
        FullName = "Ana Gómez",
        Email = "ana@empresa.com",
        RoleId = SystemRoles.UsuarioRoleId,
        Role = SystemRoles.UsuarioRoleName,
        DepartmentId = departmentId,
        IsActive = true
    };

    // ---------- GET /api/users ----------

    [Test]
    public async Task ListarUsuarios_SinToken_Devuelve401()
    {
        var r = await _client.GetAsync("/api/users");
        Assert.That(r.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
    }

    [Test]
    public async Task ListarUsuarios_Usuario_Devuelve403()
    {
        ComoUsuario();
        var r = await _client.GetAsync("/api/users");
        Assert.That(r.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
    }

    [TestCase(SystemRoles.AdminEmpresaRoleName, 10, TestName = "ListarUsuarios_AdminEmpresa_Devuelve200")]
    [TestCase(SystemRoles.SupervisorRoleName, SupervisorId, TestName = "ListarUsuarios_Supervisor_Devuelve200")]
    public async Task ListarUsuarios_RolPermitido_Devuelve200(string rol, int userId)
    {
        _factory.UserService.Setup(s => s.GetVisibleAsync(It.IsAny<Actor>()))
            .ReturnsAsync(new List<UserDto> { Usuario(30) });
        ComoRol(rol, userId);

        var r = await _client.GetAsync("/api/users");

        Assert.That(r.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }

    // ---------- GET /api/users/{id} ----------

    [Test]
    public async Task VerUsuario_AdminEmpresa_PasaElActorDelToken()
    {
        Actor? recibido = null;
        _factory.UserService.Setup(s => s.GetAsync(It.IsAny<Actor>(), 30))
            .Callback<Actor, int>((a, _) => recibido = a)
            .ReturnsAsync(Usuario(30));
        ComoAdmin();

        var r = await _client.GetAsync("/api/users/30");

        Assert.Multiple(() =>
        {
            Assert.That(r.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(recibido, Is.EqualTo(new Actor(10, Empresa, SystemRoles.AdminEmpresaRoleName)));
        });
    }

    [Test]
    public async Task VerUsuario_UsuarioDeOtraEmpresa_Devuelve404()
    {
        _factory.UserService.Setup(s => s.GetAsync(It.IsAny<Actor>(), 999))
            .ThrowsAsync(new NotFoundException("El usuario no existe en tu empresa."));
        ComoAdmin();

        var r = await _client.GetAsync("/api/users/999");

        Assert.That(r.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    // ---------- POST /api/users ----------

    [Test]
    public async Task CrearUsuario_Supervisor_Devuelve403SinLlamarAlServicio()
    {
        ComoSupervisor();

        var r = await _client.PostAsJsonAsync("/api/users", new CreateUserDto { FullName = "Ana", Email = "ana@empresa.com", RoleId = SystemRoles.UsuarioRoleId, DepartmentId = 7 });

        Assert.That(r.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
        _factory.UserService.Verify(s => s.CreateAsync(It.IsAny<int>(), It.IsAny<CreateUserDto>()), Times.Never);
    }

    [Test]
    public async Task CrearUsuario_AdminEmpresa_Devuelve201ConLocation()
    {
        _factory.UserService.Setup(s => s.CreateAsync(Empresa, It.IsAny<CreateUserDto>()))
            .ReturnsAsync(new CreatedUserDto { User = Usuario(50), TemporaryPassword = "Temp123!" });
        ComoAdmin();

        var r = await _client.PostAsJsonAsync("/api/users",
            new CreateUserDto { FullName = "Ana", Email = "ana@empresa.com", RoleId = SystemRoles.UsuarioRoleId, DepartmentId = 7 });

        Assert.Multiple(() =>
        {
            Assert.That(r.StatusCode, Is.EqualTo(HttpStatusCode.Created));
            Assert.That(r.Headers.Location?.ToString(), Does.EndWith("/api/users/50"));
        });
    }

    [Test]
    public async Task CrearUsuario_EmailDuplicado_Devuelve409()
    {
        _factory.UserService.Setup(s => s.CreateAsync(Empresa, It.IsAny<CreateUserDto>()))
            .ThrowsAsync(new DuplicateEntityException("email", "Ese correo ya está registrado."));
        ComoAdmin();

        var r = await _client.PostAsJsonAsync("/api/users",
            new CreateUserDto { FullName = "Ana", Email = "ana@empresa.com", RoleId = SystemRoles.UsuarioRoleId, DepartmentId = 7 });

        Assert.That(r.StatusCode, Is.EqualTo(HttpStatusCode.Conflict));
    }

    // ---------- PATCH /api/users/{id}/active ----------

    [Test]
    public async Task DesactivarUsuario_Supervisor_Devuelve403()
    {
        ComoSupervisor();

        var r = await _client.PatchAsJsonAsync("/api/users/30/active", new SetUserActiveDto { IsActive = false });

        Assert.That(r.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
        _factory.UserService.Verify(s => s.SetActiveAsync(It.IsAny<Actor>(), It.IsAny<int>(), It.IsAny<bool>()), Times.Never);
    }

    [Test]
    public async Task DesactivarseASiMismo_Devuelve409()
    {
        _factory.UserService.Setup(s => s.SetActiveAsync(It.IsAny<Actor>(), 10, false))
            .ThrowsAsync(new BusinessRuleException(UserService.MensajeDesactivarseASiMismo));
        ComoAdmin();

        var r = await _client.PatchAsJsonAsync("/api/users/10/active", new SetUserActiveDto { IsActive = false });

        Assert.That(r.StatusCode, Is.EqualTo(HttpStatusCode.Conflict));
    }

    // ---------- Solicitudes: GET /api/users/requests ----------

    [Test]
    public async Task ListarSolicitudes_SinToken_Devuelve401()
    {
        var r = await _client.GetAsync("/api/users/requests");
        Assert.That(r.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
    }

    [Test]
    public async Task ListarSolicitudes_Usuario_Devuelve403()
    {
        ComoUsuario();
        var r = await _client.GetAsync("/api/users/requests");
        Assert.That(r.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
    }

    [Test]
    public async Task ListarSolicitudes_AdminEmpresa_Devuelve200()
    {
        _factory.RegistrationRequestService.Setup(s => s.GetVisibleAsync(It.IsAny<Actor>()))
            .ReturnsAsync(new List<RegistrationRequestDto>());
        ComoAdmin();

        var r = await _client.GetAsync("/api/users/requests");

        Assert.That(r.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }

    // ---------- Solicitudes: POST /api/users/requests ----------

    [Test]
    public async Task CrearSolicitud_AdminEmpresa_Devuelve403()
    {
        ComoAdmin();

        var r = await _client.PostAsJsonAsync("/api/users/requests",
            new CreateRegistrationRequestDto { FullName = "Ana", Email = "ana@empresa.com", DepartmentId = 7 });

        Assert.That(r.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
        _factory.RegistrationRequestService.Verify(
            s => s.CreateAsync(It.IsAny<Actor>(), It.IsAny<CreateRegistrationRequestDto>()), Times.Never);
    }

    [Test]
    public async Task CrearSolicitud_Supervisor_Devuelve201()
    {
        _factory.RegistrationRequestService.Setup(s => s.CreateAsync(It.IsAny<Actor>(), It.IsAny<CreateRegistrationRequestDto>()))
            .ReturnsAsync(new RegistrationRequestDto { Id = 5, Status = "Pending" });
        ComoSupervisor();

        var r = await _client.PostAsJsonAsync("/api/users/requests",
            new CreateRegistrationRequestDto { FullName = "Ana", Email = "ana@empresa.com", DepartmentId = 7 });

        Assert.That(r.StatusCode, Is.EqualTo(HttpStatusCode.Created));
    }

    // ---------- Solicitudes: aprobar / rechazar ----------

    [Test]
    public async Task AprobarSolicitud_Supervisor_Devuelve403()
    {
        ComoSupervisor();

        var r = await _client.PostAsync("/api/users/requests/5/approve", null);

        Assert.That(r.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
        _factory.RegistrationRequestService.Verify(s => s.ApproveAsync(It.IsAny<Actor>(), It.IsAny<int>()), Times.Never);
    }

    [Test]
    public async Task AprobarSolicitud_AdminEmpresa_Devuelve200ConContrasena()
    {
        _factory.RegistrationRequestService.Setup(s => s.ApproveAsync(It.IsAny<Actor>(), 5))
            .ReturnsAsync(new ApprovedRequestDto
            {
                UserId = 30,
                TemporaryPassword = "Temp123!",
                Request = new RegistrationRequestDto { Id = 5, Status = "Approved" }
            });
        ComoAdmin();

        var r = await _client.PostAsync("/api/users/requests/5/approve", null);

        Assert.That(r.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }

    [Test]
    public async Task AprobarSolicitud_YaProcesada_Devuelve409()
    {
        _factory.RegistrationRequestService.Setup(s => s.ApproveAsync(It.IsAny<Actor>(), 5))
            .ThrowsAsync(new BusinessRuleException("Solo se pueden aprobar solicitudes pendientes."));
        ComoAdmin();

        var r = await _client.PostAsync("/api/users/requests/5/approve", null);

        Assert.That(r.StatusCode, Is.EqualTo(HttpStatusCode.Conflict));
    }

    [Test]
    public async Task RechazarSolicitud_AdminEmpresa_Devuelve200()
    {
        _factory.RegistrationRequestService.Setup(s => s.RejectAsync(It.IsAny<Actor>(), 5))
            .ReturnsAsync(new RegistrationRequestDto { Id = 5, Status = "Rejected" });
        ComoAdmin();

        var r = await _client.PostAsync("/api/users/requests/5/reject", null);

        Assert.That(r.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }

    // ---------- POST /api/auth/change-password ----------

    [Test]
    public async Task CambiarContrasena_SinToken_Devuelve401()
    {
        var r = await _client.PostAsJsonAsync("/api/auth/change-password",
            new ChangePasswordDto { CurrentPassword = "a", NewPassword = "Nueva123!", ConfirmPassword = "Nueva123!" });

        Assert.That(r.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
    }

    [Test]
    public async Task CambiarContrasena_Valido_Devuelve204()
    {
        ComoAdmin();

        var r = await _client.PostAsJsonAsync("/api/auth/change-password",
            new ChangePasswordDto { CurrentPassword = "Segura123!", NewPassword = "Nueva123!", ConfirmPassword = "Nueva123!" });

        Assert.That(r.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));
        _factory.AuthService.Verify(s => s.ChangePasswordAsync(10, It.IsAny<ChangePasswordDto>()), Times.Once);
    }

    [Test]
    public async Task CambiarContrasena_ContrasenaActualIncorrecta_Devuelve401()
    {
        _factory.AuthService.Setup(s => s.ChangePasswordAsync(10, It.IsAny<ChangePasswordDto>()))
            .ThrowsAsync(new InvalidCredentialsException());
        ComoAdmin();

        var r = await _client.PostAsJsonAsync("/api/auth/change-password",
            new ChangePasswordDto { CurrentPassword = "mala", NewPassword = "Nueva123!", ConfirmPassword = "Nueva123!" });

        Assert.That(r.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
    }
}
