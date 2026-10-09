using KnovaHub.ApplicationLayer.DTOs;

namespace KnovaHub.ApplicationLayer.Contract;

public interface IAuthService
{
    Task<AuthResponseDto> RegisterAsync(RegisterRequestDto dto);
    Task<AuthResponseDto> LoginAsync(LoginRequestDto dto);
}