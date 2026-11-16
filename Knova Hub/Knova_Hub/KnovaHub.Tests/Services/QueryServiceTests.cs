using KnovaHub.ApplicationLayer.Contract;
using KnovaHub.ApplicationLayer.DTOs;
using KnovaHub.ApplicationLayer.Services;
using KnovaHub.DomainLayer.Entities;
using KnovaHub.DomainLayer.Exceptions;
using KnovaHub.DomainLayer.Repository;
using Moq;
using NUnit.Framework;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace KnovaHub.Tests.Services;

[TestFixture]
public class QueryServiceTests
{
    private Mock<IDocumentRepository> _mockDocs = null!;
    private Mock<IAiService> _mockAi = null!;
    private Mock<IQueryLogRepository> _mockLog = null!;
    private Mock<IDepartmentRepository> _mockDepts = null!;
    private Mock<IUserRepository> _mockUsers = null!;
    private QueryService _service = null!;

    [SetUp]
    public void SetUp()
    {
        _mockDocs = new Mock<IDocumentRepository>();
        _mockAi = new Mock<IAiService>();
        _mockLog = new Mock<IQueryLogRepository>();
        _mockDepts = new Mock<IDepartmentRepository>();
        _mockUsers = new Mock<IUserRepository>();

        _service = new QueryService(
            _mockDocs.Object,
            _mockAi.Object,
            _mockLog.Object,
            _mockDepts.Object,
            _mockUsers.Object);
    }

    private static Actor Actor(int userId, int companyId, string role) =>
        new(userId, companyId, role);

    private static User UsuarioActivo(int userId, int companyId, int? departmentId = null) =>
        new()
        {
            Id = userId,
            CompanyId = companyId,
            DepartmentId = departmentId,
            IsActive = true
        };

    private static List<IndexedDocumentText> TextosIndexados(params IndexedDocumentText[] textos) =>
        textos.ToList();

    // ---------- Validación de pregunta ----------

    [TestCase("", TestName = "Ask_PreguntaVacia_LanzaDataValidation")]
    [TestCase("   ", TestName = "Ask_PreguntaSoloEspacios_LanzaDataValidation")]
    public void Ask_PreguntaInvalida_LanzaDataValidationException(string pregunta)
    {
        var actor = Actor(1, 5, SystemRoles.AdminEmpresaRoleName);

        Assert.ThrowsAsync<DataValidationException>(() => _service.AskAsync(actor, pregunta));

        _mockDocs.Verify(d => d.GetIndexedTextsAsync(It.IsAny<int>(), It.IsAny<int[]?>()), Times.Never);
        _mockAi.Verify(a => a.AskAsync(It.IsAny<AiRequestDto>(), It.IsAny<CancellationToken>()), Times.Never);
        _mockLog.Verify(l => l.AddAsync(It.IsAny<QueryLog>()), Times.Never);
    }

    // ---------- AdminEmpresa: visibleDepartmentIds = null ----------

