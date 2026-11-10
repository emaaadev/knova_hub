using System.ComponentModel.DataAnnotations;
using KnovaHub.ApplicationLayer.Contract;
using KnovaHub.ApplicationLayer.DTOs;
using KnovaHub.DomainLayer.Entities;
using KnovaHub.DomainLayer.Exceptions;
using KnovaHub.DomainLayer.Repository;

namespace KnovaHub.ApplicationLayer.Services;

public class RegistrationRequestService : IRegistrationRequestService
{
    private readonly IRegistrationRequestRepository _requests;
    private readonly IUserRepository _users;
    private readonly IDepartmentRepository _departments;

    public RegistrationRequestService(
        IRegistrationRequestRepository requests,
        IUserRepository users,
        IDepartmentRepository departments)
    {
        _requests = requests;
        _users = users;
        _departments = departments;
    }

    public async Task<List<RegistrationRequestDto>> GetVisibleAsync(Actor actor)
    {
        if (actor.CompanyId is not int companyId)
            return new List<RegistrationRequestDto>();

        var requests = actor.Role switch
        {
            SystemRoles.AdminEmpresaRoleName => await _requests.GetByCompanyAsync(companyId),
            SystemRoles.SupervisorRoleName => await _requests.GetBySupervisorAsync(actor.UserId),
            _ => new List<UserRegistrationRequest>()
        };

        return requests.Select(ToDto).ToList();
    }

    public async Task<RegistrationRequestDto> CreateAsync(Actor actor, CreateRegistrationRequestDto dto)
    {
        if (actor.CompanyId is not int companyId)
            throw new NotFoundException("La solicitud no existe en tu empresa.");

        if (actor.Role != SystemRoles.SupervisorRoleName)
            throw new BusinessRuleException("Solo un Supervisor puede solicitar el alta de un empleado.");

        var fullName = ValidFullName(dto.FullName);
        var email = ValidEmail(dto.Email);

        // El departamento debe ser de su empresa, estar activo y estar supervisado por él.
        var department = await _departments.GetByIdAsync(companyId, dto.DepartmentId);
        if (department is null || !department.IsActive)
            throw new DataValidationException("departmentId", "El departamento no existe en tu empresa o está inactivo.");

        if (!await _departments.IsSupervisorOfAsync(actor.UserId, dto.DepartmentId))
            throw new BusinessRuleException("Solo puedes solicitar altas en los departamentos que supervisas.");

        if (await _users.EmailExistsAsync(email))
            throw new DuplicateEntityException("email", "Ese correo ya está registrado.");

        if (await _requests.PendingEmailExistsAsync(email))
            throw new DuplicateEntityException("email", "Ya hay una solicitud pendiente con ese correo.");

        var request = new UserRegistrationRequest
        {
            CompanyId = companyId,
            DepartmentId = dto.DepartmentId,
            RequestedById = actor.UserId,
            FullName = fullName,
            Email = email,
            Status = RegistrationRequestStatus.Pending
        };

        var created = await _requests.AddAsync(request);
        created.Department = department;
        return ToDto(created);
    }

    public async Task<ApprovedRequestDto> ApproveAsync(Actor actor, int requestId)
    {
        if (actor.CompanyId is not int companyId)
            throw new NotFoundException("La solicitud no existe en tu empresa.");

        var request = await _requests.GetByIdAsync(companyId, requestId)
            ?? throw new NotFoundException("La solicitud no existe en tu empresa.");

        if (request.Status != RegistrationRequestStatus.Pending)
            throw new BusinessRuleException("Solo se pueden aprobar solicitudes pendientes.");

        // Revalidar antes de crear el usuario: el correo debe seguir libre y el departamento activo.
        if (await _users.EmailExistsAsync(request.Email))
            throw new DuplicateEntityException("email", "Ese correo ya está registrado.");

        var department = await _departments.GetByIdAsync(companyId, request.DepartmentId);
        if (department is null || !department.IsActive)
            throw new BusinessRuleException("El departamento de la solicitud ya no está activo.");

        var temporaryPassword = PasswordPolicy.GenerateTemporary();
        var user = await _requests.ApproveAsync(
            request, BCrypt.Net.BCrypt.HashPassword(temporaryPassword), actor.UserId);

        return new ApprovedRequestDto
        {
            Request = ToDto(request),
            UserId = user.Id,
            TemporaryPassword = temporaryPassword
        };
    }

    public async Task<RegistrationRequestDto> RejectAsync(Actor actor, int requestId)
    {
        if (actor.CompanyId is not int companyId)
            throw new NotFoundException("La solicitud no existe en tu empresa.");

        var request = await _requests.GetByIdAsync(companyId, requestId)
            ?? throw new NotFoundException("La solicitud no existe en tu empresa.");

        if (request.Status != RegistrationRequestStatus.Pending)
            throw new BusinessRuleException("Solo se pueden rechazar solicitudes pendientes.");

        await _requests.RejectAsync(request, actor.UserId);
        return ToDto(request);
    }

    private static string ValidFullName(string? name)
    {
        var value = name?.Trim() ?? string.Empty;
        if (value.Length == 0)
            throw new DataValidationException("fullName", "El nombre es obligatorio.");
        if (value.Length > 100)
            throw new DataValidationException("fullName", "El nombre no puede tener más de 100 caracteres.");
        return value;
    }

    private static string ValidEmail(string? email)
    {
        var value = email?.Trim().ToLowerInvariant() ?? string.Empty;
        if (value.Length == 0 || !new EmailAddressAttribute().IsValid(value))
            throw new DataValidationException("email", "El correo electrónico debe tener un formato válido.");
        return value;
    }

    private static RegistrationRequestDto ToDto(UserRegistrationRequest r) => new()
    {
        Id = r.Id,
        FullName = r.FullName,
        Email = r.Email,
        Status = r.Status.ToString(),
        DepartmentId = r.DepartmentId,
        DepartmentName = r.Department?.Name,
        RequestedByName = r.RequestedBy?.FullName ?? string.Empty,
        CreatedAt = r.CreatedAt,
        ReviewedByName = r.ReviewedBy?.FullName,
        ReviewedAt = r.ReviewedAt
    };
}
