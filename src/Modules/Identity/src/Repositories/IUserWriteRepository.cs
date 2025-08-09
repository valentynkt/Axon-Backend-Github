using BuildingBlocks.Application.Abstractions.Persistence;
using BuildingBlocks.Infrastructure.Persistence.Common.Interfaces;
using Identity.Identity.Models;

namespace Identity.Repositories;

public interface IUserWriteRepository : IWriteRepository<User, Guid>
{
    Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default);
    Task<User?> GetByUserNameAsync(string userName, CancellationToken cancellationToken = default);
    Task<bool> ExistsByEmailAsync(string email, CancellationToken cancellationToken = default);
    Task<bool> ExistsByUserNameAsync(string userName, CancellationToken cancellationToken = default);
}