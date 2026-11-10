using KnovaHub.ApplicationLayer.DTOs;
using KnovaHub.ApplicationLayer.Services;
using KnovaHub.DomainLayer.Entities;
using KnovaHub.DomainLayer.Exceptions;
using KnovaHub.DomainLayer.Repository;
using KnovaHub.Tests.Helpers;
using Moq;

namespace KnovaHub.Tests.Services;

/// <summary>
/// Reglas del flujo de altas: el Supervisor solicita en sus departamentos, la solicitud
/// queda Pendiente, y el AdminEmpresa la aprueba (creando el usuario con contraseña temporal)
/// o la rechaza.
/// </summary>
[TestFixture]
public class RegistrationRequestServiceTests
{
    private const int Empresa = 5;
    private const int AdminId = 10;
    private const int SupervisorId = 20;
    private const int DeptoVentas = 7;
    private const int DeptoSoporte = 8;

    private static readonly Actor Admin = new(AdminId, Empresa, SystemRoles.AdminEmpresaRoleName);
    private static readonly Actor Supervisor = new(SupervisorId, Empresa, SystemRoles.SupervisorRoleName);

    private Mock<IRegistrationRequestRepository> _requests = null!;
    private Mock<IUserRepository> _users = null!;
    private Mock<IDepartmentRepository> _departments = null!;
    private RegistrationRequestService _service = null!;

    [SetUp]
    public void SetUp()
    {
        _requests = new Mock<IRegistrationRequestRepository>();
        _users = new Mock<IUserRepository>();
        _departments = new Mock<IDepartmentRepository>();
        _service = new RegistrationRequestService(_requests.Object, _users.Object, _departments.Object);

        _departments.Setup(d => d.GetByIdAsync(Empresa, DeptoVentas))
            .ReturnsAsync(new Department { Id = DeptoVentas, CompanyId = Empresa, Name = "Ventas", IsActive = true });
        _departments.Setup(d => d.IsSupervisorOfAsync(SupervisorId, DeptoVentas)).ReturnsAsync(true);
        _departments.Setup(d => d.IsSupervisorOfAsync(SupervisorId, DeptoSoporte)).ReturnsAsync(false);
    }

    private static CreateRegistrationRequestDto AltaValida(int departmentId = DeptoVentas) => new()
    {
        FullName = "Ana Gómez",
        Email = "ana@empresa.com",
        DepartmentId = departmentId
    };

    private static UserRegistrationRequest SolicitudPendiente(int id = 1) => new()
    {
        Id = id,
        CompanyId = Empresa,
        DepartmentId = DeptoVentas,
        RequestedById = SupervisorId,
        FullName = "Ana Gómez",
        Email = "ana@empresa.com",
        Status = RegistrationRequestStatus.Pending
    };

    // ---------- Crear solicitud ----------

    [Test]
    public async Task Create_SupervisorEnSuDepartamento_CreaPendiente()
    {
        UserRegistrationRequest? capturada = null;
        _requests.Setup(r => r.AddAsync(It.IsAny<UserRegistrationRequest>()))
            .Callback<UserRegistrationRequest>(r => capturada = r)
            .ReturnsAsync((UserRegistrationRequest r) => { r.Id = 7; return r; });

        var r = await _service.CreateAsync(Supervisor, AltaValida());

        Assert.Multiple(() =>
        {
            Assert.That(capturada!.Status, Is.EqualTo(RegistrationRequestStatus.Pending));
            Assert.That(capturada.RequestedById, Is.EqualTo(SupervisorId));
            Assert.That(capturada.DepartmentId, Is.EqualTo(DeptoVentas));
            Assert.That(r.Status, Is.EqualTo("Pending"));
        });
    }

