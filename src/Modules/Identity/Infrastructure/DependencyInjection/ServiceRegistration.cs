using Axon.Modules.Identity.Application.Common.Models;
using Axon.Modules.Identity.Application.Contracts.ExternalServices;
using Axon.Modules.Identity.Application.Contracts.Persistence;
using Axon.Modules.Identity.Application.Services;
using Axon.Modules.Identity.Infrastructure.ExternalServices;
using Axon.Modules.Identity.Infrastructure.ExternalServices.Configuration;
using Axon.Modules.Identity.Infrastructure.Persistence.DbContexts;
using Axon.Modules.Identity.Infrastructure.Persistence.Repositories;
using Axon.Modules.Identity.Infrastructure.Services;
using BuildingBlocks.Application;
using BuildingBlocks.Core.Abstractions.Authentication;
using BuildingBlocks.Infrastructure.Configuration;
using BuildingBlocks.Infrastructure.Persistence.Write;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Polly;

namespace Axon.Modules.Identity.Infrastructure.DependencyInjection;

/// <summary>
/// Service registration for Identity Infrastructure layer
/// </summary>
public static class ServiceRegistration
{
    /// <summary>
    /// Register Identity Infrastructure services including repositories and DbContexts
    /// </summary>
    /// <param name="services">Service collection</param>
    /// <param name="configuration">Application configuration</param>
    /// <returns>Service collection for chaining</returns>
    public static IServiceCollection AddIdentityInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        
        // Get connection string - falls back to shared database (axon_chat)
        var connectionString = configuration.GetConnectionString("IdentityDb") 
            ?? configuration.GetConnectionString("DefaultConnection")
            ?? "Host=localhost;Database=axon_chat;Username=postgres;Password=postgres;Include Error Detail=true";
        
        // Write DbContext
        services.AddDbContext<IdentityWriteDbContext>(options =>
        {
            options.UseNpgsql(connectionString, npgsqlOptions =>
            {
                npgsqlOptions.MigrationsHistoryTable("__EFMigrationsHistory", "identity");
                npgsqlOptions.EnableRetryOnFailure(
                    maxRetryCount: 5,
                    maxRetryDelay: TimeSpan.FromSeconds(10),
                    errorCodesToAdd: null);
            })
            .UseSnakeCaseNamingConvention();

#if DEBUG
            // Enable detailed logging in development
            options.EnableSensitiveDataLogging(true);
            options.EnableDetailedErrors(true);
#endif
        });
        
        // Read DbContext with read-specific optimizations
        services.AddDbContext<IdentityReadDbContext>(options =>
        {
            options.UseNpgsql(connectionString, npgsqlOptions =>
            {
                npgsqlOptions.MigrationsHistoryTable("__EFMigrationsHistory", "identity");
                npgsqlOptions.CommandTimeout(60); // 60-second timeout for read operations
                npgsqlOptions.EnableRetryOnFailure(
                    maxRetryCount: 5,
                    maxRetryDelay: TimeSpan.FromSeconds(10),
                    errorCodesToAdd: null);
            })
            .UseSnakeCaseNamingConvention();
            
            // Read-specific EF Core optimizations
            options.EnableServiceProviderCaching(true);
            options.EnableSensitiveDataLogging(false); // Security: disable in production
            options.UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking);
            
            // Performance optimizations for read scenarios
            options.ConfigureWarnings(warnings =>
            {
                warnings.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.CoreEventId.DetachedLazyLoadingWarning);
                warnings.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.CoreEventId.FirstWithoutOrderByAndFilterWarning);
            });
        });
        
        // Register Write Repositories
        services.AddScoped<IAxonPrincipalWriteRepository, AxonPrincipalWriteRepository>();
        services.AddScoped<IWalletWriteRepository, WalletWriteRepository>();
        
        // Register Read Repositories
        services.AddScoped<IAxonPrincipalReadRepository, AxonPrincipalReadRepository>();
        services.AddScoped<IWalletReadRepository, WalletReadRepository>();
        
        // Register Repository and DbContext interfaces
        services.AddScoped<IIdentityReadDbContext>(provider => provider.GetRequiredService<IdentityReadDbContext>());
        services.AddScoped<IIdentityWriteDbContext>(provider => provider.GetRequiredService<IdentityWriteDbContext>());
        
        // Register module-specific UnitOfWork using the EfUnitOfWork wrapper with correct module type
        services.AddScoped<IWriteUnitOfWork<IdentityModule>>(provider =>
        {
            var context = provider.GetRequiredService<IdentityWriteDbContext>();
            return new EfUnitOfWork<IdentityWriteDbContext, IdentityModule>(context);
        });
        
        // Register External Services
        services.Configure<DynamicXyzOptions>(configuration.GetSection("DynamicXyz"));
        
        // Register JWKS Service with HTTP client and Polly retry policies
        services.AddHttpClient<IJwksService, JwksService>("JwksClient", client =>
        {
            client.Timeout = TimeSpan.FromSeconds(30);
        })
        .AddStandardResilienceHandler(options =>
        {
            options.Retry.MaxRetryAttempts = 3;
            options.Retry.BackoffType = Polly.DelayBackoffType.Exponential;
            options.Retry.UseJitter = true;
            options.Retry.Delay = TimeSpan.FromMilliseconds(500);
            options.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(10);
            options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(5);
        });
        
        services.AddScoped<IDynamicAuthService, DynamicAuthService>();
        services.AddScoped<IDynamicClaimNormalizer, DynamicClaimNormalizer>();
        
        // Register JWT Replay Guard service
        services.AddMemoryCache(); // Required for replay guard
        services.AddScoped<IJwtReplayGuard, MemoryJwtReplayGuard>();
        
        // Register Exchange Metrics Service
        services.AddScoped<IExchangeMetricsService, ExchangeMetricsService>();
        
        // CRITICAL FIX: Register ICurrentUserService implementation
        // This is required by all command/query handlers in the application
        services.AddHttpContextAccessor(); // Required for HttpContextUserService
        services.AddScoped<ICurrentUserService, HttpContextUserService>();

        // Register BuildingBlocks Infrastructure services (including concurrency handling)
        services.AddInfrastructure<IdentityWriteDbContext>(configuration);

        return services;
    }
}