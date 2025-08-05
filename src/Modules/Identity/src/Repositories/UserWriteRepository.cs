using BuildingBlocks.Persistence.Write;
using Identity.Data;
using Identity.Identity.Models;
using Microsoft.EntityFrameworkCore;

namespace Identity.Repositories;

public class UserWriteRepository : EfWriteRepository<User, Guid>, IUserWriteRepository
{
    public UserWriteRepository(IdentityWriteContext context) : base(context)
    {
    }

    public async Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        return await DbSet.FirstOrDefaultAsync(u => u.Email == email, cancellationToken);
    }

    public async Task<User?> GetByUserNameAsync(string userName, CancellationToken cancellationToken = default)
    {
        return await DbSet.FirstOrDefaultAsync(u => u.UserName == userName, cancellationToken);
    }

    public async Task<bool> ExistsByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        return await DbSet.AnyAsync(u => u.Email == email, cancellationToken);
    }

    public async Task<bool> ExistsByUserNameAsync(string userName, CancellationToken cancellationToken = default)
    {
        return await DbSet.AnyAsync(u => u.UserName == userName, cancellationToken);
    }
}