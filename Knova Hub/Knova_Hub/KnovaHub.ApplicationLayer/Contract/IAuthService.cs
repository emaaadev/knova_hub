using KnovaHub.ApplicationLayer.DTOs;

namespace KnovaHub.ApplicationLayer.Contract;

public interface IAuthService
{
    Task<AuthResponseDto> RegisterAsync(RegisterRequestDto dto);
    Task<AuthResponseDto> LoginAsync(LoginRequestDto dto);

    /// <summary>Cambia la propia contraseña y limpia la marca de contraseña temporal.</summary>
    Task ChangePasswordAsync(int userId, ChangePasswordDto dto);
}