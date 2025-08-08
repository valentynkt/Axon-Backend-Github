using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using BuildingBlocks.Core.Abstractions.Events;

namespace BuildingBlocks.Infrastructure.Events;

/// <summary>
/// Service registration extensions for centralized outbox message processing.
/// Created for Epic 06 Story 01 - Centralized Outbox Processor Service.
/// Provides convenient registration methods with comprehensive validation and configuration support.
/// </summary>
public static class OutboxProcessorServiceExtensions
{
    /// <summary>
    /// Add centralized outbox message processing services to the dependency injection container
    /// </summary>
    public static IServiceCollection AddOutboxMessageProcessor(
        this IServiceCollection services,
        IConfiguration configuration,
        Action<OutboxProcessorOptions>? configureOptions = null)
    {
        // Register configuration options
        var optionsBuilder = services.Configure<OutboxProcessorOptions>(
            configuration.GetSection(OutboxProcessorOptions.SectionName));

        if (configureOptions != null)
        {
            services.Configure<OutboxProcessorOptions>(configureOptions);
        }

        // Validate options on startup
        services.AddSingleton<IValidateOptions<OutboxProcessorOptions>, OutboxProcessorOptionsValidator>();

        // Register the processor service
        services.AddSingleton<OutboxProcessorService>();
        services.AddSingleton<IOutboxMessageProcessor>(provider => provider.GetRequiredService<OutboxProcessorService>());
        services.AddSingleton<IHostedService>(provider => provider.GetRequiredService<OutboxProcessorService>());

        // Register health check if enabled
        services.AddTransient<OutboxProcessorHealthCheck>();

        return services;
    }

    /// <summary>
    /// Add centralized outbox message processing services with custom options
    /// </summary>
    public static IServiceCollection AddOutboxMessageProcessor(
        this IServiceCollection services,
        Action<OutboxProcessorOptions> configureOptions)
    {
        services.Configure(configureOptions);
        services.AddSingleton<IValidateOptions<OutboxProcessorOptions>, OutboxProcessorOptionsValidator>();
        
        services.AddSingleton<OutboxProcessorService>();
        services.AddSingleton<IOutboxMessageProcessor>(provider => provider.GetRequiredService<OutboxProcessorService>());
        services.AddSingleton<IHostedService>(provider => provider.GetRequiredService<OutboxProcessorService>());
        
        services.AddTransient<OutboxProcessorHealthCheck>();

        return services;
    }

    /// <summary>
    /// Add outbox message processor health checks to the service collection
    /// </summary>
    public static IServiceCollection AddOutboxProcessorHealthChecks(
        this IServiceCollection services,
        string healthCheckName = "outbox_processor",
        string[]? tags = null)
    {
        return services.AddOutboxProcessorHealthCheck(
            name: healthCheckName,
            tags: tags ?? new[] { "outbox", "messaging", "background_service" },
            timeout: TimeSpan.FromSeconds(10));
    }
}

/// <summary>
/// Options validator for OutboxProcessorOptions
/// </summary>
internal sealed class OutboxProcessorOptionsValidator : IValidateOptions<OutboxProcessorOptions>
{
    public ValidateOptionsResult Validate(string? name, OutboxProcessorOptions options)
    {
        var validationErrors = options.Validate().ToList();

        if (validationErrors.Any())
        {
            return ValidateOptionsResult.Fail(validationErrors);
        }

        return ValidateOptionsResult.Success;
    }
}

/// <summary>
/// Background service wrapper for easier testing and dependency management
/// </summary>
public sealed class OutboxProcessorBackgroundService : BackgroundService
{
    private readonly IOutboxMessageProcessor _processor;
    private readonly ILogger<OutboxProcessorBackgroundService> _logger;

    public OutboxProcessorBackgroundService(
        IOutboxMessageProcessor processor,
        ILogger<OutboxProcessorBackgroundService> logger)
    {
        _processor = processor ?? throw new ArgumentNullException(nameof(processor));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Outbox processor background service starting");

        try
        {
            await _processor.StartAsync(stoppingToken);
            
            // Keep the service running until cancellation is requested
            while (!stoppingToken.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
            // Expected when cancellation is requested
            _logger.LogInformation("Outbox processor background service stopping due to cancellation");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Outbox processor background service failed");
            throw;
        }
        finally
        {
            await _processor.StopAsync(CancellationToken.None);
            _logger.LogInformation("Outbox processor background service stopped");
        }
    }

    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting outbox processor background service");
        await base.StartAsync(cancellationToken);
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Stopping outbox processor background service");
        await base.StopAsync(cancellationToken);
    }
}