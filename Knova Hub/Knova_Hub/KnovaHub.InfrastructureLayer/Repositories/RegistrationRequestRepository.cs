using KnovaHub.DomainLayer.Entities;
using KnovaHub.DomainLayer.Repository;
using KnovaHub.InfrastructureLayer.Data;
using Microsoft.EntityFrameworkCore;

namespace KnovaHub.InfrastructureLayer.Repositories;

public class RegistrationRequestRepository : IRegistrationRequestRepository
{
    private readonly KnovaHubDbContext _context;

    public RegistrationRequestRepository(KnovaHubDbContext context)
    {
        _context = context;
    }

    public Task<List<UserRegistrationRequest>> GetByCompanyAsync(
        int companyId, RegistrationRequestStatus? status = null)
    {
        return BaseQuery()
            .Where(r => r.CompanyId == companyId && (status == null || r.Status == status))
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync();
    }

    public Task<List<UserRegistrationRequest>> GetBySupervisorAsync(
        int supervisorId, RegistrationRequestStatus? status = null)
    {
        return BaseQuery()
            .Where(r => r.RequestedById == supervisorId && (status == null || r.Status == status))
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync();
    }

    public Task<UserRegistrationRequest?> GetByIdAsync(int companyId, int requestId)
    {
        // Tracked: el servicio muta la solicitud y el repositorio guarda el cambio (aprobar/rechazar).
        return _context.UserRegistrationRequests
            .Include(r => r.Department)
            .Include(r => r.RequestedBy)
            .Include(r => r.ReviewedBy)
            .FirstOrDefaultAsync(r => r.Id == requestId && r.CompanyId == companyId);
    }

    public async Task<UserRegistrationRequest> AddAsync(UserRegistrationRequest request)
    {
        _context.UserRegistrationRequests.Add(request);
        await _context.SaveChangesAsync();
        await _context.Entry(request).Reference(r => r.RequestedBy).LoadAsync();
        return request;
    }

    public async Task<User> ApproveAsync(UserRegistrationRequest request, string passwordHash, int adminId)
    {
        request.Status = RegistrationRequestStatus.Approved;
        request.ReviewedById = adminId;
        request.ReviewedAt = DateTime.UtcNow;

        var user = new User
        {
            CompanyId = request.CompanyId,
            FullName = request.FullName,
            Email = request.Email,
            RoleId = SystemRoles.UsuarioRoleId,
            DepartmentId = request.DepartmentId,
            PasswordHash = passwordHash,
            IsActive = true,
            MustChangePassword = true
        };

        // Usuario y solicitud se guardan en la misma transacción.
        _context.Users.Add(user);
        await _context.SaveChangesAsync();
        return user;
    }

    public Task RejectAsync(UserRegistrationRequest request, int adminId)
    {
        request.Status = RegistrationRequestStatus.Rejected;
        request.ReviewedById = adminId;
        request.ReviewedAt = DateTime.UtcNow;
        return _context.SaveChangesAsync();
    }

    public Task<bool> PendingEmailExistsAsync(string email)
    {
        return _context.UserRegistrationRequests
            .AnyAsync(r => r.Email == email && r.Status == RegistrationRequestStatus.Pending);
    }

    private IQueryable<UserRegistrationRequest> BaseQuery()
    {
        return _context.UserRegistrationRequests
            .AsNoTracking()
            .Include(r => r.Department)
            .Include(r => r.RequestedBy)
            .Include(r => r.ReviewedBy);
    }
}
