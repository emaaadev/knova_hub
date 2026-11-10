
using KnovaHub.ApplicationLayer.Contract;
using KnovaHub.ApplicationLayer.DTOs;
using KnovaHub.DomainLayer.Entities;
using KnovaHub.DomainLayer.Exceptions;
using KnovaHub.DomainLayer.Repository;

namespace KnovaHub.ApplicationLayer.Services;

/// <summary>
/// Gestiona las etiquetas del módulo de conocimiento.
/// Aplica aislamiento por empresa y permisos por rol.
/// </summary>
public class TagService : ITagService
{
    private readonly ITagRepository _tags;

    public TagService(ITagRepository tags)
    {
        _tags = tags;
    }

    public async Task<List<TagDto>> GetAllAsync(Actor actor)
    {
        int companyId = GetCompanyId(actor);

        bool includeInactive =
            actor.Role == SystemRoles.AdminEmpresaRoleName;

        var tags = await _tags.GetByCompanyAsync(
            companyId,
            includeInactive);

        return tags.Select(ToDto).ToList();
    }

    public async Task<TagDto> GetByIdAsync(
        Actor actor,
        int tagId)
    {
        int companyId = GetCompanyId(actor);

        var tag = await FindAsync(companyId, tagId);

        if (!tag.IsActive &&
            actor.Role != SystemRoles.AdminEmpresaRoleName)
        {
            throw new NotFoundException(
                "La etiqueta no existe o no está disponible.");
        }

        return ToDto(tag);
    }

    public async Task<TagDto> CreateAsync(
        Actor actor,
        CreateTagDto dto)
    {
        EnsureAdminEmpresa(actor);

        int companyId = GetCompanyId(actor);

        var tag = new Tag
        {
            CompanyId = companyId,
            Name = ValidName(dto.Name),
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        var created = await _tags.AddAsync(tag);

        return ToDto(created);
    }

    public async Task<TagDto> UpdateAsync(
        Actor actor,
        int tagId,
        UpdateTagDto dto)
    {
        EnsureAdminEmpresa(actor);

        int companyId = GetCompanyId(actor);

        var tag = await FindAsync(companyId, tagId);

        tag.Name = ValidName(dto.Name);
        tag.IsActive = dto.IsActive ?? tag.IsActive;

        await _tags.UpdateAsync(tag);

        return ToDto(tag);
    }

    private async Task<Tag> FindAsync(
        int companyId,
        int tagId)
    {
        return await _tags.GetByIdAsync(companyId, tagId)
            ?? throw new NotFoundException(
                "La etiqueta no existe en tu empresa.");
    }

    private static int GetCompanyId(Actor actor)
    {
        if (actor.CompanyId is not int companyId || companyId <= 0)
        {
            throw new DataValidationException(
                "El usuario no tiene una empresa válida asignada.");
        }

        return companyId;
    }

    private static void EnsureAdminEmpresa(Actor actor)
    {
        if (actor.Role != SystemRoles.AdminEmpresaRoleName)
        {
            throw new UnauthorizedAccessException(
                "Solo el administrador de empresa puede gestionar etiquetas.");
        }
    }

    private static string ValidName(string? name)
    {
        var value = name?.Trim() ?? string.Empty;

        if (value.Length == 0)
        {
            throw new DataValidationException(
                "El nombre de la etiqueta es obligatorio.");
        }

        if (value.Length > 100)
        {
            throw new DataValidationException(
                "El nombre de la etiqueta no puede superar los 100 caracteres.");
        }

        return value;
    }

    private static TagDto ToDto(Tag tag)
    {
        return new TagDto
        {
            Id = tag.Id,
            Name = tag.Name,
            IsActive = tag.IsActive,
            CreatedAt = tag.CreatedAt
        };
    }
}
