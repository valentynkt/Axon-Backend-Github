using Axon.Modules.Chat.Application.Repositories;

namespace Axon.Modules.Chat.Infrastructure.Persistence;

/// <summary>
/// Simple Unit of Work implementation
/// </summary>
public sealed class UnitOfWork : IUnitOfWork
{
    private readonly ChatDbContext _context;

    public UnitOfWork(ChatDbContext context)
    {
        _context = context;
    }

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return await _context.SaveChangesAsync(cancellationToken);
    }

    public void Dispose()
    {
        _context.Dispose();
    }
}