using KnovaHub.ApplicationLayer.Contract;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;

namespace KnovaHub.Tests.Endpoints;

/// <summary>
/// Levanta la API en memoria con IAuthService simulado, para probar los endpoints
/// sin depender de SQL Server.
/// </summary>
public class KnovaHubApiFactory : WebApplicationFactory<Program>
{
    public Mock<IAuthService> AuthService { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Fuera de "Development" la API no ejecuta las migraciones contra SQL Server al arrancar.
        builder.UseEnvironment("Testing");

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IAuthService>();
            services.AddSingleton(AuthService.Object);
        });
    }
}
