using KnovaHub.ApplicationLayer.DTOs;
using KnovaHub.ApplicationLayer.Services;
using KnovaHub.DomainLayer.Entities;
using KnovaHub.DomainLayer.Repository;
using KnovaHub.DomainLayer.Exceptions;
using KnovaHub.Tests.Helpers;
using Moq;

namespace KnovaHub.Tests.Services;

[TestFixture]
public class DepartmentServiceTests
{
    private const int Empresa = 5;

    private Mock<IDepartmentRepository> _departments = null!;
    private Mock<IUserRepository> _users = null!;
    private DepartmentService _service = null!;

    private static List<Department> DeptosEmpresa() => new()
    {
        new Department { Id = 1, CompanyId = Empresa, Name = "Ventas", IsActive = true },
        new Department { Id = 2, CompanyId = Empresa, Name = "Soporte", IsActive = true },
        new Department { Id = 3, CompanyId = Empresa, Name = "Cerrado", IsActive = false }
    };

    [SetUp]
    public void SetUp()
    {
        _departments = new Mock<IDepartmentRepository>();
        _users = new Mock<IUserRepository>();
        _service = new DepartmentService(_departments.Object, _users.Object);

        _departments.Setup(d => d.GetByCompanyAsync(Empresa)).ReturnsAsync(DeptosEmpresa);
        _departments.Setup(d => d.AddAsync(It.IsAny<Department>()))
            .ReturnsAsync((Department d) => { d.Id = 50; return d; });
    }

    [Test]
    public async Task GetVisible_AdminEmpresa_VeTodosLosDeSuEmpresa()
    {
        var r = await _service.GetVisibleAsync(new Actor(10, Empresa, SystemRoles.AdminEmpresaRoleName));

        Assert.That(r, Has.Count.EqualTo(3));
    }

    [Test]
    public async Task GetVisible_Supervisor_SoloVeLosSuyos()
    {
        _departments.Setup(d => d.GetSupervisedAsync(20))
            .ReturnsAsync(new List<Department> { DeptosEmpresa()[0] });

        var r = await _service.GetVisibleAsync(new Actor(20, Empresa, SystemRoles.SupervisorRoleName));

        Assert.That(r.Select(d => d.Name), Is.EqualTo(new[] { "Ventas" }));
        _departments.Verify(d => d.GetByCompanyAsync(It.IsAny<int>()), Times.Never);
    }

    [TestCase(SystemRoles.UsuarioRoleName, TestName = "GetVisible_Usuario_NoVeNinguno")]
    [TestCase(SystemRoles.AdminPlataformaRoleName, TestName = "GetVisible_AdminPlataforma_NoVeNinguno")]
    public async Task GetVisible_OtrosRoles_NoVenNinguno(string rol)
    {
        var r = await _service.GetVisibleAsync(new Actor(30, Empresa, rol));

        Assert.That(r, Is.Empty);
    }

    [Test]
    public async Task Create_NombreConEspacios_SeGuardaRecortadoEnLaEmpresaDelActor()
    {
        Department? guardado = null;
        _departments.Setup(d => d.AddAsync(It.IsAny<Department>()))
            .Callback<Department>(d => guardado = d)
            .ReturnsAsync((Department d) => d);

        await _service.CreateAsync(Empresa, new CreateDepartmentDto { Name = "  Recursos Humanos  " });

        Assert.Multiple(() =>
        {
            Assert.That(guardado!.Name, Is.EqualTo("Recursos Humanos"));
            Assert.That(guardado.CompanyId, Is.EqualTo(Empresa));
        });
    }

    [TestCase("", TestName = "Create_NombreVacio_Rechazado")]
    [TestCase("   ", TestName = "Create_NombreSoloEspacios_Rechazado")]
    public void Create_NombreInvalido_LanzaDataValidation(string nombre)
    {
        Assert.ThrowsAsync<DataValidationException>(() =>
            _service.CreateAsync(Empresa, new CreateDepartmentDto { Name = nombre }));
        _departments.Verify(d => d.AddAsync(It.IsAny<Department>()), Times.Never);
    }

