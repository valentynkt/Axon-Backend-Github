namespace Axon.Modules.Identity.Infrastructure.DependencyInjection;

using Axon.Modules.Identity.Application.Configuration;
using Axon.Modules.Identity.Application.Contracts.Persistence;
using Axon.Modules.Identity.Application.Contracts.Providers;
using Axon.Modules.Identity.Application.Contracts.Services;
using Axon.Modules.Identity.Application.Providers;
using Axon.Modules.Identity.Application.Services;
using Axon.Modules.Identity.Domain.Entities;
using Axon.Modules.Identity.Infrastructure.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

/// <summary>
/// Service registration extensions for the new Authentication Orchestrator architecture.
/// Part of Auth Story 7: Service Consolidation
/// </summary>
public static class AuthenticationOrchestratorExtensions
{
    /// <summary>
    /// Registers the authentication orchestrator and providers for clean architecture.
    /// This replaces the fragmented logic across AuthenticationService and DynamicAuthService.
    /// </summary>
    public static IServiceCollection AddAuthenticationOrchestrator(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Register providers as scoped services
        services.AddScoped<IAuthenticationProvider>(serviceProvider =>
        {
            var signatureVerifier = serviceProvider.GetRequiredService<IWalletSignatureVerifier>();
            var principalRepo = serviceProvider.GetRequiredService<IAxonPrincipalWriteRepository>();
            var walletOwnershipRepo = serviceProvider.GetRequiredService<IWalletOwnershipRepository>();
            var walletRepo = serviceProvider.GetRequiredService<IWalletWriteRepository>();
            var userManager = serviceProvider.GetRequiredService<UserManager<AxonUserAuth>>();
            var cache = serviceProvider.GetRequiredService<IMemoryCache>();
            var logger = serviceProvider.GetRequiredService<ILogger<WalletAuthenticationProvider>>();

            // Get HMAC secret from configuration
            var authOptions = serviceProvider.GetRequiredService<IOptions<AuthenticationOptions>>();
            var hmacSecret = authOptions.Value.HmacSecret;

            return new WalletAuthenticationProvider(
                signatureVerifier,
                principalRepo,
                walletOwnershipRepo,
                walletRepo,
                userManager,
                cache,
                logger,
                hmacSecret);
        });

        services.AddScoped<IAuthenticationProvider, DynamicAuthenticationProvider>();

        // Register the orchestrator
        services.AddScoped<IAuthenticationOrchestrator, AuthenticationOrchestrator>();

        // Keep TokenService registration as-is (it's already optimal)
        services.AddScoped<TokenService>();

        return services;
    }

    /// <summary>
    /// Adds authentication orchestrator with feature flag support for gradual rollout.
    /// </summary>
    public static IServiceCollection AddAuthenticationOrchestratorWithFeatureFlag(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var useOrchestrator = configuration.GetValue<bool>("FeatureFlags:UseAuthenticationOrchestrator", false);

        if (useOrchestrator)
        {
            // Use new orchestrator architecture
            services.AddAuthenticationOrchestrator(configuration);

            // Log that we're using the new architecture
            services.AddSingleton<IHostedService>(serviceProvider =>
            {
                var logger = serviceProvider.GetRequiredService<ILogger<AuthenticationOrchestrator>>();
                return new LoggingHostedService(logger, "Authentication Orchestrator architecture is ENABLED");
            });
        }
        else
        {
            // Keep existing services for backward compatibility
            services.AddScoped<IAuthenticationService, AuthenticationService>();

            // Log that we're using the legacy architecture
            services.AddSingleton<IHostedService>(serviceProvider =>
            {
                var logger = serviceProvider.GetRequiredService<ILogger<AuthenticationService>>();
                return new LoggingHostedService(logger, "Legacy authentication architecture is ACTIVE");
            });
        }

        return services;
    }

    /// <summary>
    /// Simple hosted service for logging architecture selection at startup
    /// </summary>
    private sealed class LoggingHostedService : IHostedService
    {
        private readonly ILogger _logger;
        private readonly string _message;

        public LoggingHostedService(ILogger logger, string message)
        {
            _logger = logger;
            _message = message;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("🏗️ {Message}", _message);
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}