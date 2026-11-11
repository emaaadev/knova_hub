using KnovaHub.ApplicationLayer.Contract;
using KnovaHub.ApplicationLayer.DTOs;
using KnovaHub.ApplicationLayer.Services;
using KnovaHub.DomainLayer.Entities;
using KnovaHub.DomainLayer.Repository;
using Moq;

namespace KnovaHub.Tests.Services;

public class DocumentSummaryTests
{
    private const int CompanyId = 5;
    private const int UserId = 20;

    [TestCase(SystemRoles.AdminEmpresaRoleName, SystemRoles.AdminEmpresaRoleId)]
    [TestCase(SystemRoles.SupervisorRoleName, SystemRoles.SupervisorRoleId)]
    [TestCase(SystemRoles.UsuarioRoleName, SystemRoles.UsuarioRoleId)]
    public async Task ResumenUsaLaMismaEmpresaYDepartamentosQueElListado(string role, int roleId)
    {
        var documents = new Mock<IDocumentRepository>();
        var departments = new Mock<IDepartmentRepository>();
        var users = new Mock<IUserRepository>();
        var service = new DocumentService(documents.Object, Mock.Of<ICategoryRepository>(),
            Mock.Of<ITagRepository>(), departments.Object, users.Object, Mock.Of<IFileStorage>());
        var actor = new Actor(UserId, CompanyId, role);
        users.Setup(repo => repo.GetByIdInCompanyAsync(CompanyId, UserId))
            .ReturnsAsync(new User { IsActive = true, RoleId = roleId, DepartmentId = 4 });
        departments.Setup(repo => repo.GetSupervisedAsync(UserId)).ReturnsAsync(new List<Department>
        {
            new() { Id = 7, CompanyId = CompanyId },
            new() { Id = 8, CompanyId = CompanyId },
            new() { Id = 99, CompanyId = 6 }
        });

        int[]? expectedIds = role switch
        {
            SystemRoles.AdminEmpresaRoleName => null,
            SystemRoles.SupervisorRoleName => new[] { 7, 8 },
            _ => new[] { 4 }
        };
        var summary = new DocumentSummary(3, 2, 1, Array.Empty<RecentDocument>());
        documents.Setup(repo => repo.GetSummaryAsync(CompanyId, expectedIds)).ReturnsAsync(summary);
        documents.Setup(repo => repo.GetByCompanyAsync(CompanyId, DocumentType.Wiki,
                null, null, 7, null, expectedIds))
            .ReturnsAsync(new List<Document>());

        Assert.That(await service.GetSummaryAsync(actor), Is.SameAs(summary));
        Assert.That(await service.GetAllAsync(actor,
            new DocumentFilterDto { Type = DocumentType.Wiki, DepartmentId = 7 }), Is.Empty);
        documents.VerifyAll();
    }
}
