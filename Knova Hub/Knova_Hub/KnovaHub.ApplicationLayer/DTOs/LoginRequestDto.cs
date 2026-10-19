namespace KnovaHub.ApplicationLayer.DTOs;

public class LoginRequestDto
{
    // RNC o email corporativo.
    public string Identifier { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}