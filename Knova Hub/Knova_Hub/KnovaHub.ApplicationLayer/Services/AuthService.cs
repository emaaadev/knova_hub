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

        // El contacto principal queda como AdminEmpresa de la empresa.
        var admin = new User
        {
            FullName = dto.ContactName.Trim(),
            Email = email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password),
            RoleId = SystemRoles.AdminEmpresaRoleId
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
            || !HasValidCompany(user)
            || !BCrypt.Net.BCrypt.Verify(dto.Password, user.PasswordHash))
        {
            throw new InvalidCredentialsException();
        }

        return BuildResponse(user);
    }

    // El AdminPlataforma puede no tener empresa; el resto necesita una empresa activa.
    private static bool HasValidCompany(User user)
    {
        if (user.Company is null)
            return user.CompanyId is null && user.RoleId == SystemRoles.AdminPlataformaRoleId;

        return user.Company.IsActive;
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
        var role = user.Role?.Name ?? SystemRoles.UsuarioRoleName;
        var companyName = user.Company?.Name ?? string.Empty;
        var rnc = user.Company?.Rnc ?? string.Empty;

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.FullName),
            new(ClaimTypes.Email, user.Email),
            new(ClaimTypes.Role, role)
        };

        // El AdminPlataforma sin empresa no lleva el claim companyId.
        if (user.CompanyId is int companyId)
            claims.Add(new Claim(KnovaClaims.CompanyId, companyId.ToString()));

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