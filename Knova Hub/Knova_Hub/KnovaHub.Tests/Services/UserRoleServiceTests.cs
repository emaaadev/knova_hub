using KnovaHub.ApplicationLayer.DTOs;
using KnovaHub.ApplicationLayer.Services;
using KnovaHub.DomainLayer.Entities;
using KnovaHub.DomainLayer.Repository;
using KnovaHub.DomainLayer.Exceptions;
using KnovaHub.Tests.Helpers;
using Moq;

namespace KnovaHub.Tests.Services;

/// <summary>
/// Reglas de cambio de rol: AdminPlataforma solo por seed, el AdminEmpresa no se quita su rol,
/// la empresa mantiene al menos un AdminEmpresa activo y cada Usuario pertenece a un departamento.
/// </summary>
[TestFixture]
public class UserRoleServiceTests
{
    private const int Empresa = 5;
    private const int AdminId = 10;
    private const int OtroId = 11;
    private const int DeptoVentas = 7;

    private static readonly Actor Admin = new(AdminId, Empresa, SystemRoles.AdminEmpresaRoleName);

    private Mock<IUserRepository> _users = null!;
    private Mock<IDepartmentRepository> _departments = null!;
    private UserRoleService _service = null!;

    [SetUp]
    public void SetUp()
    {
        _users = new Mock<IUserRepository>();
        _departments = new Mock<IDepartmentRepository>();
        _service = new UserRoleService(_users.Object, _departments.Object);

        _departments.Setup(d => d.GetByIdAsync(Empresa, DeptoVentas))
            .ReturnsAsync(new Department { Id = DeptoVentas, CompanyId = Empresa, Name = "Ventas", IsActive = true });
        _users.Setup(u => u.ChangeRoleKeepingCompanyAdminAsync(
                It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int?>()))
            .ReturnsAsync(true);
    }

    private void Existe(User user) =>
        _users.Setup(u => u.GetByIdInCompanyAsync(Empresa, user.Id)).ReturnsAsync(user);

    private void NoSeGuardoNada() =>
        _users.Verify(u => u.ChangeRoleKeepingCompanyAdminAsync(
            It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int?>()), Times.Never);

    [TestCase(SystemRoles.AdminPlataformaRoleId, TestName = "ChangeRole_AsignarAdminPlataforma_Rechazado")]
    [TestCase(0, TestName = "ChangeRole_RolInexistente0_Rechazado")]
    [TestCase(99, TestName = "ChangeRole_RolInexistente99_Rechazado")]
    public void ChangeRole_RolNoAsignable_LanzaDataValidation(int roleId)
    {
        Existe(TestData.UsuarioDeEmpresa(OtroId, SystemRoles.UsuarioRoleId, departmentId: DeptoVentas));

        Assert.ThrowsAsync<DataValidationException>(() =>
            _service.ChangeRoleAsync(Admin, OtroId, new ChangeRoleDto { RoleId = roleId }));
        NoSeGuardoNada();
    }

    [Test]
    public void ChangeRole_UsuarioDeOtraEmpresa_LanzaNotFound()
    {
        // GetByIdInCompanyAsync devuelve null cuando el usuario es de otra empresa.
        Assert.ThrowsAsync<NotFoundException>(() =>
            _service.ChangeRoleAsync(Admin, 999, new ChangeRoleDto { RoleId = SystemRoles.SupervisorRoleId }));
        NoSeGuardoNada();
    }

    [TestCase(SystemRoles.SupervisorRoleId, TestName = "ChangeRole_AdminSeBajaASupervisor_Rechazado")]
    [TestCase(SystemRoles.UsuarioRoleId, TestName = "ChangeRole_AdminSeBajaAUsuario_Rechazado")]
    public void ChangeRole_AdminSeQuitaSuPropioRol_LanzaBusinessRule(int nuevoRol)
    {
        Existe(TestData.UsuarioDeEmpresa(AdminId, SystemRoles.AdminEmpresaRoleId));

        var ex = Assert.ThrowsAsync<BusinessRuleException>(() =>
            _service.ChangeRoleAsync(Admin, AdminId, new ChangeRoleDto { RoleId = nuevoRol, DepartmentId = DeptoVentas }));

        Assert.That(ex!.Message, Is.EqualTo(UserRoleService.MensajeQuitarsePropioRol));
        NoSeGuardoNada();
    }

    [Test]
    public async Task ChangeRole_AdminSeAsignaAdminEmpresaOtraVez_Permitido()
    {
        Existe(TestData.UsuarioDeEmpresa(AdminId, SystemRoles.AdminEmpresaRoleId));

        var r = await _service.ChangeRoleAsync(Admin, AdminId, new ChangeRoleDto { RoleId = SystemRoles.AdminEmpresaRoleId });

        Assert.That(r.Role, Is.EqualTo(SystemRoles.AdminEmpresaRoleName));
    }

