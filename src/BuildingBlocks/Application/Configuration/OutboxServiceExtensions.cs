using BuildingBlocks.Application.Behaviors;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using BuildingBlocks.Application.Outbox;
using BuildingBlocks.Infrastructure.Outbox;
using MediatR;

namespace BuildingBlocks.Application.Configuration;

/// <summary>
/// Extension methods for configuring outbox pattern services.
/// Provides comprehensive DI registration and configuration options.
/// </summary>
public static class OutboxServiceExtensions
{
    /// <summary>
    /// Add complete outbox pattern implementation with transaction management.
    /// This registers all services needed for reliable event processing.
    /// </summary>
    /// <param name="services">Service collection</param>
    /// <param name="configureTransaction">Optional transaction configuration</param>
    /// <param name="configureOutbox">Optional outbox configuration</param>
    /// <returns>Service collection for chaining</returns>
    public static IServiceCollection AddOutboxPattern(
        this IServiceCollection services,
        Action<TransactionOptions>? configureTransaction = null,
        Action<OutboxOptions>? configureOutbox = null)
    {
        // Configure options
        if (configureTransaction != null)
        {
            services.Configure(configureTransaction);
        }
        else
        {
            services.Configure<TransactionOptions>(options => { }); // Ensure options are registered with defaults
        }

        if (configureOutbox != null)
        {
            services.Configure(configureOutbox);
        }
        else
        {
            services.Configure<OutboxOptions>(options => { }); // Ensure options are registered with defaults
        }

        // Register core outbox services
        services.TryAddScoped<IOutboxService, OutboxService>();
        services.TryAddScoped<IOutboxRepository, EfOutboxRepository>();

        // Register background processor
        services.TryAddSingleton<IOutboxProcessor, OutboxProcessor>();
        services.AddHostedService<OutboxProcessor>();

        // Register transaction behavior (if not already registered)
        services.TryAddTransient(typeof(IPipelineBehavior<,>), typeof(TransactionBehavior<,>));

        return services;
    }

    /// <summary>
    /// Add outbox services without background processor (for scenarios where you want custom processing).
    /// </summary>
    /// <param name="services">Service collection</param>
    /// <param name="configureTransaction">Optional transaction configuration</param>
    /// <param name="configureOutbox">Optional outbox configuration</param>
    /// <returns>Service collection for chaining</returns>
    public static IServiceCollection AddOutboxServices(
        this IServiceCollection services,
        Action<TransactionOptions>? configureTransaction = null,
        Action<OutboxOptions>? configureOutbox = null)
    {
        // Configure options
        if (configureTransaction != null)
        {
            services.Configure(configureTransaction);
        }

        if (configureOutbox != null)
        {
            services.Configure(configureOutbox);
        }

        // Register core outbox services only
        services.TryAddScoped<IOutboxService, OutboxService>();
        services.TryAddScoped<IOutboxRepository, EfOutboxRepository>();

        // Register transaction behavior
        services.TryAddTransient(typeof(IPipelineBehavior<,>), typeof(TransactionBehavior<,>));

        return services;
    }

    /// <summary>
    /// Add only the background outbox processor (for distributed scenarios).
    /// </summary>
    /// <param name="services">Service collection</param>
    /// <param name="configureOutbox">Optional outbox configuration</param>
    /// <returns>Service collection for chaining</returns>
    public static IServiceCollection AddOutboxProcessor(
        this IServiceCollection services,
        Action<OutboxOptions>? configureOutbox = null)
    {
        if (configureOutbox != null)
        {
            services.Configure(configureOutbox);
        }

        services.TryAddScoped<IOutboxService, OutboxService>();
        services.TryAddScoped<IOutboxRepository, EfOutboxRepository>();
        services.TryAddSingleton<IOutboxProcessor, OutboxProcessor>();
        services.AddHostedService<OutboxProcessor>();

        return services;
    }

    /// <summary>
    /// Configure transaction behavior with enhanced options.
    /// </summary>
    /// <param name="services">Service collection</param>
    /// <param name="configure">Configuration action</param>
    /// <returns>Service collection for chaining</returns>
    public static IServiceCollection ConfigureTransactionBehavior(
        this IServiceCollection services,
        Action<TransactionOptions> configure)
    {
        services.Configure(configure);
        services.TryAddTransient(typeof(IPipelineBehavior<,>), typeof(TransactionBehavior<,>));
        return services;
    }

    /// <summary>
    /// Configure outbox processing options.
    /// </summary>
    /// <param name="services">Service collection</param>
    /// <param name="configure">Configuration action</param>
    /// <returns>Service collection for chaining</returns>
    public static IServiceCollection ConfigureOutboxProcessing(
        this IServiceCollection services,
        Action<OutboxOptions> configure)
    {
        services.Configure(configure);
        return services;
    }

    /// <summary>
    /// Validate outbox configuration at startup.
    /// </summary>
    /// <param name="services">Service collection</param>
    /// <returns>Service collection for chaining</returns>
    public static IServiceCollection ValidateOutboxConfiguration(this IServiceCollection services)
    {
        services.AddSingleton<IValidateOptions<TransactionOptions>, ValidateTransactionOptions>();
        services.AddSingleton<IValidateOptions<OutboxOptions>, ValidateOutboxOptions>();
        return services;
    }
}