    [Test]
    public void Create_DepartamentoNoSupervisado_LanzaBusinessRule()
    {
        _departments.Setup(d => d.GetByIdAsync(Empresa, DeptoSoporte))
            .ReturnsAsync(new Department { Id = DeptoSoporte, CompanyId = Empresa, Name = "Soporte", IsActive = true });

        Assert.ThrowsAsync<BusinessRuleException>(() =>
            _service.CreateAsync(Supervisor, AltaValida(departmentId: DeptoSoporte)));
        _requests.Verify(r => r.AddAsync(It.IsAny<UserRegistrationRequest>()), Times.Never);
    }

    [Test]
    public void Create_DepartamentoInactivo_LanzaDataValidation()
    {
        _departments.Setup(d => d.GetByIdAsync(Empresa, DeptoSoporte))
            .ReturnsAsync(new Department { Id = DeptoSoporte, CompanyId = Empresa, Name = "Soporte", IsActive = false });

        Assert.ThrowsAsync<DataValidationException>(() =>
            _service.CreateAsync(Supervisor, AltaValida(departmentId: DeptoSoporte)));
        _requests.Verify(r => r.AddAsync(It.IsAny<UserRegistrationRequest>()), Times.Never);
    }

    [Test]
    public void Create_EmailYaRegistrado_LanzaDuplicate()
    {
        _users.Setup(u => u.EmailExistsAsync("ana@empresa.com")).ReturnsAsync(true);

        Assert.ThrowsAsync<DuplicateEntityException>(() => _service.CreateAsync(Supervisor, AltaValida()));
        _requests.Verify(r => r.AddAsync(It.IsAny<UserRegistrationRequest>()), Times.Never);
    }

    [Test]
    public void Create_EmailConOtraSolicitudPendiente_LanzaDuplicate()
    {
        _users.Setup(u => u.EmailExistsAsync("ana@empresa.com")).ReturnsAsync(false);
        _requests.Setup(r => r.PendingEmailExistsAsync("ana@empresa.com")).ReturnsAsync(true);

        Assert.ThrowsAsync<DuplicateEntityException>(() => _service.CreateAsync(Supervisor, AltaValida()));
        _requests.Verify(r => r.AddAsync(It.IsAny<UserRegistrationRequest>()), Times.Never);
    }

    [Test]
    public void Create_AdminNoEsSupervisor_LanzaBusinessRule()
    {
        Assert.ThrowsAsync<BusinessRuleException>(() => _service.CreateAsync(Admin, AltaValida()));
        _requests.Verify(r => r.AddAsync(It.IsAny<UserRegistrationRequest>()), Times.Never);
    }

    // ---------- Aprobar ----------

    [Test]
    public async Task Approve_Pendiente_CreaUsuarioConContrasenaTemporal()
    {
        var solicitud = SolicitudPendiente();
        _requests.Setup(r => r.GetByIdAsync(Empresa, 1)).ReturnsAsync(solicitud);
        _users.Setup(u => u.EmailExistsAsync("ana@empresa.com")).ReturnsAsync(false);
        _requests.Setup(r => r.ApproveAsync(solicitud, It.IsAny<string>(), AdminId))
            .Callback<UserRegistrationRequest, string, int>((r, _, _) => r.Status = RegistrationRequestStatus.Approved)
            .ReturnsAsync((UserRegistrationRequest _, string hash, int _) => new User { Id = 99, PasswordHash = hash });

        var r = await _service.ApproveAsync(Admin, 1);

        Assert.Multiple(() =>
        {
            Assert.That(r.UserId, Is.EqualTo(99));
            Assert.That(r.Request.Status, Is.EqualTo("Approved"));
            Assert.That(r.TemporaryPassword, Is.Not.Empty);
            Assert.That(PasswordPolicy.IsStrong(r.TemporaryPassword), Is.True);
        });
        _requests.Verify(r => r.ApproveAsync(solicitud, It.Is<string>(h => h.StartsWith("$2")), AdminId), Times.Once);
    }

