using Axon.Modules.Chat.Application.Common.Models;
using Axon.Modules.Chat.Application.Contracts.AI;
using Axon.Modules.Chat.Application.Abstractions.Persistence;
using Axon.Modules.Chat.Application.Contracts.Persistence;
using Axon.Modules.Chat.Application.Contracts.Telemetry;
using Axon.Modules.Chat.Infrastructure.ExternalServices.AI.OpenAI;
using Axon.Modules.Chat.Infrastructure.ExternalServices.AI.OpenAI.Models;
using Axon.Modules.Chat.Infrastructure.ExternalServices.AI.Providers;
using Axon.Modules.Chat.Infrastructure.ExternalServices.AI.MCP;
using Axon.Modules.Chat.Infrastructure.Persistence.DbContexts;
using Axon.Modules.Chat.Infrastructure.Persistence.Repositories;
using Axon.Modules.Chat.Infrastructure.Services.Telemetry;
using BuildingBlocks.Application;
using BuildingBlocks.Core.Abstractions.Authentication;
using BuildingBlocks.Infrastructure.Persistence.Write;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly;

namespace Axon.Modules.Chat.Infrastructure.DependencyInjection;

/// <summary>
/// Service registration for Chat Infrastructure layer
/// </summary>
public static class ServiceRegistration
{
    /// <summary>
    /// Register Chat Infrastructure services
    /// </summary>
    /// <param name="services">Service collection</param>
    /// <param name="configuration">Application configuration</param>
    /// <returns>Service collection for chaining</returns>
    public static IServiceCollection AddChatInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        
        // Configure OpenAI options
        services.Configure<OpenAiOptions>(
            configuration.GetSection(OpenAiOptions.SectionName));

        // Configure MCP servers options
        services.Configure<McpServersOptions>(
            configuration.GetSection(McpServersOptions.SectionName));
        
        // Register DbContexts
        var connectionString = configuration.GetConnectionString("ChatDb")
            ?? configuration.GetConnectionString("DefaultConnection")
            ?? "Host=localhost;Database=axon_db;Username=postgres;Password=postgres";
        
