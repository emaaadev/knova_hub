namespace KnovaHub.ApplicationLayer.DTOs;

/// <summary>Datos de la empresa que se muestran en "Tu empresa".</summary>
public class CompanyDto
{
    public int Id { get; set; }
    public string Rnc { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string Sector { get; set; } = string.Empty;
    public string Activity { get; set; } = string.Empty;
}

/// <summary>
/// Campos editables de la empresa. Son nulables a propósito: la validación y sus mensajes
/// (con el campo que falló) los hace CompanyService, no el binder de MVC.
/// </summary>
public class UpdateCompanyDto
{
    public string? Rnc { get; set; }
    public string? Name { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? Address { get; set; }
    public string? Sector { get; set; }
    public string? Activity { get; set; }
}
