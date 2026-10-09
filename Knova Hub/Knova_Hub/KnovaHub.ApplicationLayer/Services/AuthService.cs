using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using KnovaHub.ApplicationLayer.Contract;
using KnovaHub.ApplicationLayer.DTOs;
using KnovaHub.DomainLayer.Entities;
using KnovaHub.DomainLayer.Repository;
using KnovaHub.InfrastructureLayer.Exceptions;
using Microsoft.IdentityModel.Tokens;

namespace KnovaHub.ApplicationLayer.Services;

public class AuthService : IAuthService
{
    private readonly IUserRepository _users;
    private readonly JwtSettings _jwt;

    public AuthService(IUserRepository users, JwtSettings jwt)
    {
        _users = users;
        _jwt = jwt;
    }

    public async Task<AuthResponseDto> RegisterAsync(RegisterRequestDto dto)
    {
        // La contraseña no se puede validar en la BD (solo se guarda el hash), por eso se valida aquí.
        if (dto.Password != dto.ConfirmPassword)
            throw new DataValidationException("Las contraseñas no coinciden.");

        if (!IsStrongPassword(dto.Password))
            throw new DataValidationException(
                "La contraseña debe tener mínimo 8 caracteres, al menos un número y un carácter especial.");

        var email = dto.Email.Trim().ToLowerInvariant();

        var company = new Company
        {
            Rnc = dto.Rnc.Trim(),
            Name = dto.CompanyName.Trim(),
            Email = email,
            Phone = dto.Phone.Trim(),
            Address = dto.Address.Trim()
        };

        // El contacto principal queda como Admin de la empresa.
        var admin = new User
        {
            FullName = dto.ContactName.Trim(),
            Email = email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password),
            RoleId = SystemRoles.AdminRoleId
        };

        // El resto de validaciones (RNC y correo únicos, formatos, campos obligatorios) las hace la base de datos.
        var created = await _users.RegisterCompanyAsync(company, admin);
        return BuildResponse(created);
    }

    public async Task<AuthResponseDto> LoginAsync(LoginRequestDto dto)
    {
        var user = await _users.GetByEmailOrRncAsync(dto.Identifier);

        if (user is null
            || !user.IsActive
            || user.Company is null
            || !user.Company.IsActive
            || !BCrypt.Net.BCrypt.Verify(dto.Password, user.PasswordHash))
        {
            throw new InvalidCredentialsException();
        }

        return BuildResponse(user);
    }

    private static bool IsStrongPassword(string password)
    {
        return password.Length >= 8
            && password.Any(char.IsDigit)
            && password.Any(c => !char.IsLetterOrDigit(c) && !char.IsWhiteSpace(c));
    }

    private AuthResponseDto BuildResponse(User user)
    {
        var expiresAt = DateTime.UtcNow.AddMinutes(_jwt.ExpiresMinutes);
        var role = user.Role?.Name ?? SystemRoles.UserRoleName;
        var companyName = user.Company?.Name ?? string.Empty;
        var rnc = user.Company?.Rnc ?? string.Empty;

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Name, user.FullName),
            new Claim(ClaimTypes.Email, user.Email),
            new Claim(ClaimTypes.Role, role),
            new Claim("companyId", user.CompanyId.ToString())
        };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwt.Key));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _jwt.Issuer,
            audience: _jwt.Audience,
            claims: claims,
            expires: expiresAt,
            signingCredentials: credentials);

        return new AuthResponseDto
        {
            Token = new JwtSecurityTokenHandler().WriteToken(token),
            ExpiresAt = expiresAt,
            UserId = user.Id,
            FullName = user.FullName,
            Email = user.Email,
            Role = role,
            CompanyId = user.CompanyId,
            CompanyName = companyName,
            Rnc = rnc
        };
    }
}