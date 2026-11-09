using KnovaHub.ApplicationLayer.DTOs;
using KnovaHub.ApplicationLayer.Services;
using KnovaHub.DomainLayer.Entities;
using KnovaHub.DomainLayer.Exceptions;
using KnovaHub.DomainLayer.Repository;
using KnovaHub.Tests.Helpers;
using Moq;

namespace KnovaHub.Tests.Services;

/// <summary>
/// Reglas del módulo Usuarios: el AdminEmpresa gestiona toda su empresa, el Supervisor
/// solo ve a los de sus departamentos, y la creación genera una contraseña temporal.
/// </summary>
[TestFixture]
public class UserServiceTests
{
    private const int Empresa = 5;
    private const int AdminId = 10;
    private const int SupervisorId = 20;
    private const int DeptoVentas = 7;
    private const int DeptoSoporte = 8;

    private static readonly Actor Admin = new(AdminId, Empresa, SystemRoles.AdminEmpresaRoleName);
    private static readonly Actor Supervisor = new(SupervisorId, Empresa, SystemRoles.SupervisorRoleName);

    private Mock<IUserRepository> _users = null!;
    private Mock<IDepartmentRepository> _departments = null!;
    private UserService _service = null!;

    [SetUp]
    public void SetUp()
    {
        _users = new Mock<IUserRepository>();
        _departments = new Mock<IDepartmentRepository>();
        _service = new UserService(_users.Object, _departments.Object);

        _departments.Setup(d => d.GetByIdAsync(Empresa, DeptoVentas))
            .ReturnsAsync(new Department { Id = DeptoVentas, CompanyId = Empresa, Name = "Ventas", IsActive = true });

        _users.Setup(u => u.AddAsync(It.IsAny<User>()))
            .ReturnsAsync((User u) => { u.Id = 42; return u; });
    }

    private static CreateUserDto AltaValida(int roleId = SystemRoles.UsuarioRoleId, int? departmentId = DeptoVentas) => new()
    {
        FullName = "Ana Gómez",
        Email = "ana@empresa.com",
        RoleId = roleId,
        DepartmentId = departmentId
    };

    // ---------- Listar ----------

    [Test]
    public async Task GetVisible_AdminEmpresa_DevuelveLosDeSuEmpresa()
    {
        _users.Setup(u => u.GetByCompanyAsync(Empresa))
            .ReturnsAsync(new List<User> { TestData.UsuarioDeEmpresa(11, SystemRoles.UsuarioRoleId, departmentId: DeptoVentas) });

        var r = await _service.GetVisibleAsync(Admin);

        Assert.That(r, Has.Count.EqualTo(1));
        _users.Verify(u => u.GetByCompanyAsync(Empresa), Times.Once);
    }

    [Test]
    public async Task GetVisible_Supervisor_DevuelveSoloLosDeSusDepartamentos()
    {
        _departments.Setup(d => d.GetSupervisedAsync(SupervisorId))
            .ReturnsAsync(new List<Department> { new() { Id = DeptoVentas } });
        _users.Setup(u => u.GetByDepartmentIdsAsync(It.Is<IReadOnlyCollection<int>>(x => x.Contains(DeptoVentas))))
            .ReturnsAsync(new List<User> { TestData.UsuarioDeEmpresa(11, SystemRoles.UsuarioRoleId, departmentId: DeptoVentas) });

        var r = await _service.GetVisibleAsync(Supervisor);

        Assert.That(r, Has.Count.EqualTo(1));
        _users.Verify(u => u.GetByCompanyAsync(It.IsAny<int>()), Times.Never);
    }

    [Test]
    public async Task GetVisible_ActorSinEmpresa_DevuelveVacio()
    {
        var plataforma = new Actor(1, null, SystemRoles.AdminPlataformaRoleName);

        var r = await _service.GetVisibleAsync(plataforma);

        Assert.That(r, Is.Empty);
    }

    // ---------- Ver uno ----------

