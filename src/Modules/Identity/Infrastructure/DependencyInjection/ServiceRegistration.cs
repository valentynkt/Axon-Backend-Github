using Axon.Modules.Identity.Application.Common.Models;
using Axon.Modules.Identity.Application.Contracts.Persistence;
using Axon.Modules.Identity.Infrastructure.Persistence.DbContexts;
using Axon.Modules.Identity.Infrastructure.Persistence.Repositories;
using Axon.Modules.Identity.Infrastructure.Services;
using BuildingBlocks.Application;
using BuildingBlocks.Core.Abstractions.Authentication;
using BuildingBlocks.Infrastructure.Persistence.Write;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

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
            ?? "Host=localhost;Database=axon_chat;Username=postgres;Password=postgres";
        
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
        
        // Register UnitOfWork using the EfUnitOfWork wrapper with correct module type
        services.AddScoped<IWriteUnitOfWork>(provider => 
        {
            var context = provider.GetRequiredService<IdentityWriteDbContext>();
            return new EfUnitOfWork<IdentityWriteDbContext, IdentityModule>(context);
        });
        
        // Register application services
        // TODO: Add application services here
        
        // CRITICAL FIX: Register ICurrentUserService implementation
        // This is required by all command/query handlers in the application
        // Note: When IdentityApiModule is enabled, this should be moved there
        services.AddHttpContextAccessor(); // Required for HttpContextUserService
        services.AddScoped<ICurrentUserService, HttpContextUserService>();
        
        return services;
    }
}