    [Test]
    public void ChangeRole_QuitarRolAlUltimoAdminActivo_LanzaBusinessRule()
    {
        Existe(TestData.UsuarioDeEmpresa(OtroId, SystemRoles.AdminEmpresaRoleId));
        _users.Setup(u => u.ChangeRoleKeepingCompanyAdminAsync(Empresa, OtroId, SystemRoles.SupervisorRoleId, null))
            .ReturnsAsync(false);

        var ex = Assert.ThrowsAsync<BusinessRuleException>(() =>
            _service.ChangeRoleAsync(Admin, OtroId, new ChangeRoleDto { RoleId = SystemRoles.SupervisorRoleId }));

        Assert.That(ex!.Message, Is.EqualTo(UserRoleService.MensajeUltimoAdmin));
    }

    [Test]
    public void ChangeRole_AUsuarioSinDepartamento_LanzaDataValidation()
    {
        Existe(TestData.UsuarioDeEmpresa(OtroId, SystemRoles.SupervisorRoleId));

        var ex = Assert.ThrowsAsync<DataValidationException>(() =>
            _service.ChangeRoleAsync(Admin, OtroId, new ChangeRoleDto { RoleId = SystemRoles.UsuarioRoleId }));

        Assert.That(ex!.Message, Does.Contain("departamento"));
        NoSeGuardoNada();
    }

    [Test]
    public void ChangeRole_AUsuarioConDepartamentoDeOtraEmpresa_LanzaDataValidation()
    {
        Existe(TestData.UsuarioDeEmpresa(OtroId, SystemRoles.SupervisorRoleId));

        Assert.ThrowsAsync<DataValidationException>(() =>
            _service.ChangeRoleAsync(Admin, OtroId, new ChangeRoleDto { RoleId = SystemRoles.UsuarioRoleId, DepartmentId = 500 }));
        NoSeGuardoNada();
    }

    [Test]
    public void ChangeRole_AUsuarioConDepartamentoInactivo_LanzaDataValidation()
    {
        Existe(TestData.UsuarioDeEmpresa(OtroId, SystemRoles.SupervisorRoleId));
        _departments.Setup(d => d.GetByIdAsync(Empresa, 8))
            .ReturnsAsync(new Department { Id = 8, CompanyId = Empresa, Name = "Viejo", IsActive = false });

        Assert.ThrowsAsync<DataValidationException>(() =>
            _service.ChangeRoleAsync(Admin, OtroId, new ChangeRoleDto { RoleId = SystemRoles.UsuarioRoleId, DepartmentId = 8 }));
        NoSeGuardoNada();
    }

    [Test]
    public async Task ChangeRole_AUsuarioConDepartamentoValido_GuardaRolYDepartamento()
    {
        Existe(TestData.UsuarioDeEmpresa(OtroId, SystemRoles.SupervisorRoleId));

        var r = await _service.ChangeRoleAsync(Admin, OtroId,
            new ChangeRoleDto { RoleId = SystemRoles.UsuarioRoleId, DepartmentId = DeptoVentas });

        _users.Verify(u => u.ChangeRoleKeepingCompanyAdminAsync(Empresa, OtroId, SystemRoles.UsuarioRoleId, DeptoVentas), Times.Once);
        Assert.Multiple(() =>
        {
            Assert.That(r.Role, Is.EqualTo(SystemRoles.UsuarioRoleName));
            Assert.That(r.DepartmentId, Is.EqualTo(DeptoVentas));
        });
    }

    [Test]
    public async Task ChangeRole_ASupervisor_IgnoraElDepartamentoEnviado()
    {
        Existe(TestData.UsuarioDeEmpresa(OtroId, SystemRoles.UsuarioRoleId, departmentId: DeptoVentas));

        var r = await _service.ChangeRoleAsync(Admin, OtroId,
            new ChangeRoleDto { RoleId = SystemRoles.SupervisorRoleId, DepartmentId = DeptoVentas });

        _users.Verify(u => u.ChangeRoleKeepingCompanyAdminAsync(Empresa, OtroId, SystemRoles.SupervisorRoleId, null), Times.Once);
        Assert.That(r.DepartmentId, Is.Null);
    }

    [Test]
    public void ChangeDepartment_UsuarioQueNoEsRolUsuario_LanzaDataValidation()
    {
        Existe(TestData.UsuarioDeEmpresa(OtroId, SystemRoles.SupervisorRoleId));

        Assert.ThrowsAsync<DataValidationException>(() => _service.ChangeDepartmentAsync(Empresa, OtroId, DeptoVentas));
        _users.Verify(u => u.UpdateDepartmentAsync(It.IsAny<int>(), It.IsAny<int>()), Times.Never);
    }

