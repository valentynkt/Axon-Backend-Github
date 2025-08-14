using BuildingBlocks.Application;
using BuildingBlocks.Infrastructure.Persistence.Postgres;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Axon.Modules.Chat.Infrastructure.Persistence;

public static class PersistenceRegistration
{
    public const string ReadDbKey  = "chat.read.db";
    public const string WriteDbKey = "chat.write.db";
    public const string RepoKey    = "chat.repo";

    public static IServiceCollection AddChatPersistence(
        this IServiceCollection services,
        IConfiguration config)
    {
        // Register EF DbContexts (provider/connection via our Postgres helper)
        services.AddPostgresDbContext<ChatWriteDbContext>(config, connectionName: "ChatWrite");
        services.AddPostgresDbContext<ChatReadDbContext>(config,  connectionName: "ChatRead");

        // Expose each DbContext also as a keyed DbContext so adapters can resolve unambiguously
        services.AddKeyedScoped<DbContext, ChatWriteDbContext>(WriteDbKey);
        services.AddKeyedScoped<DbContext, ChatReadDbContext>(ReadDbKey);

        // Register repository adapters (keyed). Handlers can ask for keyed repos, or you can
        // provide small helper factory methods to avoid attributes (see below).
        services.AddKeyedScoped(typeof(IWriteRepository<>), RepoKey, typeof(ChatWriteRepositoryAdapter<>));
        services.AddKeyedScoped(typeof(IWriteRepository<,>), RepoKey, typeof(ChatWriteRepositoryAdapter<,>));
        services.AddKeyedScoped(typeof(IReadRepository<>),  RepoKey, typeof(ChatReadRepositoryAdapter<>));
        services.AddKeyedScoped(typeof(IReadRepository<,>), RepoKey, typeof(ChatReadRepositoryAdapter<,>));

        // Optional: factories so constructors don’t need [FromKeyedServices]
        services.AddScoped<IChatRepositories, ChatRepositories>();

        return services;
    }
}

/// <summary>Convenience factory to get Chat repos without attributes in constructors.</summary>
public interface IChatRepositories
{
    IReadRepository<TRead> Read<TRead>() where TRead : class;
    IWriteRepository<TAgg> Write<TAgg>() where TAgg : class, BuildingBlocks.Core.Domain.Entities.Abstractions.IAggregateRoot<BuildingBlocks.Core.Domain.Primitives.StrongId<Guid>>;
}

internal sealed class ChatRepositories(IServiceProvider sp) : IChatRepositories
{
    public IReadRepository<TRead> Read<TRead>() where TRead : class =>
        sp.GetRequiredKeyedService<IReadRepository<TRead>>(PersistenceRegistration.RepoKey);

    public IWriteRepository<TAgg> Write<TAgg>() where TAgg : class, BuildingBlocks.Core.Domain.Entities.Abstractions.IAggregateRoot<BuildingBlocks.Core.Domain.Primitives.StrongId<Guid>> =>
        sp.GetRequiredKeyedService<IWriteRepository<TAgg>>(PersistenceRegistration.RepoKey);
}
