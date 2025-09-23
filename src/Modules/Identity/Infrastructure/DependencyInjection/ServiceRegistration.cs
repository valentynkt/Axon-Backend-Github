using Axon.Modules.Identity.Application.Common.Models;
using Axon.Modules.Identity.Application.Configuration;
using Axon.Modules.Identity.Application.Contracts.ExternalServices;
using Axon.Modules.Identity.Application.Contracts.Persistence;
using Axon.Modules.Identity.Application.Contracts.Services;
using Axon.Modules.Identity.Application.Services;
using Axon.Modules.Identity.Infrastructure.ExternalServices;
using Axon.Modules.Identity.Infrastructure.ExternalServices.Configuration;
using Axon.Modules.Identity.Infrastructure.Persistence.Context;
using Axon.Modules.Identity.Infrastructure.Persistence.DbContexts;
using Axon.Modules.Identity.Infrastructure.Services.Configuration;
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
        
        // Identity DbContext for Microsoft Identity Framework
        services.AddDbContext<IdentityContext>(options =>
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

        // Register Wallet Ownership Repository for Story 5.3
        services.AddScoped<IWalletOwnershipRepository, WalletOwnershipRepository>();
        
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
        
        // Configure Dynamic JWT validation options (Story 5.5)
        services.Configure<DynamicValidationOptions>(configuration.GetSection(DynamicValidationOptions.SectionName));

        // Register Dynamic auth service with JWKS pre-warming
        services.AddScoped<IDynamicAuthService, DynamicAuthService>();
        services.AddHostedService<DynamicAuthService>(); // For JWKS pre-warming

        services.AddScoped<IDynamicClaimNormalizer, DynamicClaimNormalizer>();

        // Register memory cache required for unified authentication service
        services.AddMemoryCache();

        // Configure unified authentication options
        services.Configure<AuthenticationOptions>(configuration.GetSection(AuthenticationOptions.SectionName));

        // Register focused authentication services
        // NOTE: JwtTokenService uses AxonUserAuth with Microsoft Identity Framework
        services.AddScoped<IJwtTokenService, JwtTokenService>();
        services.AddScoped<IBearerTokenExtractor, BearerTokenExtractor>();

        // Register unified authentication service (orchestrates the focused services)
        // Authentication service - JWT validation handled by middleware
        services.AddScoped<IAuthenticationService, AuthenticationService>();

        // Add Microsoft Identity Framework with AxonUserAuth (Story 5 - Identity Integration)
        services.AddAxonIdentityWithFeatureFlag(configuration);

        // Register AxonUserStore directly for injection
        services.AddScoped<Persistence.Stores.AxonUserStore>();

        // Register modern claims transformation for Dynamic.xyz integration
        services.AddTransient<Microsoft.AspNetCore.Authentication.IClaimsTransformation, DynamicClaimsTransformation>();

        // Configure Azure Key Vault options for JWT signing (Story 5.5)
        // Note: AuthenticationService now handles all JWT operations
        var keyVaultSection = configuration.GetSection(AzureKeyVaultOptions.SectionName);
        if (keyVaultSection.Exists() && !string.IsNullOrEmpty(keyVaultSection["VaultUri"]))
        {
            // Configure Azure Key Vault options for production use
            services.Configure<AzureKeyVaultOptions>(keyVaultSection);
        }

        // Note: Replay protection is now handled directly in AuthenticationService
        // Removed legacy services: IReplayProtectionService, IApiKeyAudienceService, ICanonicalMessageService

        // Register Address Normalization Service for API edge validation
        services.AddScoped<IAddressNormalizationService, AddressNormalizationService>();

        // Register Principal Resolution Service for Story 5.3
        services.AddScoped<IPrincipalResolutionService, PrincipalResolutionService>();

        // Register Wallet Verification Service for Story 5.4 - Transaction guards
        services.AddScoped<IWalletVerificationService, WalletVerificationService>();


        // Register Wallet Signature Verifier as Singleton (stateless service)
        services.AddSingleton<IWalletSignatureVerifier, Ed25519SignatureVerifier>();

        // CRITICAL FIX: Register ICurrentUserService implementation
        // This is required by all command/query handlers in the application
        services.AddHttpContextAccessor(); // Required for HttpContextUserService
        services.AddScoped<ICurrentUserService, HttpContextUserService>();

        // Register BuildingBlocks Infrastructure services (including concurrency handling)
        services.AddInfrastructure<IdentityWriteDbContext>(configuration);

        return services;
    }
}