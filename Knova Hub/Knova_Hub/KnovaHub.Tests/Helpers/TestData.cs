using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using KnovaHub.ApplicationLayer.DTOs;
using KnovaHub.ApplicationLayer.Services;
using KnovaHub.DomainLayer.Entities;
using Microsoft.IdentityModel.Tokens;

namespace KnovaHub.Tests.Helpers;

/// <summary>
/// Datos de prueba reutilizables para los tests de autenticación.
/// </summary>
public static class TestData
{
    public const string PasswordValida = "Segura123!";

    public static JwtSettings JwtSettings() => new()
    {
        Key = "clave-de-pruebas-unitarias-knova-hub-con-mas-de-32-caracteres",
        Issuer = "KnovaHub.Tests",
        Audience = "KnovaHub.Tests.Client",
        ExpiresMinutes = 60
    };

    public static RegisterRequestDto RegistroValido() => new()
    {
        Rnc = "131234567",
        CompanyName = "Empresa de Prueba SRL",
        Email = "contacto@empresa.com",
        Phone = "809-555-1234",
        Address = "Av. Principal #1, Santo Domingo",
        ContactName = "Juan Pérez",
        Password = PasswordValida,
        ConfirmPassword = PasswordValida
    };

    public static User UsuarioAdmin(string passwordHash) => new()
    {
        Id = 10,
        FullName = "Juan Pérez",
        Email = "contacto@empresa.com",
        PasswordHash = passwordHash,
        IsActive = true,
        CompanyId = 5,
        Company = new Company
        {
            Id = 5,
            Rnc = "131234567",
            Name = "Empresa de Prueba SRL",
            Email = "contacto@empresa.com",
            IsActive = true
        },
        RoleId = SystemRoles.AdminRoleId,
        Role = new Role { Id = SystemRoles.AdminRoleId, Name = SystemRoles.AdminRoleName }
    };

    /// <summary>
    /// Genera un JWT con los mismos claims que emite AuthService, para probar endpoints protegidos.
    /// </summary>
    public static string GenerarToken(JwtSettings jwt, DateTime expira, string? claveFirma = null)
    {
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "10"),
            new Claim(ClaimTypes.Name, "Juan Pérez"),
            new Claim(ClaimTypes.Email, "contacto@empresa.com"),
            new Claim(ClaimTypes.Role, SystemRoles.AdminRoleName),
            new Claim("companyId", "5")
        };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(claveFirma ?? jwt.Key));

        var token = new JwtSecurityToken(
            issuer: jwt.Issuer,
            audience: jwt.Audience,
            claims: claims,
            notBefore: expira.AddHours(-1),
            expires: expira,
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
