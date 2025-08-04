using Axon.Modules.Chat.Application.Services;
using Axon.Modules.Chat.Infrastructure.Persistence.Interceptors;
using Axon.Modules.Chat.Infrastructure.Services;
using Axon.Modules.Chat.Infrastructure.Services.EventSourcing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Axon.Modules.Chat.Infrastructure.Persistence.Design;

/// <summary>
/// Design-time factory for ChatDbContext to support EF Core migrations
/// Provides minimal dependencies required for design-time operations
/// </summary>
public sealed class ChatDbContextDesignTimeFactory : IDesignTimeDbContextFactory<ChatDbContext>
{
    public ChatDbContext CreateDbContext(string[] args)
    {
        // Create minimal service collection for design-time dependencies
        var services = new ServiceCollection();
        
        // Add logging (minimal for design-time)
        services.AddLogging(builder => builder.SetMinimumLevel(LogLevel.Warning));
        
        // Add minimal required services for design-time
        services.AddScoped<ICurrentUserService, DefaultCurrentUserService>();
        services.AddScoped<IDateTimeProvider, SystemDateTimeProvider>();
        services.AddScoped<IEventTypeRegistry, InMemoryEventTypeRegistry>();
        services.AddScoped<IEventSerializer, SystemTextJsonEventSerializer>();
        
        // Build service provider
        var serviceProvider = services.BuildServiceProvider();
        
        // Create interceptors with proper dependencies
        var auditInterceptor = new AuditSaveChangesInterceptor(
            serviceProvider.GetRequiredService<ICurrentUserService>(),
            serviceProvider.GetRequiredService<IDateTimeProvider>(),
            serviceProvider.GetRequiredService<ILogger<AuditSaveChangesInterceptor>>());
        
        var domainEventInterceptor = new DomainEventInterceptor(
            serviceProvider.GetRequiredService<IEventSerializer>(),
            serviceProvider.GetRequiredService<ILogger<DomainEventInterceptor>>());
        
        // Configure DbContext options
        var optionsBuilder = new DbContextOptionsBuilder<ChatDbContext>();
        
        // Use a default connection string for design-time operations
        // This will be overridden at runtime by dependency injection
        optionsBuilder.UseNpgsql("Host=localhost;Database=axon_chat_design;Username=postgres;Password=postgres;Include Error Detail=true",
            options =>
            {
                options.MigrationsHistoryTable("__EFMigrationsHistory", "chat");
                options.EnableRetryOnFailure(maxRetryCount: 3);
            });
            
        // Add interceptors
        optionsBuilder.AddInterceptors(auditInterceptor, domainEventInterceptor);
        
        // Enable sensitive data logging for design-time debugging
        optionsBuilder.EnableSensitiveDataLogging();
        optionsBuilder.EnableDetailedErrors();
        
        return new ChatDbContext(optionsBuilder.Options, auditInterceptor, domainEventInterceptor);
    }
}