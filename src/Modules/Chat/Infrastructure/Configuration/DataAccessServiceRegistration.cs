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
        // Register database context
        services.AddChatDbContext(configuration);

        // Register repositories
        services.AddRepositoryImplementations();

        // Register Unit of Work pattern
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        // Register Event Sourcing components
        services.AddEventSourcingComponents();

        // Register supporting services
        services.AddSupportingServices();

        return services;
    }

    private static IServiceCollection AddChatDbContext(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("DefaultConnection string is not configured");

        services.AddDbContext<ChatDbContext>(options =>
        {
            options.UseNpgsql(connectionString);
#if DEBUG
            options.EnableSensitiveDataLogging();
            options.LogTo(Console.WriteLine, LogLevel.Information);
#endif
        });

        return services;
    }

    private static IServiceCollection AddRepositoryImplementations(this IServiceCollection services)
    {
        services.AddScoped<IConversationRepository, ConversationRepository>();
        services.AddScoped<IMessageRepository, MessageRepository>();
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