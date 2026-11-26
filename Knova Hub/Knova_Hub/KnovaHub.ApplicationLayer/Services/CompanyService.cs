using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;
using KnovaHub.ApplicationLayer.Contract;
using KnovaHub.ApplicationLayer.DTOs;
using KnovaHub.DomainLayer.Constants;
using KnovaHub.DomainLayer.Entities;
using KnovaHub.DomainLayer.Exceptions;
using KnovaHub.DomainLayer.Repository;

namespace KnovaHub.ApplicationLayer.Services;

public class CompanyService : ICompanyService
{
    // Mismos límites que la configuración de EF (KnovaHubDbContext) y los CHECK de la base de datos.
    private const int NameMax = 150;
    private const int EmailMax = 150;
    private const int AddressMax = 250;
    private const int ActivityMax = 250;

    private static readonly Regex RncRegex = new(@"^(\d{9}|\d{11})$");
    private static readonly Regex EmailRegex = new(@"^[^@\s]+@[^@\s]+\.[^@\s]+$");
    private static readonly Regex PhoneRegex = new(@"^[0-9+() \-]{7,20}$");

    private readonly ICompanyRepository _companies;

    public CompanyService(ICompanyRepository companies)
    {
        _companies = companies;
    }

    public async Task<CompanyDto?> GetAsync(int companyId)
    {
        var company = await _companies.GetByIdAsync(companyId);
        return company is null ? null : ToDto(company);
    }

    public async Task<CompanyDto?> UpdateAsync(int companyId, UpdateCompanyDto dto)
    {
        // Se valida todo antes de tocar la base de datos.
        var name = (dto.Name ?? string.Empty).Trim();
        if (name.Length == 0)
            throw new DataValidationException("name", "El nombre de la empresa es obligatorio.");
        if (name.Length > NameMax)
            throw new DataValidationException("name", $"El nombre no puede superar los {NameMax} caracteres.");

        var rnc = (dto.Rnc ?? string.Empty).Trim();
        if (!RncRegex.IsMatch(rnc))
            throw new DataValidationException("rnc", "El RNC debe contener 9 u 11 dígitos.");

        var email = (dto.Email ?? string.Empty).Trim().ToLowerInvariant();
        if (email.Length == 0)
            throw new DataValidationException("email", "El correo es obligatorio.");
        if (email.Length > EmailMax
            || !EmailRegex.IsMatch(email)
            || !new EmailAddressAttribute().IsValid(email))
            throw new DataValidationException("email", "El correo electrónico debe tener un formato válido.");

        var phone = (dto.Phone ?? string.Empty).Trim();
        if (phone.Length == 0)
            throw new DataValidationException("phone", "El teléfono es obligatorio.");
        if (!PhoneRegex.IsMatch(phone))
            throw new DataValidationException("phone",
                "El teléfono solo puede tener números, espacios, + ( ) y guiones (mínimo 7 caracteres).");

        var address = (dto.Address ?? string.Empty).Trim();
        if (address.Length == 0)
            throw new DataValidationException("address", "La dirección es obligatoria.");
        if (address.Length > AddressMax)
            throw new DataValidationException("address", $"La dirección no puede superar los {AddressMax} caracteres.");

        var sector = CompanySectors.All.FirstOrDefault(s =>
            string.Equals(s, dto.Sector?.Trim(), StringComparison.OrdinalIgnoreCase));
        if (sector is null)
            throw new DataValidationException("sector", "Debe seleccionar un sector válido.");

        var activity = (dto.Activity ?? string.Empty).Trim();
        if (activity.Length == 0)
            throw new DataValidationException("activity", "La actividad de la empresa es obligatoria.");
        if (activity.Length > ActivityMax)
            throw new DataValidationException("activity", $"La actividad no puede superar los {ActivityMax} caracteres.");

        var company = await _companies.GetByIdAsync(companyId);
        if (company is null)
            return null;

        company.Name = name;
        company.Rnc = rnc;
        company.Email = email;
        company.Phone = phone;
        company.Address = address;
        company.Sector = sector;
        company.Activity = activity;

        // El repositorio lanza DuplicateEntityException si el RNC o el correo son de otra empresa.
        await _companies.UpdateAsync(company);
        return ToDto(company);
    }

    private static CompanyDto ToDto(Company company) => new()
    {
        Id = company.Id,
        Rnc = company.Rnc,
        Name = company.Name,
        Email = company.Email,
        Phone = company.Phone,
        Address = company.Address,
        Sector = company.Sector,
        Activity = company.Activity
    };
}