/// <summary>
/// Validates transaction options configuration.
/// </summary>
internal sealed class ValidateTransactionOptions : IValidateOptions<TransactionOptions>
{
    public ValidateOptionsResult Validate(string? name, TransactionOptions options)
    {
        var failures = new List<string>();

        if (options.DefaultTimeout <= TimeSpan.Zero)
        {
            failures.Add("DefaultTimeout must be greater than zero");
        }

        if (options.OutboxProcessingDelay < TimeSpan.Zero)
        {
            failures.Add("OutboxProcessingDelay must be non-negative");
        }

        if (options.DefaultTimeout > TimeSpan.FromHours(1))
        {
            failures.Add("DefaultTimeout should not exceed 1 hour for typical scenarios");
        }

        return failures.Count == 0 
            ? ValidateOptionsResult.Success 
            : ValidateOptionsResult.Fail(failures);
    }
}

/// <summary>
/// Validates outbox options configuration.
/// </summary>
internal sealed class ValidateOutboxOptions : IValidateOptions<OutboxOptions>
{
    public ValidateOptionsResult Validate(string? name, OutboxOptions options)
    {
        var failures = new List<string>();

        if (options.BatchSize <= 0)
        {
            failures.Add("BatchSize must be greater than zero");
        }

        if (options.BatchSize > 10000)
        {
            failures.Add("BatchSize should not exceed 10,000 for performance reasons");
        }

        if (options.MaxRetries < 0)
        {
            failures.Add("MaxRetries must be non-negative");
        }

        if (options.MaxRetries > 10)
        {
            failures.Add("MaxRetries should not exceed 10 to prevent infinite retry loops");
        }

        if (options.MaxConcurrency <= 0)
        {
            failures.Add("MaxConcurrency must be greater than zero");
        }

        if (options.MaxConcurrency > Environment.ProcessorCount * 10)
        {
            failures.Add($"MaxConcurrency should not exceed {Environment.ProcessorCount * 10} (10x CPU cores) for optimal performance");
        }

        if (options.BaseRetryDelayMinutes <= 0)
        {
            failures.Add("BaseRetryDelayMinutes must be greater than zero");
        }

        if (options.ProcessingTimeoutMinutes <= 0)
        {
            failures.Add("ProcessingTimeoutMinutes must be greater than zero");
        }

        if (options.ProcessingInterval <= TimeSpan.Zero)
        {
            failures.Add("ProcessingInterval must be greater than zero");
        }

        if (options.ProcessingInterval > TimeSpan.FromMinutes(30))
        {
            failures.Add("ProcessingInterval should not exceed 30 minutes for timely event processing");
        }

        if (options.CompletedRetentionPeriod <= TimeSpan.Zero)
        {
            failures.Add("CompletedRetentionPeriod must be greater than zero");
        }

        if (options.CleanupBatchSize <= 0)
        {
            failures.Add("CleanupBatchSize must be greater than zero");
        }

        if (options.CleanupInterval <= TimeSpan.Zero)
        {
            failures.Add("CleanupInterval must be greater than zero");
        }

        return failures.Count == 0 
            ? ValidateOptionsResult.Success 
            : ValidateOptionsResult.Fail(failures);
    }
}

/// <summary>
/// Pre-configured outbox settings for common scenarios.
/// </summary>
public static class OutboxPresets
{
    /// <summary>
    /// High-throughput configuration for systems processing many events.
    /// </summary>
    public static Action<OutboxOptions> HighThroughput => options =>
    {
        options.BatchSize = 500;
        options.MaxConcurrency = Environment.ProcessorCount * 4;
        options.ProcessingInterval = TimeSpan.FromSeconds(10);
        options.MaxRetries = 5;
        options.BaseRetryDelayMinutes = 2;
        options.EnableMetrics = true;
    };

    /// <summary>
    /// Low-latency configuration for systems requiring fast event processing.
    /// </summary>
    public static Action<OutboxOptions> LowLatency => options =>
    {
        options.BatchSize = 50;
        options.MaxConcurrency = Environment.ProcessorCount * 2;
        options.ProcessingInterval = TimeSpan.FromSeconds(5);
        options.MaxRetries = 3;
        options.BaseRetryDelayMinutes = 1;
        options.EnableMetrics = true;
    };

    /// <summary>
    /// Conservative configuration for systems with limited resources.
    /// </summary>
    public static Action<OutboxOptions> Conservative => options =>
    {
        options.BatchSize = 25;
        options.MaxConcurrency = Environment.ProcessorCount;
        options.ProcessingInterval = TimeSpan.FromMinutes(1);
        options.MaxRetries = 3;
        options.BaseRetryDelayMinutes = 5;
        options.EnableMetrics = false;
        options.CompletedRetentionPeriod = TimeSpan.FromDays(3);
    };

    /// <summary>
    /// Development configuration with extensive logging and fast cleanup.
    /// </summary>
    public static Action<OutboxOptions> Development => options =>
    {
        options.BatchSize = 10;
        options.MaxConcurrency = 2;
        options.ProcessingInterval = TimeSpan.FromSeconds(15);
        options.MaxRetries = 2;
        options.BaseRetryDelayMinutes = 1;
        options.EnableMetrics = true;
        options.EnableHealthChecks = true;
        options.CompletedRetentionPeriod = TimeSpan.FromHours(1);
        options.CleanupInterval = TimeSpan.FromMinutes(10);
    };
}