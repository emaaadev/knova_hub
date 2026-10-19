using KnovaHub.DomainLayer.Entities;

namespace KnovaHub.DomainLayer.Repository;

public interface IUserRepository
{
    Task<User?> GetByEmailOrRncAsync(string identifier);
    Task<User> RegisterCompanyAsync(Company company, User admin);
}