    [Test]
    public async Task ChangeDepartment_Valido_ActualizaElDepartamento()
    {
        Existe(TestData.UsuarioDeEmpresa(OtroId, SystemRoles.UsuarioRoleId, departmentId: 3));

        var r = await _service.ChangeDepartmentAsync(Empresa, OtroId, DeptoVentas);

        _users.Verify(u => u.UpdateDepartmentAsync(OtroId, DeptoVentas), Times.Once);
        Assert.That(r.DepartmentId, Is.EqualTo(DeptoVentas));
    }

    // ---------- Cambio de rol: casos adicionales ----------

    [Test]
    public async Task ChangeRole_AdminBajaAOtroAdmin_Permitido()
    {
        Existe(TestData.UsuarioDeEmpresa(OtroId, SystemRoles.AdminEmpresaRoleId));

        var r = await _service.ChangeRoleAsync(Admin, OtroId,
            new ChangeRoleDto { RoleId = SystemRoles.SupervisorRoleId });

        _users.Verify(u => u.ChangeRoleKeepingCompanyAdminAsync(
            Empresa, OtroId, SystemRoles.SupervisorRoleId, null), Times.Once);
        Assert.That(r.Role, Is.EqualTo(SystemRoles.SupervisorRoleName));
    }

    [Test]
    public async Task ChangeRole_UsuarioAAdminEmpresa_LimpiaElDepartamento()
    {
        Existe(TestData.UsuarioDeEmpresa(OtroId, SystemRoles.UsuarioRoleId, departmentId: DeptoVentas));

        var r = await _service.ChangeRoleAsync(Admin, OtroId,
            new ChangeRoleDto { RoleId = SystemRoles.AdminEmpresaRoleId });

        _users.Verify(u => u.ChangeRoleKeepingCompanyAdminAsync(
            Empresa, OtroId, SystemRoles.AdminEmpresaRoleId, null), Times.Once);
        Assert.That(r.DepartmentId, Is.Null);
    }

    [Test]
    public void ChangeRole_ActorSinEmpresa_LanzaNotFound()
    {
        var plataforma = new Actor(1, null, SystemRoles.AdminPlataformaRoleName);

        Assert.ThrowsAsync<NotFoundException>(() =>
            _service.ChangeRoleAsync(plataforma, OtroId, new ChangeRoleDto { RoleId = SystemRoles.SupervisorRoleId }));
        NoSeGuardoNada();
    }

    // ---------- Mover de departamento: casos adicionales ----------

    [Test]
    public void ChangeDepartment_DepartamentoInactivo_LanzaDataValidation()
    {
        Existe(TestData.UsuarioDeEmpresa(OtroId, SystemRoles.UsuarioRoleId, departmentId: DeptoVentas));
        _departments.Setup(d => d.GetByIdAsync(Empresa, 8))
            .ReturnsAsync(new Department { Id = 8, CompanyId = Empresa, Name = "Viejo", IsActive = false });

        Assert.ThrowsAsync<DataValidationException>(() => _service.ChangeDepartmentAsync(Empresa, OtroId, 8));
        _users.Verify(u => u.UpdateDepartmentAsync(It.IsAny<int>(), It.IsAny<int>()), Times.Never);
    }

    [Test]
    public void ChangeDepartment_DepartamentoDeOtraEmpresa_LanzaDataValidation()
    {
        Existe(TestData.UsuarioDeEmpresa(OtroId, SystemRoles.UsuarioRoleId, departmentId: DeptoVentas));

        Assert.ThrowsAsync<DataValidationException>(() => _service.ChangeDepartmentAsync(Empresa, OtroId, 500));
        _users.Verify(u => u.UpdateDepartmentAsync(It.IsAny<int>(), It.IsAny<int>()), Times.Never);
    }

    [Test]
    public void ChangeDepartment_UsuarioDeOtraEmpresa_LanzaNotFound()
    {
        Assert.ThrowsAsync<NotFoundException>(() => _service.ChangeDepartmentAsync(Empresa, 999, DeptoVentas));
        _users.Verify(u => u.UpdateDepartmentAsync(It.IsAny<int>(), It.IsAny<int>()), Times.Never);
    }

    // ---------- Consultar un usuario ----------

    [Test]
    public async Task GetUser_Valido_DevuelveRolYDepartamento()
    {
        Existe(TestData.UsuarioDeEmpresa(OtroId, SystemRoles.UsuarioRoleId, departmentId: DeptoVentas));

        var r = await _service.GetUserAsync(Empresa, OtroId);

        Assert.Multiple(() =>
        {
            Assert.That(r.Role, Is.EqualTo(SystemRoles.UsuarioRoleName));
            Assert.That(r.DepartmentId, Is.EqualTo(DeptoVentas));
        });
    }

    [Test]
    public void GetUser_UsuarioDeOtraEmpresa_LanzaNotFound()
    {
        Assert.ThrowsAsync<NotFoundException>(() => _service.GetUserAsync(Empresa, 999));
    }
}