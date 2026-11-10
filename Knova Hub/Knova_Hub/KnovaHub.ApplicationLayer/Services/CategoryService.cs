
using KnovaHub.ApplicationLayer.Contract;
using KnovaHub.ApplicationLayer.DTOs;
using KnovaHub.DomainLayer.Entities;
using KnovaHub.DomainLayer.Exceptions;
using KnovaHub.DomainLayer.Repository;

namespace KnovaHub.ApplicationLayer.Services;

/// <summary>
/// Gestiona las categorías del módulo de conocimiento.
/// Aplica aislamiento por empresa y permisos por rol.
/// </summary>
public class CategoryService : ICategoryService
{
	private readonly ICategoryRepository _categories;

	public CategoryService(ICategoryRepository categories)
	{
		_categories = categories;
	}

	public async Task<List<CategoryDto>> GetAllAsync(Actor actor)
	{
		int companyId = GetCompanyId(actor);

		bool includeInactive =
			actor.Role == SystemRoles.AdminEmpresaRoleName;

		var categories = await _categories.GetByCompanyAsync(
			companyId,
			includeInactive);

		return categories.Select(ToDto).ToList();
	}

	public async Task<CategoryDto> GetByIdAsync(
		Actor actor,
		int categoryId)
	{
		int companyId = GetCompanyId(actor);

		var category = await FindAsync(companyId, categoryId);

		if (!category.IsActive &&
			actor.Role != SystemRoles.AdminEmpresaRoleName)
		{
			throw new NotFoundException(
				"La categoría no existe o no está disponible.");
		}

		return ToDto(category);
	}

	public async Task<CategoryDto> CreateAsync(
		Actor actor,
		CreateCategoryDto dto)
	{
		EnsureAdminEmpresa(actor);

		int companyId = GetCompanyId(actor);

		var category = new Category
		{
			CompanyId = companyId,
			Name = ValidName(dto.Name),
			Description = ValidDescription(dto.Description),
			IsActive = true,
			CreatedAt = DateTime.UtcNow
		};

		var created = await _categories.AddAsync(category);

		return ToDto(created);
	}

	public async Task<CategoryDto> UpdateAsync(
		Actor actor,
		int categoryId,
		UpdateCategoryDto dto)
	{
		EnsureAdminEmpresa(actor);

		int companyId = GetCompanyId(actor);

		var category = await FindAsync(companyId, categoryId);

		category.Name = ValidName(dto.Name);
		category.Description = ValidDescription(dto.Description);
		category.IsActive = dto.IsActive ?? category.IsActive;
		category.UpdatedAt = DateTime.UtcNow;

		await _categories.UpdateAsync(category);

		return ToDto(category);
	}


    public async Task DeactivateAsync(
        Actor actor,
        int categoryId)
    {
        EnsureAdminEmpresa(actor);

        int companyId = GetCompanyId(actor);

        var category = await FindAsync(companyId, categoryId);

        if (!category.IsActive)
        {
            throw new DataValidationException(
                "La categoría ya se encuentra desactivada.");
        }

        category.IsActive = false;
        category.UpdatedAt = DateTime.UtcNow;

        await _categories.UpdateAsync(category);
    }


    private async Task<Category> FindAsync(
		int companyId,
		int categoryId)
	{
		return await _categories.GetByIdAsync(companyId, categoryId)
			?? throw new NotFoundException(
				"La categoría no existe en tu empresa.");
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
				"Solo el administrador de empresa puede gestionar categorías.");
		}
	}

	private static string ValidName(string? name)
	{
		var value = name?.Trim() ?? string.Empty;

		if (value.Length == 0)
		{
			throw new DataValidationException(
				"El nombre de la categoría es obligatorio.");
		}

		if (value.Length > 100)
		{
			throw new DataValidationException(
				"El nombre de la categoría no puede superar los 100 caracteres.");
		}

		return value;
	}

	private static string? ValidDescription(string? description)
	{
		var value = description?.Trim();

		if (string.IsNullOrEmpty(value))
			return null;

		if (value.Length > 500)
		{
			throw new DataValidationException(
				"La descripción no puede superar los 500 caracteres.");
		}

		return value;
	}

	private static CategoryDto ToDto(Category category)
	{
		return new CategoryDto
		{
			Id = category.Id,
			Name = category.Name,
			Description = category.Description,
			IsActive = category.IsActive,
			CreatedAt = category.CreatedAt,
			UpdatedAt = category.UpdatedAt
		};
	}
}
