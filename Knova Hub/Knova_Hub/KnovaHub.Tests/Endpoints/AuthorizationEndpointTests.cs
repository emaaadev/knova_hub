using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using KnovaHub.ApplicationLayer.DTOs;
using KnovaHub.ApplicationLayer.Services;
using KnovaHub.DomainLayer.Entities;
using KnovaHub.InfrastructureLayer.Exceptions;
using KnovaHub.Tests.Helpers;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace KnovaHub.Tests.Endpoints;

/// <summary>
/// Políticas por rol y por departamento: peticiones HTTP reales contra la API en memoria
/// con tokens de cada rol. Lo importante es el código: 401 sin token, 403 cuando el rol
/// o el departamento no corresponde.
/// </summary>
[TestFixture]
public class AuthorizationEndpointTests
{
    private const int Empresa = 5;
    private const int SupervisorId = 20;
    private const int DeptoSupervisado = 7;
    private const int DeptoAjeno = 8;

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

        var repo = _factory.DepartmentRepository;
        repo.Setup(r => r.GetByIdAsync(Empresa, DeptoSupervisado))
            .ReturnsAsync(new Department { Id = DeptoSupervisado, CompanyId = Empresa, Name = "Ventas", IsActive = true });
        repo.Setup(r => r.GetByIdAsync(Empresa, DeptoAjeno))
            .ReturnsAsync(new Department { Id = DeptoAjeno, CompanyId = Empresa, Name = "Soporte", IsActive = true });
        repo.Setup(r => r.IsSupervisorOfAsync(SupervisorId, DeptoSupervisado)).ReturnsAsync(true);
        repo.Setup(r => r.IsSupervisorOfAsync(SupervisorId, DeptoAjeno)).ReturnsAsync(false);

        _factory.DepartmentService.Setup(s => s.GetAsync(Empresa, It.IsAny<int>()))
            .ReturnsAsync((int _, int id) => new DepartmentDto { Id = id, Name = "Depto", IsActive = true });
        _factory.DepartmentService.Setup(s => s.GetVisibleAsync(It.IsAny<Actor>()))
            .ReturnsAsync(new List<DepartmentDto>());
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

    // ---------- GET /api/departments ----------

