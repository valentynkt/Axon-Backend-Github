using Axon.Modules.Identity.Application.Abstractions.Persistence;
using Axon.Modules.Identity.Application.Common.Models;
using Axon.Modules.Identity.Application.Contracts.Persistence;
using Axon.Modules.Identity.Infrastructure.Persistence.DbContexts;
using Axon.Modules.Identity.Infrastructure.Persistence.Repositories;
using BuildingBlocks.Application;
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
        
        // Get connection string
        var connectionString = configuration.GetConnectionString("IdentityDb") 
            ?? configuration.GetConnectionString("DefaultConnection")
            ?? "Host=localhost;Database=axon_identity;Username=postgres;Password=postgres";
        
        // Write DbContext
        services.AddDbContext<IdentityDbContext>(options =>
        {
            options.UseNpgsql(connectionString, npgsqlOptions =>
            {
                npgsqlOptions.MigrationsHistoryTable("__EFMigrationsHistory", "identity");
            })
            .UseSnakeCaseNamingConvention();
        });
        
        // Read DbContext with read-specific optimizations
        services.AddDbContext<IdentityReadDbContext>(options =>
        {
            options.UseNpgsql(connectionString, npgsqlOptions =>
            {
                npgsqlOptions.MigrationsHistoryTable("__EFMigrationsHistory", "identity");
                npgsqlOptions.CommandTimeout(30); // 30-second timeout for read operations
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
        services.AddScoped<IAxonPrincipalRepository, AxonPrincipalRepository>();
        services.AddScoped<IWalletRepository, WalletRepository>();
        
        // Register Read Repositories
        services.AddScoped<IAxonPrincipalReadRepository, AxonPrincipalReadRepository>();
        services.AddScoped<IWalletReadRepository, WalletReadRepository>();
        
        // Register Repository and DbContext interfaces
        services.AddScoped<IIdentityReadDbContext>(provider => provider.GetRequiredService<IdentityReadDbContext>());
        services.AddScoped<IIdentityWriteDbContext>(provider => provider.GetRequiredService<IdentityDbContext>());
        
        // Register UnitOfWork using the EfUnitOfWork wrapper with correct module type
        services.AddScoped<IWriteUnitOfWork>(provider => 
        {
            var context = provider.GetRequiredService<IdentityDbContext>();
            return new EfUnitOfWork<IdentityDbContext, IdentityModule>(context);
        });
        
        return services;
    }
}