    [Test]
    public void Get_SupervisorConUsuarioDeOtroDepartamento_LanzaNotFound()
    {
        _users.Setup(u => u.GetByIdInCompanyAsync(Empresa, 11))
            .ReturnsAsync(TestData.UsuarioDeEmpresa(11, SystemRoles.UsuarioRoleId, departmentId: DeptoSoporte));
        _departments.Setup(d => d.IsSupervisorOfAsync(SupervisorId, DeptoSoporte)).ReturnsAsync(false);

        Assert.ThrowsAsync<NotFoundException>(() => _service.GetAsync(Supervisor, 11));
    }

    [Test]
    public async Task Get_SupervisorConUsuarioDeSuDepartamento_DevuelveElUsuario()
    {
        _users.Setup(u => u.GetByIdInCompanyAsync(Empresa, 11))
            .ReturnsAsync(TestData.UsuarioDeEmpresa(11, SystemRoles.UsuarioRoleId, departmentId: DeptoVentas));
        _departments.Setup(d => d.IsSupervisorOfAsync(SupervisorId, DeptoVentas)).ReturnsAsync(true);

        var r = await _service.GetAsync(Supervisor, 11);

        Assert.That(r.DepartmentId, Is.EqualTo(DeptoVentas));
    }

    // ---------- Crear ----------

    [Test]
    public async Task Create_UsuarioConDepartamento_GeneraContrasenaTemporal()
    {
        User? capturado = null;
        _users.Setup(u => u.EmailExistsAsync("ana@empresa.com")).ReturnsAsync(false);
        _users.Setup(u => u.AddAsync(It.IsAny<User>()))
            .Callback<User>(u => capturado = u)
            .ReturnsAsync((User u) => { u.Id = 42; return u; });

        var r = await _service.CreateAsync(Empresa, AltaValida());

        Assert.Multiple(() =>
        {
            Assert.That(r.TemporaryPassword, Is.Not.Empty);
            Assert.That(capturado!.MustChangePassword, Is.True);
            Assert.That(capturado.PasswordHash, Does.StartWith("$2"));
            Assert.That(capturado.IsActive, Is.True);
            Assert.That(capturado.DepartmentId, Is.EqualTo(DeptoVentas));
            Assert.That(PasswordPolicy.IsStrong(r.TemporaryPassword), Is.True);
        });
    }

    [Test]
    public void Create_RolNoAsignable_LanzaDataValidation()
    {
        Assert.ThrowsAsync<DataValidationException>(() =>
            _service.CreateAsync(Empresa, AltaValida(roleId: SystemRoles.AdminPlataformaRoleId)));
        _users.Verify(u => u.AddAsync(It.IsAny<User>()), Times.Never);
    }

    [Test]
    public void Create_EmailYaRegistrado_LanzaDuplicate()
    {
        _users.Setup(u => u.EmailExistsAsync("ana@empresa.com")).ReturnsAsync(true);

        Assert.ThrowsAsync<DuplicateEntityException>(() => _service.CreateAsync(Empresa, AltaValida()));
        _users.Verify(u => u.AddAsync(It.IsAny<User>()), Times.Never);
    }

    [Test]
    public void Create_UsuarioSinDepartamento_LanzaDataValidation()
    {
        Assert.ThrowsAsync<DataValidationException>(() =>
            _service.CreateAsync(Empresa, AltaValida(roleId: SystemRoles.UsuarioRoleId, departmentId: null)));
        _users.Verify(u => u.AddAsync(It.IsAny<User>()), Times.Never);
    }

    [Test]
    public void Create_UsuarioConDepartamentoInactivo_LanzaDataValidation()
    {
        _departments.Setup(d => d.GetByIdAsync(Empresa, DeptoSoporte))
            .ReturnsAsync(new Department { Id = DeptoSoporte, CompanyId = Empresa, Name = "Soporte", IsActive = false });

        Assert.ThrowsAsync<DataValidationException>(() =>
            _service.CreateAsync(Empresa, AltaValida(roleId: SystemRoles.UsuarioRoleId, departmentId: DeptoSoporte)));
        _users.Verify(u => u.AddAsync(It.IsAny<User>()), Times.Never);
    }