    [Test]
    public async Task Ask_AdminEmpresa_LlamaGetIndexedTextsConNullDepartamentos()
    {
        var actor = Actor(1, 5, SystemRoles.AdminEmpresaRoleName);
        _mockUsers.Setup(u => u.GetByIdInCompanyAsync(5, 1)).ReturnsAsync(UsuarioActivo(1, 5));
        _mockDocs.Setup(d => d.GetIndexedTextsAsync(5, null)).ReturnsAsync(TextosIndexados());
        _mockAi.Setup(a => a.AskAsync(It.IsAny<AiRequestDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AiAnswerDto { Answer = "respuesta" });

        await _service.AskAsync(actor, "¿cómo estoy?");

        _mockDocs.Verify(d => d.GetIndexedTextsAsync(5, null), Times.Once);
    }

    // ---------- Supervisor: departamentos supervisados ----------

    [Test]
    public async Task Ask_Supervisor_LlamaGetIndexedTextsConDepartamentosSupervisados()
    {
        var actor = Actor(2, 5, SystemRoles.SupervisorRoleName);
        _mockUsers.Setup(u => u.GetByIdInCompanyAsync(5, 2)).ReturnsAsync(UsuarioActivo(2, 5));
        _mockDepts.Setup(d => d.GetSupervisedAsync(2)).ReturnsAsync(new List<Department>
        {
            new() { Id = 10, CompanyId = 5 },
            new() { Id = 11, CompanyId = 5 },
            new() { Id = 20, CompanyId = 99 } // de otra empresa, debe excluirse
        });
        _mockDocs.Setup(d => d.GetIndexedTextsAsync(5, new[] { 10, 11 })).ReturnsAsync(TextosIndexados());
        _mockAi.Setup(a => a.AskAsync(It.IsAny<AiRequestDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AiAnswerDto { Answer = "respuesta" });

        await _service.AskAsync(actor, "pregunta");

        _mockDocs.Verify(d => d.GetIndexedTextsAsync(5, new[] { 10, 11 }), Times.Once);
    }

    // ---------- Usuario: su propio departamento ----------

    [Test]
    public async Task Ask_Usuario_LlamaGetIndexedTextsConSuDepartamento()
    {
        var actor = Actor(3, 5, SystemRoles.UsuarioRoleName);
        _mockUsers.Setup(u => u.GetByIdInCompanyAsync(5, 3)).ReturnsAsync(UsuarioActivo(3, 5, 7));
        _mockDocs.Setup(d => d.GetIndexedTextsAsync(5, new[] { 7 })).ReturnsAsync(TextosIndexados());
        _mockAi.Setup(a => a.AskAsync(It.IsAny<AiRequestDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AiAnswerDto { Answer = "respuesta" });

        await _service.AskAsync(actor, "pregunta");

        _mockDocs.Verify(d => d.GetIndexedTextsAsync(5, new[] { 7 }), Times.Once);
    }

    // ---------- Usuario sin departamento: array vacío ----------

    [Test]
    public async Task Ask_UsuarioSinDepartamento_LlamaGetIndexedTextsConArrayVacio()
    {
        var actor = Actor(4, 5, SystemRoles.UsuarioRoleName);
        _mockUsers.Setup(u => u.GetByIdInCompanyAsync(5, 4)).ReturnsAsync(UsuarioActivo(4, 5, null));
        _mockDocs.Setup(d => d.GetIndexedTextsAsync(5, Array.Empty<int>())).ReturnsAsync(TextosIndexados());
        _mockAi.Setup(a => a.AskAsync(It.IsAny<AiRequestDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AiAnswerDto { Answer = "respuesta" });

        await _service.AskAsync(actor, "pregunta");

        _mockDocs.Verify(d => d.GetIndexedTextsAsync(5, Array.Empty<int>()), Times.Once);
    }

    // ---------- Construye el request a la IA correctamente ----------

    [Test]
    public async Task Ask_ConstruyeAiRequestConUsuarioEmpresaYPregunta()
    {
        var actor = Actor(1, 5, SystemRoles.AdminEmpresaRoleName);
        var textos = TextosIndexados(
            new IndexedDocumentText(10, "Manual RRHH", "vacaciones: 14 días"),
            new IndexedDocumentText(20, "Política de viajes", "viáticos: 500"));
        _mockUsers.Setup(u => u.GetByIdInCompanyAsync(5, 1)).ReturnsAsync(UsuarioActivo(1, 5));
        _mockDocs.Setup(d => d.GetIndexedTextsAsync(5, null)).ReturnsAsync(textos);
        AiRequestDto? requestCapturado = null;
        _mockAi.Setup(a => a.AskAsync(It.IsAny<AiRequestDto>(), It.IsAny<CancellationToken>()))
            .Callback<AiRequestDto, CancellationToken>((r, _) => requestCapturado = r)
            .ReturnsAsync(new AiAnswerDto { Answer = "respuesta" });

        await _service.AskAsync(actor, "¿cuántos días de vacaciones?");

        Assert.Multiple(() =>
        {
            Assert.That(requestCapturado, Is.Not.Null);
            Assert.That(requestCapturado!.UserId, Is.EqualTo(1));
            Assert.That(requestCapturado.CompanyId, Is.EqualTo(5));
            Assert.That(requestCapturado.Question, Is.EqualTo("¿cuántos días de vacaciones?"));
            Assert.That(requestCapturado.Context, Has.Count.EqualTo(2));
            Assert.That(requestCapturado.Context[0].DocumentId, Is.EqualTo(10));
            Assert.That(requestCapturado.Context[0].Title, Is.EqualTo("Manual RRHH"));
            Assert.That(requestCapturado.Context[0].Content, Is.EqualTo("vacaciones: 14 días"));
            Assert.That(requestCapturado.Context[1].DocumentId, Is.EqualTo(20));
            Assert.That(requestCapturado.Context[1].Title, Is.EqualTo("Política de viajes"));
        });
    }

    // ---------- Guarda el QueryLog ----------

    [Test]
    public async Task Ask_GuardaQueryLogConDatosCorrectos()
    {
        var actor = Actor(1, 5, SystemRoles.AdminEmpresaRoleName);
        _mockUsers.Setup(u => u.GetByIdInCompanyAsync(5, 1)).ReturnsAsync(UsuarioActivo(1, 5));
        _mockDocs.Setup(d => d.GetIndexedTextsAsync(5, null)).ReturnsAsync(TextosIndexados(
            new IndexedDocumentText(10, "Doc A", "texto A"),
            new IndexedDocumentText(20, "Doc B", "texto B")));
        _mockAi.Setup(a => a.AskAsync(It.IsAny<AiRequestDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AiAnswerDto { Answer = "respuesta" });

        await _service.AskAsync(actor, "pregunta de prueba");

        _mockLog.Verify(l => l.AddAsync(It.Is<QueryLog>(q =>
            q.UserId == 1 &&
            q.CompanyId == 5 &&
            q.Question == "pregunta de prueba" &&
            q.DocumentIds == "10,20")),
            Times.Once);
    }

    // ---------- Devuelve respuesta con fuentes ----------

    [Test]
    public async Task Ask_DevuelveRespuestaConFuentes()
    {
        var actor = Actor(1, 5, SystemRoles.AdminEmpresaRoleName);
        _mockUsers.Setup(u => u.GetByIdInCompanyAsync(5, 1)).ReturnsAsync(UsuarioActivo(1, 5));
        _mockDocs.Setup(d => d.GetIndexedTextsAsync(5, null)).ReturnsAsync(TextosIndexados(
            new IndexedDocumentText(10, "Manual RRHH", "texto")));
        _mockAi.Setup(a => a.AskAsync(It.IsAny<AiRequestDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AiAnswerDto { Answer = "respuesta de prueba" });

        var resultado = await _service.AskAsync(actor, "pregunta");

        Assert.Multiple(() =>
        {
            Assert.That(resultado.Answer, Is.EqualTo("respuesta de prueba"));
            Assert.That(resultado.Sources, Has.Count.EqualTo(1));
            Assert.That(resultado.Sources[0].DocumentId, Is.EqualTo(10));
            Assert.That(resultado.Sources[0].Title, Is.EqualTo("Manual RRHH"));
        });
    }

    // ---------- Sin documentos indexados: respuesta con fuentes vacías ----------

    [Test]
    public async Task Ask_SinDocumentosIndexados_DevuelveRespuestaSinFuentes()
    {
        var actor = Actor(1, 5, SystemRoles.AdminEmpresaRoleName);
        _mockUsers.Setup(u => u.GetByIdInCompanyAsync(5, 1)).ReturnsAsync(UsuarioActivo(1, 5));
        _mockDocs.Setup(d => d.GetIndexedTextsAsync(5, null)).ReturnsAsync(new List<IndexedDocumentText>());
        _mockAi.Setup(a => a.AskAsync(It.IsAny<AiRequestDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AiAnswerDto { Answer = "no encontré información" });

        var resultado = await _service.AskAsync(actor, "pregunta");

        Assert.Multiple(() =>
        {
            Assert.That(resultado.Answer, Is.EqualTo("no encontré información"));
            Assert.That(resultado.Sources, Is.Empty);
        });
    }
}