    [Test]
    public void Update_DepartamentoDeOtraEmpresa_LanzaNotFound()
    {
        Assert.ThrowsAsync<NotFoundException>(() =>
            _service.UpdateAsync(Empresa, 999, new UpdateDepartmentDto { Name = "X" }));
    }

    [Test]
    public void SetSupervisorDepartments_UsuarioNoEsSupervisor_LanzaDataValidation()
    {
        _users.Setup(u => u.GetByIdInCompanyAsync(Empresa, 30))
            .ReturnsAsync(TestData.UsuarioDeEmpresa(30, SystemRoles.UsuarioRoleId));

        Assert.ThrowsAsync<DataValidationException>(() => _service.SetSupervisorDepartmentsAsync(
            Empresa, 30, new AssignSupervisorDepartmentsDto { DepartmentIds = { 1 } }));
        _departments.Verify(d => d.SetSupervisorDepartmentsAsync(It.IsAny<int>(), It.IsAny<IReadOnlyCollection<int>>()), Times.Never);
    }

    [Test]
    public void SetSupervisorDepartments_SupervisorDeOtraEmpresa_LanzaNotFound()
    {
        Assert.ThrowsAsync<NotFoundException>(() => _service.SetSupervisorDepartmentsAsync(
            Empresa, 404, new AssignSupervisorDepartmentsDto { DepartmentIds = { 1 } }));
    }

    [TestCase(99, TestName = "SetSupervisorDepartments_DepartamentoDeOtraEmpresa_Rechazado")]
    [TestCase(3, TestName = "SetSupervisorDepartments_DepartamentoInactivo_Rechazado")]
    public void SetSupervisorDepartments_DepartamentoNoValido_LanzaDataValidation(int deptoId)
    {
        _users.Setup(u => u.GetByIdInCompanyAsync(Empresa, 20))
            .ReturnsAsync(TestData.UsuarioDeEmpresa(20, SystemRoles.SupervisorRoleId));

        Assert.ThrowsAsync<DataValidationException>(() => _service.SetSupervisorDepartmentsAsync(
            Empresa, 20, new AssignSupervisorDepartmentsDto { DepartmentIds = { 1, deptoId } }));
        _departments.Verify(d => d.SetSupervisorDepartmentsAsync(It.IsAny<int>(), It.IsAny<IReadOnlyCollection<int>>()), Times.Never);
    }

    [Test]
    public async Task SetSupervisorDepartments_Valido_GuardaSinRepetidos()
    {
        _users.Setup(u => u.GetByIdInCompanyAsync(Empresa, 20))
            .ReturnsAsync(TestData.UsuarioDeEmpresa(20, SystemRoles.SupervisorRoleId));

        var r = await _service.SetSupervisorDepartmentsAsync(
            Empresa, 20, new AssignSupervisorDepartmentsDto { DepartmentIds = { 1, 2, 1 } });

        _departments.Verify(d => d.SetSupervisorDepartmentsAsync(20,
            It.Is<IReadOnlyCollection<int>>(ids => ids.Count == 2 && ids.Contains(1) && ids.Contains(2))), Times.Once);
        Assert.That(r.Select(d => d.Name), Is.EqualTo(new[] { "Soporte", "Ventas" }));
    }

    // ---------- Crear: limites del nombre ----------

    [Test]
    public void Create_NombreDeMasDe100Caracteres_Rechazado()
    {
        var ex = Assert.ThrowsAsync<DataValidationException>(() =>
            _service.CreateAsync(Empresa, new CreateDepartmentDto { Name = new string('a', 101) }));

        Assert.That(ex!.Message, Does.Contain("100 caracteres"));
        _departments.Verify(d => d.AddAsync(It.IsAny<Department>()), Times.Never);
    }

    [Test]
    public async Task Create_NombreDe100Caracteres_Aceptado()
    {
        var nombre = new string('a', 100);

        var r = await _service.CreateAsync(Empresa, new CreateDepartmentDto { Name = nombre });

        Assert.That(r.Name, Is.EqualTo(nombre));
    }

    // ---------- Ver ----------

    [Test]
    public async Task Get_Valido_DevuelveElDepartamento()
    {
        _departments.Setup(d => d.GetByIdAsync(Empresa, 1)).ReturnsAsync(DeptosEmpresa()[0]);

        var r = await _service.GetAsync(Empresa, 1);

        Assert.That(r.Name, Is.EqualTo("Ventas"));
    }

