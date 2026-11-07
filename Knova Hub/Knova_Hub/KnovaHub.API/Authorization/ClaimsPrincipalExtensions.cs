using System.Security.Claims;
using KnovaHub.ApplicationLayer.DTOs;
using KnovaHub.ApplicationLayer.Services;

namespace KnovaHub.API.Authorization;

public static class ClaimsPrincipalExtensions
{
    /// <summary>Lee del JWT quién hace la petición. Devuelve null si faltan claims.</summary>
    public static Actor? ToActor(this ClaimsPrincipal user)
    {
        if (!int.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
            return null;

        var role = user.FindFirstValue(ClaimTypes.Role);
        if (string.IsNullOrEmpty(role))
            return null;

        int? companyId = int.TryParse(user.FindFirstValue(KnovaClaims.CompanyId), out var c) ? c : null;
        return new Actor(userId, companyId, role);
    }
}
