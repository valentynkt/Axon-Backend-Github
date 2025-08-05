using BuildingBlocks.Core.Pagination;
using BuildingBlocks.Persistence.Read;
using Identity.Data;
using Identity.Identity.Models;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;

namespace Identity.Repositories;

public class UserReadRepository : PostgresReadRepository<User, Guid>, IUserReadRepository
{
    public UserReadRepository(IdentityReadContext context) : base(context)
    {
    }

    public async Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        return await GetQueryable()
            .FirstOrDefaultAsync(u => u.Email == email, cancellationToken);
    }

    public async Task<User?> GetByUserNameAsync(string userName, CancellationToken cancellationToken = default)
    {
        return await GetQueryable()
            .FirstOrDefaultAsync(u => u.UserName == userName, cancellationToken);
    }

    public async Task<IReadOnlyList<User>> GetUsersByRoleAsync(string roleName, CancellationToken cancellationToken = default)
    {
        return await GetQueryable()
            .Join(Context.Set<UserRole>(),
                user => user.Id,
                userRole => userRole.UserId,
                (user, userRole) => new { User = user, UserRole = userRole })
            .Join(Context.Set<Role>(),
                ur => ur.UserRole.RoleId,
                role => role.Id,
                (ur, role) => new { ur.User, Role = role })
            .Where(ur => ur.Role.Name == roleName)
            .Select(ur => ur.User)
            .ToListAsync(cancellationToken);
    }

    public async Task<int> GetTotalUsersCountAsync(CancellationToken cancellationToken = default)
    {
        return await GetQueryable().CountAsync(cancellationToken);
    }
}