using BuildingBlocks.Application;
using BuildingBlocks.Core.Domain.Primitives;
using BuildingBlocks.Infrastructure.Persistence.Read;
using BuildingBlocks.Infrastructure.Persistence.Write;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Axon.Modules.Chat.Infrastructure.Persistence;

/// <summary>
/// Adapters ensure the generic repos use the correct (keyed) DbContext for the Chat module.
/// </summary>
public sealed class ChatReadRepositoryAdapter<TRead> 
    : EfReadRepository<TRead, Guid>, IReadRepository<TRead>
    where TRead : class
{
    public ChatReadRepositoryAdapter([FromKeyedServices(PersistenceRegistration.ReadDbKey)] DbContext ctx)
        : base(ctx) { }
}

public sealed class ChatReadRepositoryAdapter<TRead, TId> 
    : EfReadRepository<TRead, TId>
    where TRead : class
    where TId : notnull
{
    public ChatReadRepositoryAdapter([FromKeyedServices(PersistenceRegistration.ReadDbKey)] DbContext ctx)
        : base(ctx) { }
}

public sealed class ChatWriteRepositoryAdapter<TAgg> 
    : EfWriteRepository<TAgg>
    where TAgg : class, BuildingBlocks.Core.Domain.Entities.Abstractions.IAggregateRoot<StrongId<Guid>>
{
    public ChatWriteRepositoryAdapter([FromKeyedServices(PersistenceRegistration.WriteDbKey)] DbContext ctx)
        : base(ctx) { }
}

public sealed class ChatWriteRepositoryAdapter<TAgg, TId> 
    : EfWriteRepository<TAgg, TId>
    where TAgg : class, BuildingBlocks.Core.Domain.Entities.Abstractions.IAggregateRoot<TId>
    where TId : IStrongId
{
    public ChatWriteRepositoryAdapter([FromKeyedServices(PersistenceRegistration.WriteDbKey)] DbContext ctx)
        : base(ctx) { }
}