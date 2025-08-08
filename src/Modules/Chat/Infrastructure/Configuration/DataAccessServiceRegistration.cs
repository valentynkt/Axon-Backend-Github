using Axon.Modules.Chat.Application.Repositories;
using Axon.Modules.Chat.Application.Services;
using Axon.Modules.Chat.Infrastructure.Persistence;
using Axon.Modules.Chat.Infrastructure.Persistence.Interceptors;
using Axon.Modules.Chat.Infrastructure.Persistence.Repositories;
using Axon.Modules.Chat.Infrastructure.Services;
using Axon.Modules.Chat.Infrastructure.Services.EventSourcing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Axon.BuildingBlocks.Persistence.Interfaces;
using Axon.BuildingBlocks.Postgres;
using Axon.Modules.Chat.Application.Repositories;
using BuildingBlocks.Infrastructure.Persistence.Common.Interfaces;
using Microsoft.Extensions.Logging;

namespace Axon.Modules.Chat.Infrastructure.Configuration;

/// <summary>
/// Simple service registration for Chat Data Access Layer
/// </summary>
public static class DataAccessServiceRegistration
{
    public static IServiceCollection AddChatDataAccess(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Register CQRS persistence with separate read and write contexts
        services.AddChatCqrsPersistence(configuration);

        // Register repositories with CQRS separation
        services.AddChatRepositories();

        // Register Unit of Work pattern for write operations
        services.AddScoped<IWriteUnitOfWork, PostgresWriteUnitOfWork<ChatWriteDbContext>>();

        // Register Event Sourcing components
        services.AddEventSourcingComponents();

        // Register supporting services
        services.AddSupportingServices();

        return services;
    }

    private static IServiceCollection AddChatCqrsPersistence(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("DefaultConnection string is not configured");

        // Register write context with interceptors
        services.AddDbContext<ChatWriteDbContext>(options =>
        {
            options.UseNpgsql(connectionString, npgsqlOptions =>
            {
                npgsqlOptions.EnableRetryOnFailure(
                    maxRetryCount: 3,
                    maxRetryDelay: TimeSpan.FromSeconds(5),
                    errorCodesToAdd: null);
                npgsqlOptions.CommandTimeout(30);
            });
            
#if DEBUG
            options.EnableSensitiveDataLogging(false); // Security: disabled even in debug
            options.EnableDetailedErrors();
            options.LogTo(Console.WriteLine, LogLevel.Information);
#endif
        });

        // Register read context optimized for queries
        services.AddDbContext<ChatReadDbContext>(options =>
        {
            options.UseNpgsql(connectionString, npgsqlOptions =>
            {
                npgsqlOptions.EnableRetryOnFailure(
                    maxRetryCount: 3,
                    maxRetryDelay: TimeSpan.FromSeconds(5),
                    errorCodesToAdd: null);
                npgsqlOptions.CommandTimeout(30);
            });
            
            // Optimize for read operations
            options.UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking);
            
#if DEBUG
            options.EnableSensitiveDataLogging(false);
            options.EnableDetailedErrors();
            options.LogTo(Console.WriteLine, LogLevel.Information);
#endif
        });

        return services;
    }

    private static IServiceCollection AddChatRepositories(this IServiceCollection services)
    {
        // Register write repositories
        services.AddScoped<IConversationWriteRepository, ConversationWriteRepository>();
        services.AddScoped<IMessageWriteRepository, MessageWriteRepository>();
        
        // Register read repositories
        services.AddScoped<IConversationReadRepository, ConversationReadRepository>();
        services.AddScoped<IMessageReadRepository, MessageReadRepository>();
        
        return services;
    }

    private static IServiceCollection AddEventSourcingComponents(this IServiceCollection services)
    {
        services.AddScoped<IEventPublisher, MediatREventPublisher>();
        services.AddScoped<IEventTypeRegistry, InMemoryEventTypeRegistry>();
        services.AddScoped<IEventSerializer, SystemTextJsonEventSerializer>();
        return services;
    }

    private static IServiceCollection AddSupportingServices(this IServiceCollection services)
    {
        // Register interceptors
        services.AddScoped<AuditSaveChangesInterceptor>();
        services.AddScoped<DomainEventInterceptor>();
        
        // Register services
        services.AddScoped<ICurrentUserService, DefaultCurrentUserService>();
        services.AddScoped<IDateTimeProvider, SystemDateTimeProvider>();
        
        return services;
    }
}