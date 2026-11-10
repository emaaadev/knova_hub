using System.Text;
using KnovaHub.API.Authorization;
using KnovaHub.API.Filters;
using KnovaHub.ApplicationLayer.Contract;
using KnovaHub.ApplicationLayer.Services;
using KnovaHub.DomainLayer.Repository;
using KnovaHub.InfrastructureLayer.Data;
using KnovaHub.InfrastructureLayer.Repositories;
using KnovaHub.InfrastructureLayer.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

// Base de datos (SQL Server + Entity Framework)
builder.Services.AddDbContext<KnovaHubDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// JWT
var jwt = builder.Configuration.GetSection("Jwt").Get<JwtSettings>()
    ?? throw new InvalidOperationException("Falta la sección 'Jwt' en appsettings.json");
if (string.IsNullOrWhiteSpace(jwt.Key) || jwt.Key.Length < 32)
    throw new InvalidOperationException(
        "Falta la clave JWT (Jwt:Key) o es muy corta (mínimo 32 caracteres). Configúrala con User Secrets, ver README.");
builder.Services.AddSingleton(jwt);

// Inyección de dependencias

// Repositorios existentes
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IDepartmentRepository, DepartmentRepository>();
builder.Services.AddScoped<IRegistrationRequestRepository, RegistrationRequestRepository>();

// Servicios existentes
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IDepartmentService, DepartmentService>();
builder.Services.AddScoped<IUserRoleService, UserRoleService>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IRegistrationRequestService, RegistrationRequestService>();


// Repositorios de documentos, categorías y etiquetas
builder.Services.AddScoped<IDocumentRepository, DocumentRepository>();
builder.Services.AddScoped<ICategoryRepository, CategoryRepository>();
builder.Services.AddScoped<ITagRepository, TagRepository>();

// Servicios de documentos, categorías y etiquetas
builder.Services.AddScoped<IDocumentService, DocumentService>();
builder.Services.AddScoped<ICategoryService, CategoryService>();
builder.Services.AddScoped<ITagService, TagService>();


// Almacenamiento privado de archivos del módulo de conocimiento.
// Se utiliza una carpeta fuera de wwwroot para impedir el acceso público.
builder.Services.AddSingleton<IFileStorage>(_ =>
{
    var storagePath = builder.Configuration["FileStorage:Path"];

    if (string.IsNullOrWhiteSpace(storagePath))
    {
        storagePath = Path.Combine(
            builder.Environment.ContentRootPath,
            "PrivateStorage",
            "Documents");
    }

    return new LocalFileStorage(storagePath);
});

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwt.Issuer,
            ValidAudience = jwt.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Key)),
            ClockSkew = TimeSpan.Zero
        };
    });
// Políticas por rol y por departamento (ver docs/matriz-de-permisos.md)
builder.Services.AddKnovaAuthorization();

// CORS para el frontend (Live Server)
builder.Services.AddCors(options =>
    options.AddPolicy("Frontend", policy => policy
        .WithOrigins("http://127.0.0.1:5500", "http://localhost:5500")
        .AllowAnyHeader()
        .AllowAnyMethod()));

builder.Services.AddControllers(options => options.Filters.Add<KnovaExceptionFilter>());
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Pega aquí el token que devuelve /api/auth/login"
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
            },
            Array.Empty<string>()
        }
    });
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();

    // Crea/actualiza la base de datos automáticamente en desarrollo.
    using var scope = app.Services.CreateScope();
    scope.ServiceProvider.GetRequiredService<KnovaHubDbContext>().Database.Migrate();
}

app.UseHttpsRedirection();
app.UseCors("Frontend");
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();

// Expone Program para que las pruebas de integración (WebApplicationFactory) puedan levantar la API.
public partial class Program { }
