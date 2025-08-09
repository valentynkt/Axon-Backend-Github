using BuildingBlocks.Application.Abstractions.Persistence;
using BuildingBlocks.Infrastructure.Persistence.Common.Interfaces;
using Identity.Identity.Models;

namespace Identity.Repositories;

public interface IUserReadRepository : IReadRepository<User, Guid>
{
    Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default);
    Task<User?> GetByUserNameAsync(string userName, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<User>> GetUsersByRoleAsync(string roleName, CancellationToken cancellationToken = default);
    Task<int> GetTotalUsersCountAsync(CancellationToken cancellationToken = default);
}