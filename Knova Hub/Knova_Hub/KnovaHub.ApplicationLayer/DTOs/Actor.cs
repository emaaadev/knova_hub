namespace KnovaHub.ApplicationLayer.DTOs;

/// <summary>
/// Usuario autenticado que hace la petición (sale de los claims del JWT).
/// </summary>
public record Actor(int UserId, int? CompanyId, string Role);