    [Test]
    public void Get_DepartamentoDeOtraEmpresa_LanzaNotFound()
    {
        Assert.ThrowsAsync<NotFoundException>(() => _service.GetAsync(Empresa, 999));
    }

    // ---------- Editar ----------

    [Test]
    public async Task Update_Valido_ActualizaNombreYEstado()
    {
        var depto = new Department { Id = 1, CompanyId = Empresa, Name = "Ventas", IsActive = true };
        _departments.Setup(d => d.GetByIdAsync(Empresa, 1)).ReturnsAsync(depto);

        var r = await _service.UpdateAsync(Empresa, 1,
            new UpdateDepartmentDto { Name = "  Comercial  ", IsActive = false });

        _departments.Verify(d => d.UpdateAsync(depto), Times.Once);
        Assert.Multiple(() =>
        {
            Assert.That(r.Name, Is.EqualTo("Comercial"));
            Assert.That(r.IsActive, Is.False);
        });
    }

    [TestCase("", TestName = "Update_NombreVacio_Rechazado")]
    [TestCase("   ", TestName = "Update_NombreSoloEspacios_Rechazado")]
    public void Update_NombreInvalido_LanzaDataValidation(string nombre)
    {
        _departments.Setup(d => d.GetByIdAsync(Empresa, 1))
            .ReturnsAsync(new Department { Id = 1, CompanyId = Empresa, Name = "Ventas", IsActive = true });

        Assert.ThrowsAsync<DataValidationException>(() =>
            _service.UpdateAsync(Empresa, 1, new UpdateDepartmentDto { Name = nombre }));
        _departments.Verify(d => d.UpdateAsync(It.IsAny<Department>()), Times.Never);
    }

    // ---------- Asignar departamentos a un Supervisor ----------

    [Test]
    public async Task SetSupervisorDepartments_ListaVacia_QuitaTodosLosDepartamentos()
    {
        _users.Setup(u => u.GetByIdInCompanyAsync(Empresa, 20))
            .ReturnsAsync(TestData.UsuarioDeEmpresa(20, SystemRoles.SupervisorRoleId));

        var r = await _service.SetSupervisorDepartmentsAsync(
            Empresa, 20, new AssignSupervisorDepartmentsDto());

        _departments.Verify(d => d.SetSupervisorDepartmentsAsync(20,
            It.Is<IReadOnlyCollection<int>>(ids => ids.Count == 0)), Times.Once);
        Assert.That(r, Is.Empty);
    }

    // ---------- Ver departamentos de un Supervisor ----------

    [Test]
    public async Task GetSupervisedDepartments_Valido_DevuelveLosSuyos()
    {
        _users.Setup(u => u.GetByIdInCompanyAsync(Empresa, 20))
            .ReturnsAsync(TestData.UsuarioDeEmpresa(20, SystemRoles.SupervisorRoleId));
        _departments.Setup(d => d.GetSupervisedAsync(20))
            .ReturnsAsync(new List<Department> { DeptosEmpresa()[0], DeptosEmpresa()[1] });

        var r = await _service.GetSupervisedDepartmentsAsync(Empresa, 20);

        Assert.That(r.Select(d => d.Name), Is.EqualTo(new[] { "Soporte", "Ventas" }));
    }

    [Test]
    public void GetSupervisedDepartments_NoEsSupervisor_LanzaDataValidation()
    {
        _users.Setup(u => u.GetByIdInCompanyAsync(Empresa, 30))
            .ReturnsAsync(TestData.UsuarioDeEmpresa(30, SystemRoles.UsuarioRoleId));

        Assert.ThrowsAsync<DataValidationException>(() => _service.GetSupervisedDepartmentsAsync(Empresa, 30));
        _departments.Verify(d => d.GetSupervisedAsync(It.IsAny<int>()), Times.Never);
    }

    [Test]
    public void GetSupervisedDepartments_DeOtraEmpresa_LanzaNotFound()
    {
        Assert.ThrowsAsync<NotFoundException>(() => _service.GetSupervisedDepartmentsAsync(Empresa, 404));
    }
}