    [Test]
    public void Approve_YaProcesada_LanzaBusinessRule()
    {
        var solicitud = SolicitudPendiente();
        solicitud.Status = RegistrationRequestStatus.Approved;
        _requests.Setup(r => r.GetByIdAsync(Empresa, 1)).ReturnsAsync(solicitud);

        Assert.ThrowsAsync<BusinessRuleException>(() => _service.ApproveAsync(Admin, 1));
        _requests.Verify(r => r.ApproveAsync(It.IsAny<UserRegistrationRequest>(), It.IsAny<string>(), It.IsAny<int>()), Times.Never);
    }

    [Test]
    public void Approve_EmailYaTomado_LanzaDuplicate()
    {
        _requests.Setup(r => r.GetByIdAsync(Empresa, 1)).ReturnsAsync(SolicitudPendiente());
        _users.Setup(u => u.EmailExistsAsync("ana@empresa.com")).ReturnsAsync(true);

        Assert.ThrowsAsync<DuplicateEntityException>(() => _service.ApproveAsync(Admin, 1));
        _requests.Verify(r => r.ApproveAsync(It.IsAny<UserRegistrationRequest>(), It.IsAny<string>(), It.IsAny<int>()), Times.Never);
    }

    [Test]
    public void Approve_DepartamentoYaInactivo_LanzaBusinessRule()
    {
        _requests.Setup(r => r.GetByIdAsync(Empresa, 1)).ReturnsAsync(SolicitudPendiente());
        _users.Setup(u => u.EmailExistsAsync("ana@empresa.com")).ReturnsAsync(false);
        _departments.Setup(d => d.GetByIdAsync(Empresa, DeptoVentas))
            .ReturnsAsync(new Department { Id = DeptoVentas, CompanyId = Empresa, Name = "Ventas", IsActive = false });

        Assert.ThrowsAsync<BusinessRuleException>(() => _service.ApproveAsync(Admin, 1));
        _requests.Verify(r => r.ApproveAsync(It.IsAny<UserRegistrationRequest>(), It.IsAny<string>(), It.IsAny<int>()), Times.Never);
    }

    [Test]
    public void Approve_DeOtraEmpresa_LanzaNotFound()
    {
        Assert.ThrowsAsync<NotFoundException>(() => _service.ApproveAsync(Admin, 999));
        _requests.Verify(r => r.ApproveAsync(It.IsAny<UserRegistrationRequest>(), It.IsAny<string>(), It.IsAny<int>()), Times.Never);
    }

    // ---------- Rechazar ----------

    [Test]
    public async Task Reject_Pendiente_LaDejaRechazada()
    {
        var solicitud = SolicitudPendiente();
        _requests.Setup(r => r.GetByIdAsync(Empresa, 1)).ReturnsAsync(solicitud);
        _requests.Setup(r => r.RejectAsync(solicitud, AdminId))
            .Callback<UserRegistrationRequest, int>((r, _) => r.Status = RegistrationRequestStatus.Rejected)
            .Returns(Task.CompletedTask);

        var r = await _service.RejectAsync(Admin, 1);

        _requests.Verify(r => r.RejectAsync(solicitud, AdminId), Times.Once);
        Assert.That(r.Status, Is.EqualTo("Rejected"));
    }

    [Test]
    public void Reject_YaProcesada_LanzaBusinessRule()
    {
        var solicitud = SolicitudPendiente();
        solicitud.Status = RegistrationRequestStatus.Rejected;
        _requests.Setup(r => r.GetByIdAsync(Empresa, 1)).ReturnsAsync(solicitud);

        Assert.ThrowsAsync<BusinessRuleException>(() => _service.RejectAsync(Admin, 1));
        _requests.Verify(r => r.RejectAsync(It.IsAny<UserRegistrationRequest>(), It.IsAny<int>()), Times.Never);
    }

    [Test]
    public void Reject_DeOtraEmpresa_LanzaNotFound()
    {
        Assert.ThrowsAsync<NotFoundException>(() => _service.RejectAsync(Admin, 999));
        _requests.Verify(r => r.RejectAsync(It.IsAny<UserRegistrationRequest>(), It.IsAny<int>()), Times.Never);
    }
}
