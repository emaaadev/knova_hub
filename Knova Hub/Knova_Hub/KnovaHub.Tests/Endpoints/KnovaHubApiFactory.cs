using KnovaHub.ApplicationLayer.Contract;
using KnovaHub.DomainLayer.Repository;
using KnovaHub.InfrastructureLayer.Repositories;
using KnovaHub.InfrastructureLayer.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;

namespace KnovaHub.Tests.Endpoints;

/// <summary>
/// Levanta la API en memoria con los servicios simulados, para probar los endpoints
/// y las políticas de autorización sin depender de SQL Server.
/// </summary>
public class KnovaHubApiFactory : WebApplicationFactory<Program>
{
    public Mock<IAuthService> AuthService { get; } = new();
    public Mock<IDepartmentService> DepartmentService { get; } = new();
    public Mock<IUserRoleService> UserRoleService { get; } = new();
    public Mock<IUserService> UserService { get; } = new();
    public Mock<IRegistrationRequestService> RegistrationRequestService { get; } = new();

    /// <summary>Lo usa el handler de la política AccesoDepartamento.</summary>
    public Mock<IDepartmentRepository> DepartmentRepository { get; } = new();

    /// <summary>Simula el servicio de consultas a la IA.</summary>
    public Mock<IAiService> AiService { get; } = new();

    /// <summary>Simula el servicio de consultas de aplicación.</summary>
    public Mock<IQueryService> QueryService { get; } = new();

    /// <summary>Simula el repositorio de logs de consulta.</summary>
    public Mock<IQueryLogRepository> QueryLogRepository { get; } = new();

    /// <summary>Simula el repositorio de documentos.</summary>
    public Mock<IDocumentRepository> DocumentRepository { get; } = new();

    public void ResetMocks()
    {
        AuthService.Reset();
        DepartmentService.Reset();
        UserRoleService.Reset();
        UserService.Reset();
        RegistrationRequestService.Reset();
        DepartmentRepository.Reset();
        AiService.Reset();
        QueryService.Reset();
        QueryLogRepository.Reset();
        DocumentRepository.Reset();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Fuera de "Development" la API no ejecuta las migraciones contra SQL Server al arrancar.
        builder.UseEnvironment("Testing");

        // La clave JWT ya no está en appsettings.json. Esta es solo para pruebas (no es un secreto real).
        builder.UseSetting("Jwt:Key", "clave-solo-para-pruebas-1234567890-abcdef");

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IAuthService>();
            services.AddSingleton(AuthService.Object);

            services.RemoveAll<IDepartmentService>();
            services.AddSingleton(DepartmentService.Object);

            services.RemoveAll<IUserRoleService>();
            services.AddSingleton(UserRoleService.Object);

            services.RemoveAll<IUserService>();
            services.AddSingleton(UserService.Object);

            services.RemoveAll<IRegistrationRequestService>();
            services.AddSingleton(RegistrationRequestService.Object);

            services.RemoveAll<IDepartmentRepository>();
            services.AddSingleton(DepartmentRepository.Object);

            services.RemoveAll<IAiService>();
            services.AddSingleton(AiService.Object);

            services.RemoveAll<IQueryService>();
            services.AddSingleton(QueryService.Object);

            services.RemoveAll<IQueryLogRepository>();
            services.AddSingleton(QueryLogRepository.Object);

            services.RemoveAll<IDocumentRepository>();
            services.AddSingleton(DocumentRepository.Object);
        });
    }
}
