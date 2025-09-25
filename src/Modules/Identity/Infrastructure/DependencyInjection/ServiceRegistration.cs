using Axon.Modules.Identity.Application.Common.Models;
using Axon.Modules.Identity.Application.Configuration;
using Axon.Modules.Identity.Application.Contracts.ExternalServices;
using Axon.Modules.Identity.Application.Contracts.Persistence;
using Axon.Modules.Identity.Application.Contracts.Providers;
using Axon.Modules.Identity.Application.Contracts.Services;
using Axon.Modules.Identity.Application.Providers;
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
using Microsoft.Extensions.Hosting;
using Microsoft.AspNetCore.DataProtection;
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
        IConfiguration configuration,
        IHostEnvironment? environment = null)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        
        // Get connection string - falls back to shared database (axon_chat) only in development
        var connectionString = configuration.GetConnectionString("IdentityDb")
            ?? configuration.GetConnectionString("DefaultConnection");

        // Only use hardcoded fallback in Development environment for local development
        if (string.IsNullOrEmpty(connectionString))
        {
            if (environment?.IsDevelopment() == true)
            {
                connectionString = "Host=localhost;Database=axon_chat;Username=postgres;Password=postgres;Include Error Detail=true";
            }
            else
            {
                throw new InvalidOperationException(
                    "Database connection string not found. Please configure 'ConnectionStrings:IdentityDb' or 'ConnectionStrings:DefaultConnection' in appsettings.");
            }
        }
        
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
            });

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
            });

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
            });
            
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
        // WARNING: IDynamicAuthService should ONLY be used by DynamicAuthenticationProvider
        // API endpoints should use IAuthenticationOrchestrator instead to maintain proper architecture
        services.AddScoped<IDynamicAuthService, DynamicAuthService>();

        // Register IDynamicClaimNormalizer as Singleton to avoid lifetime conflicts with HostedService
        // This is safe since the service is stateless and doesn't hold any per-request state
        services.AddSingleton<IDynamicClaimNormalizer, DynamicClaimNormalizer>();

        // Register DynamicAuthService as HostedService for JWKS pre-warming
        // Use a factory pattern to create it with singleton dependencies
        services.AddHostedService<DynamicAuthService>(); // For JWKS pre-warming

        // Configure environment-specific distributed cache
        ConfigureDistributedCache(services, configuration, environment);

        // Configure enhanced Data Protection API for secure tokens with automatic key rotation
        var dataProtectionBuilder = services.AddDataProtection()
            .SetApplicationName("Axon")
            .SetDefaultKeyLifetime(TimeSpan.FromDays(90)); // Automatic key rotation every 90 days

        // Configure key persistence for production environments
        if (environment?.IsProduction() == true)
        {
            // In production, store keys in a persistent location
            var keysPath = configuration["DataProtection:KeysPath"] ?? Path.Combine(Path.GetTempPath(), "Axon", "DataProtection-Keys");
            Directory.CreateDirectory(keysPath);
            dataProtectionBuilder.PersistKeysToFileSystem(new DirectoryInfo(keysPath));
        }

        // Note: Azure Key Vault integration can be added by referencing:
        // - Microsoft.AspNetCore.DataProtection.AzureKeyVault
        // - Microsoft.AspNetCore.DataProtection.AzureStorage
        // And then calling: .ProtectKeysWithAzureKeyVault() and .PersistKeysToAzureBlobStorage()

        // Configure unified authentication options
        services.Configure<AuthenticationOptions>(configuration.GetSection(AuthenticationOptions.SectionName));

        // Register authentication services (Story 7.1 - Clean Orchestrator Architecture)
        // NO FEATURE FLAGS - Clean implementation only
        services.AddScoped<IJwtTokenService, JwtTokenService>();
        services.AddScoped<IChallengeService, ChallengeService>();
        services.AddScoped<ChallengeTokenProvider>();
        services.AddScoped<RefreshTokenProvider>();
        services.AddScoped<IRefreshTokenProvider>(sp => sp.GetRequiredService<RefreshTokenProvider>());

        // Register user profile service (SOLID refactoring - separates profile concerns from auth)
        services.AddScoped<IUserProfileService, UserProfileService>();

        // Register unified token replay protection service
        services.AddSingleton<Microsoft.IdentityModel.Tokens.ITokenReplayCache, TokenReplayCache>();
        services.AddScoped<TokenReplayCache>();

        // Register Authentication Orchestrator - Single entry point for all auth flows
        services.AddScoped<IAuthenticationOrchestrator, AuthenticationOrchestrator>();

        // Register Authentication Providers
        services.AddScoped<Application.Contracts.Providers.IAuthenticationProvider, Application.Providers.WalletAuthenticationProvider>();
        services.AddScoped<Application.Contracts.Providers.IAuthenticationProvider, Application.Providers.DynamicAuthenticationProvider>();

        // Add Microsoft Identity Framework with AxonUserAuth
        services.AddAxonIdentity();

        // Register AxonUserStore directly for injection
        services.AddScoped<Persistence.Stores.AxonUserStore>();

        // Register custom claims principal factory for enhanced claims
        services.AddScoped<AxonClaimsPrincipalFactory>();

        // AxonClaimsPrincipalFactory handles all claims generation at authentication time

        // Configure Azure Key Vault options for JWT signing (Story 5.5)
        // Note: IJwtTokenService handles all JWT operations
        var keyVaultSection = configuration.GetSection(AzureKeyVaultOptions.SectionName);
        if (keyVaultSection.Exists() && !string.IsNullOrEmpty(keyVaultSection["VaultUri"]))
        {
            // Configure Azure Key Vault options for production use
            services.Configure<AzureKeyVaultOptions>(keyVaultSection);
        }

        // Note: Replay protection is handled by ChallengeService
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

    /// <summary>
    /// Configures distributed cache based on environment
    /// </summary>
    private static void ConfigureDistributedCache(
        IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment? environment)
    {
        var redisConnection = configuration.GetConnectionString("Redis");
        var useRedis = !string.IsNullOrEmpty(redisConnection) &&
                      (environment?.IsProduction() == true ||
                       configuration.GetValue<bool>("Cache:UseRedis", false));

        if (useRedis)
        {
            // TODO: Add StackExchange.Redis package reference to enable Redis caching
            // For now, fall back to distributed memory cache
            services.AddDistributedMemoryCache(options =>
            {
                // Configure memory cache size limits from configuration
                var cacheSection = configuration.GetSection("Cache:Memory");
                if (cacheSection.Exists())
                {
                    cacheSection.Bind(options);
                }
            });
        }
        else
        {
            // Use in-memory cache for development
            services.AddDistributedMemoryCache(options =>
            {
                // Configure memory cache size limit if specified
                var sizeLimit = configuration.GetValue<long?>("Cache:Memory:SizeLimit");
                if (sizeLimit.HasValue)
                {
                    options.SizeLimit = sizeLimit.Value;
                }
            });
        }
    }
}