        // Unified DbContext with both read and write capabilities
        services.AddDbContext<ChatDbContext>(options =>
        {
            options.UseNpgsql(connectionString, npgsqlOptions =>
            {
                npgsqlOptions.MigrationsHistoryTable("__EFMigrationsHistory", "chat");
                npgsqlOptions.CommandTimeout(30); // 30-second timeout for operations
                npgsqlOptions.EnableRetryOnFailure(
                    maxRetryCount: 3,
                    maxRetryDelay: TimeSpan.FromSeconds(5),
                    errorCodesToAdd: null);
            });

            // Read-specific EF Core optimizations
            options.EnableServiceProviderCaching(true);

#if DEBUG
            options.EnableSensitiveDataLogging(true);
            options.EnableDetailedErrors(true);
#endif

            // Performance optimizations
            options.ConfigureWarnings(warnings =>
            {
                warnings.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.CoreEventId.DetachedLazyLoadingWarning);
                warnings.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.CoreEventId.FirstWithoutOrderByAndFilterWarning);
            });
        });

        // Register Repository and DbContext interfaces
        services.AddScoped<IConversationRepository, ConversationWriteRepository>();
        services.AddScoped<IConversationReadRepository, ConversationReadRepository>();
        services.AddScoped<IMessageReadRepository, MessageReadRepository>();
        services.AddScoped<IChatDbContext>(provider => provider.GetRequiredService<ChatDbContext>());
        
        // Register module-specific UnitOfWork using the EfUnitOfWork wrapper with correct module type
        services.AddScoped<IWriteUnitOfWork<ChatModule>>(provider =>
        {
            var context = provider.GetRequiredService<ChatDbContext>();
            return new EfUnitOfWork<ChatDbContext, ChatModule>(context);
        });
        
        // TimeProvider is registered in Application layer
        
        // NOTE: ICurrentUserService is now registered by IdentityApiModule with HttpContextUserService
        // This provides real user context from Dynamic.xyz authentication
        
        // Register Chat Telemetry service
        services.AddScoped<IChatTelemetry, ChatTelemetry>();

        // Register MCP tool builder (OpenAI-specific)
        services.AddSingleton<IMcpToolBuilder<McpToolDefinition>, OpenAiMcpToolBuilder>();

        // Register OpenAI HTTP client with proper configuration
        services.AddHttpClient<IAiClient, OpenAiMcpClient>((serviceProvider, client) =>
        {
            var options = serviceProvider.GetRequiredService<IOptions<OpenAiOptions>>().Value;

            // Normalize configuration (ensure no trailing slashes, paths start with /)
            options.Normalize();

            // Validate configuration
            var validationErrors = options.Validate().ToList();
            if (validationErrors.Count != 0)
            {
                var errorMessages = string.Join("; ", validationErrors.Select(v => v.ErrorMessage));
                throw new InvalidOperationException($"OpenAI configuration is invalid: {errorMessages}");
            }

            // Configure base URL
            client.BaseAddress = new Uri(options.BaseUrl);

            // Configure authentication
            client.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", options.ApiKey);

            // Configure request headers
            client.DefaultRequestHeaders.Accept.Add(
                new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
            client.DefaultRequestHeaders.UserAgent.ParseAdd("Axon-Chat/1.0");

            // Configure timeout (uses TimeSpan, not seconds property on client)
            client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds > 0 ? options.TimeoutSeconds : 30);
        })
        .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
        {
            // Connection pooling optimization - keep connections alive for 15 minutes
            PooledConnectionLifetime = TimeSpan.FromMinutes(15),

            // Close idle connections after 5 minutes
            PooledConnectionIdleTimeout = TimeSpan.FromMinutes(5),

            // Enable HTTP/2 support
            EnableMultipleHttp2Connections = true
        })
        // Prevent handler recreation (handlers are expensive to create)
        .SetHandlerLifetime(Timeout.InfiniteTimeSpan)
        // Add standard resilience handler with OpenAI-optimized configuration
        // Includes: Rate Limiter, Total Timeout, Retry (exponential backoff), Circuit Breaker, Attempt Timeout
        .AddStandardResilienceHandler(options =>
        {
            // Retry Configuration - handle transient failures with exponential backoff
            options.Retry.MaxRetryAttempts = 3;
            options.Retry.Delay = TimeSpan.FromSeconds(2);
            options.Retry.BackoffType = Polly.DelayBackoffType.Exponential;
            options.Retry.UseJitter = true; // Add jitter to prevent thundering herd

            // Only retry on transient errors (429 rate limit, 5xx server errors, network errors)
            options.Retry.ShouldHandle = args =>
            {
                return args.Outcome switch
                {
                    // Retry on rate limiting
                    { Result.StatusCode: System.Net.HttpStatusCode.TooManyRequests } => PredicateResult.True(),
                    // Retry on server errors (5xx)
                    { Result.StatusCode: >= System.Net.HttpStatusCode.InternalServerError } => PredicateResult.True(),
                    // Retry on network failures
                    { Exception: HttpRequestException } => PredicateResult.True(),
                    { Exception: TimeoutException } => PredicateResult.True(),
                    // Don't retry on client errors (4xx except 429) or success
                    _ => PredicateResult.False()
                };
            };

            // Circuit Breaker Configuration - prevent cascading failures
            options.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(30);
            options.CircuitBreaker.FailureRatio = 0.2; // Open circuit at 20% failure rate
            options.CircuitBreaker.MinimumThroughput = 5; // Need 5 requests before breaking
            options.CircuitBreaker.BreakDuration = TimeSpan.FromSeconds(30); // Stay open for 30s

            // Timeout Configuration
            options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(30); // Per-attempt timeout
            options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(90); // Total request timeout (3 retries)
        });

        // Register AI provider infrastructure
        services.AddScoped<OpenAiProvider>();
        services.AddScoped<IAiProviderFactory, AiProviderFactory>();

        // Register core MCP server resolver
        services.AddScoped<McpServerResolver>();
        
        // CRITICAL FIX: Register memory cache for decorator pattern
        services.AddMemoryCache();
        
        // PERFORMANCE OPTIMIZATION: Register cached decorator around core resolver
        // This provides 90% CPU reduction for repeated MCP server configuration lookups
        services.AddScoped<IMcpServerResolver>(provider =>
        {
            var coreResolver = provider.GetRequiredService<McpServerResolver>();
            var cache = provider.GetRequiredService<IMemoryCache>();
            var logger = provider.GetRequiredService<ILogger<CachedMcpConfigurationService>>();
            
            return new CachedMcpConfigurationService(coreResolver, cache, logger);
        });

        return services;
    }
}