    [Test]
    public async Task ListarDepartamentos_SinToken_Devuelve401()
    {
        var r = await _client.GetAsync("/api/departments");
        Assert.That(r.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
    }

    [Test]
    public async Task ListarDepartamentos_Usuario_Devuelve403()
    {
        ComoUsuario();
        var r = await _client.GetAsync("/api/departments");
        Assert.That(r.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
    }

    [Test]
    public async Task ListarDepartamentos_AdminPlataformaSinEmpresa_Devuelve403()
    {
        ComoAdminPlataforma();
        var r = await _client.GetAsync("/api/departments");
        Assert.That(r.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
    }

    [Test]
    public async Task ListarDepartamentos_AdminEmpresaSinClaimCompanyId_Devuelve403()
    {
        ComoRol(SystemRoles.AdminEmpresaRoleName, companyId: null);
        var r = await _client.GetAsync("/api/departments");
        Assert.That(r.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
    }

    [TestCase(SystemRoles.AdminEmpresaRoleName, 10, TestName = "ListarDepartamentos_AdminEmpresa_Devuelve200")]
    [TestCase(SystemRoles.SupervisorRoleName, SupervisorId, TestName = "ListarDepartamentos_Supervisor_Devuelve200")]
    public async Task ListarDepartamentos_RolPermitido_Devuelve200(string rol, int userId)
    {
        ComoRol(rol, userId);
        var r = await _client.GetAsync("/api/departments");
        Assert.That(r.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }

    // ---------- GET /api/departments/{id} (política por departamento) ----------

    [Test]
    public async Task VerDepartamento_SupervisorEnSuDepartamento_Devuelve200()
    {
        ComoSupervisor();
        var r = await _client.GetAsync($"/api/departments/{DeptoSupervisado}");
        Assert.That(r.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }

    [Test]
    public async Task VerDepartamento_SupervisorEnDepartamentoAjeno_Devuelve403()
    {
        ComoSupervisor();
        var r = await _client.GetAsync($"/api/departments/{DeptoAjeno}");
        Assert.That(r.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
    }

    [Test]
    public async Task VerDepartamento_AdminEmpresaCualquieraDeSuEmpresa_Devuelve200()
    {
        ComoAdmin();
        var r = await _client.GetAsync($"/api/departments/{DeptoAjeno}");
        Assert.That(r.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }

    [Test]
    public async Task VerDepartamento_DeOtraEmpresa_Devuelve404()
    {
        _factory.DepartmentService.Setup(s => s.GetAsync(Empresa, 999))
            .ThrowsAsync(new NotFoundException("El departamento no existe en tu empresa."));
        ComoAdmin();

        var r = await _client.GetAsync("/api/departments/999");

        Assert.That(r.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    // ---------- Acciones solo de AdminEmpresa ----------

    [Test]
    public async Task CrearDepartamento_Supervisor_Devuelve403SinLlamarAlServicio()
    {
        ComoSupervisor();
        var r = await _client.PostAsJsonAsync("/api/departments", new CreateDepartmentDto { Name = "Nuevo" });

        Assert.That(r.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
        _factory.DepartmentService.Verify(s => s.CreateAsync(It.IsAny<int>(), It.IsAny<CreateDepartmentDto>()), Times.Never);
    }

    [Test]
    public async Task CrearDepartamento_AdminEmpresa_Devuelve201EnSuEmpresa()
    {
        _factory.DepartmentService.Setup(s => s.CreateAsync(Empresa, It.IsAny<CreateDepartmentDto>()))
            .ReturnsAsync(new DepartmentDto { Id = 50, Name = "Nuevo", IsActive = true });
        ComoAdmin();

        var r = await _client.PostAsJsonAsync("/api/departments", new CreateDepartmentDto { Name = "Nuevo" });

        Assert.Multiple(() =>
        {
            Assert.That(r.StatusCode, Is.EqualTo(HttpStatusCode.Created));
            Assert.That(r.Headers.Location?.ToString(), Does.EndWith("/api/departments/50"));
        });
    }

    [Test]
    public async Task CrearDepartamento_NombreDuplicado_Devuelve409()
    {
        _factory.DepartmentService.Setup(s => s.CreateAsync(Empresa, It.IsAny<CreateDepartmentDto>()))
            .ThrowsAsync(new DuplicateEntityException("name", "Ya existe un departamento con ese nombre en la empresa."));
        ComoAdmin();

        var r = await _client.PostAsJsonAsync("/api/departments", new CreateDepartmentDto { Name = "Ventas" });

        Assert.That(r.StatusCode, Is.EqualTo(HttpStatusCode.Conflict));
    }

    [Test]
    public async Task AsignarDepartamentosASupervisor_Supervisor_Devuelve403()
    {
        ComoSupervisor();
        var r = await _client.PutAsJsonAsync($"/api/supervisors/{SupervisorId}/departments",
            new AssignSupervisorDepartmentsDto { DepartmentIds = { DeptoAjeno } });

        Assert.That(r.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
    }

    [Test]
    public async Task CambiarRol_Supervisor_Devuelve403SinLlamarAlServicio()
    {
        ComoSupervisor();
        var r = await _client.PutAsJsonAsync("/api/users/11/role", new ChangeRoleDto { RoleId = SystemRoles.AdminEmpresaRoleId });

        Assert.That(r.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
        _factory.UserRoleService.Verify(
            s => s.ChangeRoleAsync(It.IsAny<Actor>(), It.IsAny<int>(), It.IsAny<ChangeRoleDto>()), Times.Never);
    }

    [Test]
    public async Task CambiarRol_AdminSeQuitaSuRol_Devuelve409ConMensaje()
    {
        _factory.UserRoleService.Setup(s => s.ChangeRoleAsync(It.IsAny<Actor>(), 10, It.IsAny<ChangeRoleDto>()))
            .ThrowsAsync(new BusinessRuleException(UserRoleService.MensajeQuitarsePropioRol));
        ComoAdmin();

        var r = await _client.PutAsJsonAsync("/api/users/10/role", new ChangeRoleDto { RoleId = SystemRoles.UsuarioRoleId });
        var cuerpo = await r.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Multiple(() =>
        {
            Assert.That(r.StatusCode, Is.EqualTo(HttpStatusCode.Conflict));
            Assert.That(cuerpo.GetProperty("message").GetString(), Is.EqualTo(UserRoleService.MensajeQuitarsePropioRol));
        });
    }

    [Test]
    public async Task CambiarRol_AdminEmpresa_PasaElActorDelToken()
    {
        Actor? recibido = null;
        _factory.UserRoleService.Setup(s => s.ChangeRoleAsync(It.IsAny<Actor>(), 11, It.IsAny<ChangeRoleDto>()))
            .Callback<Actor, int, ChangeRoleDto>((a, _, _) => recibido = a)
            .ReturnsAsync(new UserRoleDto { Id = 11, Role = SystemRoles.SupervisorRoleName });
        ComoAdmin();

        var r = await _client.PutAsJsonAsync("/api/users/11/role", new ChangeRoleDto { RoleId = SystemRoles.SupervisorRoleId });

        Assert.Multiple(() =>
        {
            Assert.That(r.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(recibido, Is.EqualTo(new Actor(10, Empresa, SystemRoles.AdminEmpresaRoleName)));
        });
    }

    // ---------- PUT /api/users/{id}/department (Supervisor solo en sus departamentos) ----------

    private void UsuarioEnDepartamento(int userId, int? deptoId) =>
        _factory.UserRoleService.Setup(s => s.GetUserAsync(Empresa, userId))
            .ReturnsAsync(new UserRoleDto { Id = userId, RoleId = SystemRoles.UsuarioRoleId, DepartmentId = deptoId });

    [Test]
    public async Task MoverUsuario_SupervisorHaciaDepartamentoAjeno_Devuelve403()
    {
        UsuarioEnDepartamento(30, DeptoSupervisado);
        ComoSupervisor();

        var r = await _client.PutAsJsonAsync("/api/users/30/department", new ChangeUserDepartmentDto { DepartmentId = DeptoAjeno });

        Assert.That(r.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
        _factory.UserRoleService.Verify(s => s.ChangeDepartmentAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>()), Times.Never);
    }

    [Test]
    public async Task MoverUsuario_SupervisorConUsuarioDeDepartamentoAjeno_Devuelve403()
    {
        UsuarioEnDepartamento(30, DeptoAjeno);
        ComoSupervisor();

        var r = await _client.PutAsJsonAsync("/api/users/30/department", new ChangeUserDepartmentDto { DepartmentId = DeptoSupervisado });

        Assert.That(r.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
    }

    [Test]
    public async Task MoverUsuario_SupervisorConUsuarioSinDepartamento_Devuelve403()
    {
        UsuarioEnDepartamento(30, null);
        ComoSupervisor();

        var r = await _client.PutAsJsonAsync("/api/users/30/department", new ChangeUserDepartmentDto { DepartmentId = DeptoSupervisado });

        Assert.That(r.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
    }

    [Test]
    public async Task MoverUsuario_SupervisorDentroDeSusDepartamentos_Devuelve200()
    {
        UsuarioEnDepartamento(30, DeptoSupervisado);
        _factory.UserRoleService.Setup(s => s.ChangeDepartmentAsync(Empresa, 30, DeptoSupervisado))
            .ReturnsAsync(new UserRoleDto { Id = 30, DepartmentId = DeptoSupervisado });
        ComoSupervisor();

        var r = await _client.PutAsJsonAsync("/api/users/30/department", new ChangeUserDepartmentDto { DepartmentId = DeptoSupervisado });

        Assert.That(r.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }

    [Test]
    public async Task MoverUsuario_AdminEmpresaConUsuarioSinDepartamento_Devuelve200()
    {
        UsuarioEnDepartamento(30, null);
        _factory.UserRoleService.Setup(s => s.ChangeDepartmentAsync(Empresa, 30, DeptoAjeno))
            .ReturnsAsync(new UserRoleDto { Id = 30, DepartmentId = DeptoAjeno });
        ComoAdmin();

        var r = await _client.PutAsJsonAsync("/api/users/30/department", new ChangeUserDepartmentDto { DepartmentId = DeptoAjeno });

        Assert.That(r.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }

    [Test]
    public async Task MoverUsuario_Usuario_Devuelve403()
    {
        ComoUsuario();
        var r = await _client.PutAsJsonAsync("/api/users/30/department", new ChangeUserDepartmentDto { DepartmentId = DeptoSupervisado });
        Assert.That(r.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
    }
}