    [Test]
    public async Task Create_Supervisor_NoLlevaDepartamento()
    {
        _users.Setup(u => u.EmailExistsAsync("ana@empresa.com")).ReturnsAsync(false);

        var r = await _service.CreateAsync(Empresa, AltaValida(roleId: SystemRoles.SupervisorRoleId));

        Assert.That(r.User.DepartmentId, Is.Null);
    }

    // ---------- Actualizar ----------

    [Test]
    public void Update_EmailCambiadoAOcupado_LanzaDuplicate()
    {
        _users.Setup(u => u.GetByIdInCompanyAsync(Empresa, 11))
            .ReturnsAsync(TestData.UsuarioDeEmpresa(11, SystemRoles.UsuarioRoleId, departmentId: DeptoVentas));
        _users.Setup(u => u.EmailExistsAsync("nuevo@empresa.com")).ReturnsAsync(true);

        Assert.ThrowsAsync<DuplicateEntityException>(() => _service.UpdateAsync(Empresa, 11,
            new UpdateUserDto { FullName = "Ana Gómez", Email = "nuevo@empresa.com" }));
        _users.Verify(u => u.UpdateAsync(It.IsAny<User>()), Times.Never);
    }

    [Test]
    public async Task Update_Valido_GuardaLosDatos()
    {
        var existente = TestData.UsuarioDeEmpresa(11, SystemRoles.UsuarioRoleId, departmentId: DeptoVentas);
        _users.Setup(u => u.GetByIdInCompanyAsync(Empresa, 11)).ReturnsAsync(existente);
        _users.Setup(u => u.EmailExistsAsync("ana@empresa.com")).ReturnsAsync(false);

        var r = await _service.UpdateAsync(Empresa, 11,
            new UpdateUserDto { FullName = "Ana Gómez", Email = "ana@empresa.com" });

        _users.Verify(u => u.UpdateAsync(existente), Times.Once);
        Assert.That(r.Email, Is.EqualTo("ana@empresa.com"));
    }

    [Test]
    public void Update_UsuarioDeOtraEmpresa_LanzaNotFound()
    {
        Assert.ThrowsAsync<NotFoundException>(() => _service.UpdateAsync(Empresa, 999,
            new UpdateUserDto { FullName = "X", Email = "x@y.com" }));
        _users.Verify(u => u.UpdateAsync(It.IsAny<User>()), Times.Never);
    }

    // ---------- Activar / desactivar ----------

    [Test]
    public void SetActive_AdminSeDesactivaASiMismo_LanzaBusinessRule()
    {
        _users.Setup(u => u.GetByIdInCompanyAsync(Empresa, AdminId))
            .ReturnsAsync(TestData.UsuarioDeEmpresa(AdminId, SystemRoles.AdminEmpresaRoleId));

        var ex = Assert.ThrowsAsync<BusinessRuleException>(() => _service.SetActiveAsync(Admin, AdminId, false));

        Assert.That(ex!.Message, Is.EqualTo(UserService.MensajeDesactivarseASiMismo));
        _users.Verify(u => u.UpdateAsync(It.IsAny<User>()), Times.Never);
    }

    [Test]
    public async Task SetActive_DesactivarAOtraPersona_Guarda()
    {
        var existente = TestData.UsuarioDeEmpresa(11, SystemRoles.UsuarioRoleId, departmentId: DeptoVentas);
        _users.Setup(u => u.GetByIdInCompanyAsync(Empresa, 11)).ReturnsAsync(existente);

        var r = await _service.SetActiveAsync(Admin, 11, false);

        _users.Verify(u => u.UpdateAsync(existente), Times.Once);
        Assert.That(r.IsActive, Is.False);
    }
}
