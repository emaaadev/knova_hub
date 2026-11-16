using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using KnovaHub.ApplicationLayer.DTOs;
using KnovaHub.ApplicationLayer.Services;
using KnovaHub.DomainLayer.Entities;
using KnovaHub.DomainLayer.Repository;
using KnovaHub.Tests.Helpers;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace KnovaHub.Tests.Endpoints;

[TestFixture]
public class DocumentDiscoveryEndpointTests
{
    private const int CompanyId = 5;
    private const int UserId = 20;
    private const int DepartmentId = 7;

    private KnovaHubApiFactory _factory = null!;
    private HttpClient _client = null!;
    private JwtSettings _jwt = null!;
    private Mock<IUserRepository> _users = null!;

    [SetUp]
    public void SetUp()
    {
        _users = new Mock<IUserRepository>();
        _factory = new KnovaHubApiFactory();
        _client = _factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            services.AddSingleton(_users.Object))).CreateClient();
        _jwt = _factory.Services.GetRequiredService<JwtSettings>();
        SignIn(CompanyId);
    }

    [TearDown]
    public void TearDown()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    [Test]
    public async Task Relacionados_OrdenaPorCategoriaEtiquetasYTextoExtraido()
    {
        var source = Document(1, "Manual de vacaciones", 3, new[] { 9 }, markdown: "solicitud aprobación vacaciones");
        var match = Document(2, "Procedimiento de vacaciones", 3, new[] { 9 }, markdown: "solicitud aprobacion vacaciones");
        var textOnly = Document(3, "Flujo administrativo", markdown: "solicitud aprobacion vacaciones");
        var tagOnly = Document(4, "Comunicado", tagIds: new[] { 9 });
        var categoryOnly = Document(5, "Nómina", categoryId: 3);
        var unrelated = Document(6, "Inventario de equipos");
        SetCatalog(CompanyId, false, unrelated, tagOnly, textOnly, match, categoryOnly, source);

        Assert.That(await GetIds("/api/documents/1/related"), Is.EqualTo(new[] { 2, 3, 5, 4 }));
        _factory.DocumentRepository.Verify(repo => repo.RecordReadAsync(It.IsAny<DocumentRead>()), Times.Never);
    }

    [Test]
    public async Task Recomendaciones_UsuarioNuevo_DevuelveLosMasConsultados()
    {
        SetCatalog(CompanyId, true,
            Document(1, "Guía de ingreso", reads: 5),
            Document(2, "Política de vacaciones", reads: 21),
            Document(3, "Catálogo de equipos"));
        SetHistory(CompanyId, Array.Empty<string>(), Array.Empty<int>());

        Assert.That(await GetIds("/api/documents/recommendations"), Is.EqualTo(new[] { 2, 1, 3 }));
    }

    [Test]
    public async Task Recomendaciones_UtilizaPreguntasYAperturasRecientes()
    {
        SetCatalog(CompanyId, true,
            Document(1, "Directorio telefónico", reads: 100),
            Document(2, "Reembolso de combustible"),
            Document(3, "Rotación de guardias", categoryId: 10),
            Document(4, "Horarios de soporte", categoryId: 10, reads: 5));
        SetHistory(CompanyId, new[] { "¿Cómo solicito reembolso de combustible?" }, new[] { 4 });

        Assert.That(await GetIds("/api/documents/recommendations"), Is.EqualTo(new[] { 2, 3, 1, 4 }));
    }

    [Test]
    public async Task Aislamiento_LimitaCatalogoEHistorialYDevuelve404ParaUnaBaseNoVisible()
    {
        var publicDocument = Document(1, "Proceso de vacaciones");
        var departmentDocument = Document(2, "Guía de vacaciones");
        var hiddenDocument = Document(3, "Archivo privado de vacaciones", reads: 200);
        var otherCompanyDocument = Document(4, "Manual de vacaciones", reads: 500);
        SetCatalog(CompanyId, false, publicDocument, departmentDocument);
        SetCatalog(CompanyId, true, publicDocument, departmentDocument);
        SetCatalog(6, false, otherCompanyDocument);
        SetCatalog(6, true, otherCompanyDocument);
        SetHistory(CompanyId, new[] { hiddenDocument.Title }, new[] { hiddenDocument.Id, otherCompanyDocument.Id });
        SetHistory(6, Array.Empty<string>(), Array.Empty<int>());

        Assert.That(await GetIds("/api/documents/1/related"), Is.EqualTo(new[] { 2 }));
        Assert.That(await GetIds("/api/documents/recommendations"), Is.EquivalentTo(new[] { 1, 2 }));
        foreach (var id in new[] { hiddenDocument.Id, otherCompanyDocument.Id, 999 })
        {
            using var response = await _client.GetAsync($"/api/documents/{id}/related");
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        }

        SignIn(6);
        Assert.That(await GetIds("/api/documents/recommendations"), Is.EqualTo(new[] { 4 }));
        using var otherCompanyResponse = await _client.GetAsync("/api/documents/1/related");
        Assert.That(otherCompanyResponse.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));

        _factory.DocumentRepository.Verify(repo => repo.GetRecentlyReadIdsAsync(
            CompanyId, UserId, It.IsAny<DateTime>(), 10, It.IsAny<CancellationToken>()), Times.Once);
        _factory.QueryLogRepository.Verify(repo => repo.GetRecentQuestionsAsync(
            CompanyId, UserId, It.IsAny<DateTime>(), 10, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task AbrirDocumento_RegistraSoloLecturasAutorizadas()
    {
        _factory.DocumentRepository.Setup(repo => repo.GetByIdAsync(CompanyId, 1)).ReturnsAsync(
            new Document { Id = 1, CompanyId = CompanyId, Title = "Guía", Visibility = DocumentVisibility.General });
        _factory.DocumentRepository.Setup(repo => repo.GetByIdAsync(CompanyId, 2)).ReturnsAsync(
            new Document { Id = 2, CompanyId = CompanyId, DepartmentId = 8, Visibility = DocumentVisibility.Departamento });

        using var allowed = await _client.GetAsync("/api/documents/1");
        using var denied = await _client.GetAsync("/api/documents/2");

        Assert.That(allowed.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(denied.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        _factory.DocumentRepository.Verify(repo => repo.RecordReadAsync(It.Is<DocumentRead>(
            read => read.CompanyId == CompanyId && read.UserId == UserId && read.DocumentId == 1)), Times.Once);
        _factory.DocumentRepository.Verify(repo => repo.RecordReadAsync(It.IsAny<DocumentRead>()), Times.Once);
    }

    private void SignIn(int companyId)
    {
        _users.Setup(repo => repo.GetByIdInCompanyAsync(companyId, UserId)).ReturnsAsync(
            TestData.UsuarioDeEmpresa(UserId, SystemRoles.UsuarioRoleId, companyId, DepartmentId));
        var token = TestData.GenerarToken(_jwt, DateTime.UtcNow.AddMinutes(30),
            rol: SystemRoles.UsuarioRoleName, userId: UserId, companyId: companyId);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }

    private void SetCatalog(int companyId, bool includeReadCounts, params DiscoveryDocument[] documents)
    {
        _factory.DocumentRepository.Setup(repo => repo.GetDiscoveryDocumentsAsync(
            companyId, It.Is<int[]?>(ids => ids != null && ids.SequenceEqual(new[] { DepartmentId })),
            includeReadCounts, It.IsAny<CancellationToken>())).ReturnsAsync(documents.ToList());
    }

    private void SetHistory(int companyId, string[] questions, int[] readIds)
    {
        _factory.QueryLogRepository.Setup(repo => repo.GetRecentQuestionsAsync(
            companyId, UserId, It.IsAny<DateTime>(), 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(questions.ToList());
        _factory.DocumentRepository.Setup(repo => repo.GetRecentlyReadIdsAsync(
            companyId, UserId, It.IsAny<DateTime>(), 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(readIds.ToList());
    }

    private async Task<int[]> GetIds(string path)
    {
        using var response = await _client.GetAsync(path);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var suggestions = await response.Content.ReadFromJsonAsync<List<DocumentSuggestionDto>>();
        return suggestions!.Select(item => item.Id).ToArray();
    }

    private static DiscoveryDocument Document(
        int id, string title, int? categoryId = null, int[]? tagIds = null, string? markdown = null, long reads = 0) =>
        new(id, title, null, DocumentType.Documento, categoryId, tagIds ?? Array.Empty<int>(),
            null, markdown, new DateTime(2026, 11, 16), reads);
}
