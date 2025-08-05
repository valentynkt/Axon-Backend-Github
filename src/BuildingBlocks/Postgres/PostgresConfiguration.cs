using BuildingBlocks.EFCore;
using BuildingBlocks.Web;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;

namespace BuildingBlocks.Postgres;

/// <summary>
/// PostgreSQL configuration patterns for Clean Architecture compliance
/// Provides fluent configuration API for enterprise-grade setup
/// </summary>
public static class PostgresConfiguration
{
    /// <summary>
    /// Configure PostgreSQL with Clean Architecture patterns
    /// </summary>
    public static WebApplicationBuilder AddPostgresWithCleanArchitecture<TContext>(
        this WebApplicationBuilder builder,
        string? connectionName = "DefaultConnection",
        Action<PostgresConfigurationOptions>? configureOptions = null)
        where TContext : DbContext, IDbContext
    {
        var options = new PostgresConfigurationOptions();
        configureOptions?.Invoke(options);

        // Core database configuration
        builder.AddCustomDbContext<TContext>(connectionName);

        // Add repository patterns
        builder.Services.ConfigureRepositoryPatterns(options);

        // Add performance monitoring
        if (options.EnablePerformanceMonitoring)
        {
            builder.Services.AddPerformanceMonitoring<TContext>();
        }

        // Add health checks
        if (options.EnableHealthChecks)
        {
            builder.Services.AddPostgresHealthChecks<TContext>();
        }

        return builder;
    }

    /// <summary>
    /// Configure repository patterns with Clean Architecture compliance
    /// </summary>
    private static IServiceCollection ConfigureRepositoryPatterns(
        this IServiceCollection services,
        PostgresConfigurationOptions options)
    {
        // Add repository compatibility layer
        services.AddRepositoryCompatibilityLayer();

        // Configure transaction behavior
        if (options.DefaultTransactionBehavior != TransactionBehavior.None)
        {
            services.ConfigureTransactionBehavior(options.DefaultTransactionBehavior);
        }

        // Configure caching if enabled
        if (options.EnableRepositoryCaching)
        {
            services.AddRepositoryCaching();
        }

        return services;
    }

    /// <summary>
    /// Add performance monitoring for PostgreSQL operations
    /// </summary>
    private static IServiceCollection AddPerformanceMonitoring<TContext>(this IServiceCollection services)
        where TContext : DbContext, IDbContext
    {
        services.AddScoped<IPerformanceTracker<TContext>, PostgresPerformanceTracker<TContext>>();
        return services;
    }

    /// <summary>
    /// Add comprehensive health checks for PostgreSQL
    /// </summary>
    private static IServiceCollection AddPostgresHealthChecks<TContext>(this IServiceCollection services)
        where TContext : DbContext, IDbContext
    {
        services.AddHealthChecks()
            .AddDbContextCheck<TContext>("postgres-dbcontext")
            .AddCheck<PostgresRepositoryHealthCheck>("postgres-repository")
            .AddCheck<PostgresPerformanceHealthCheck<TContext>>("postgres-performance");

        return services;
    }

    /// <summary>
    /// Configure transaction behavior patterns
    /// </summary>
    private static IServiceCollection ConfigureTransactionBehavior(
        this IServiceCollection services,
        TransactionBehavior behavior)
    {
        switch (behavior)
        {
            case TransactionBehavior.PerRequest:
                services.AddScoped<ITransactionBehaviorHandler, PerRequestTransactionHandler>();
                break;
            case TransactionBehavior.PerOperation:
                services.AddScoped<ITransactionBehaviorHandler, PerOperationTransactionHandler>();
                break;
            case TransactionBehavior.Explicit:
                services.AddScoped<ITransactionBehaviorHandler, ExplicitTransactionHandler>();
                break;
        }

        return services;
    }

    /// <summary>
    /// Add repository-level caching
    /// </summary>
    private static IServiceCollection AddRepositoryCaching(this IServiceCollection services)
    {
        services.AddMemoryCache();
        services.AddScoped(typeof(ICachedRepository<,>), typeof(CachedRepository<,>));
        return services;
    }
}

/// <summary>
/// Configuration options for PostgreSQL setup
/// </summary>
public class PostgresConfigurationOptions
{
    public bool EnablePerformanceMonitoring { get; set; } = true;
    public bool EnableHealthChecks { get; set; } = true;
    public bool EnableRepositoryCaching { get; set; } = false;
    public TransactionBehavior DefaultTransactionBehavior { get; set; } = TransactionBehavior.PerOperation;
    public TimeSpan CommandTimeout { get; set; } = TimeSpan.FromSeconds(30);
    public int MaxRetryCount { get; set; } = 3;
    public bool EnableSensitiveDataLogging { get; set; } = false;
}

/// <summary>
/// Transaction behavior patterns
/// </summary>
public enum TransactionBehavior
{
    None,
    PerRequest,
    PerOperation,
    Explicit
}

/// <summary>
/// Performance tracking interface
/// </summary>
public interface IPerformanceTracker<TContext> where TContext : DbContext
{
    Task<T> TrackAsync<T>(string operationName, Func<Task<T>> operation);
    Task TrackAsync(string operationName, Func<Task> operation);
}

/// <summary>
/// PostgreSQL performance tracker implementation
/// </summary>
public class PostgresPerformanceTracker<TContext> : IPerformanceTracker<TContext>
    where TContext : DbContext
{
    private readonly ILogger<PostgresPerformanceTracker<TContext>> _logger;

    public PostgresPerformanceTracker(ILogger<PostgresPerformanceTracker<TContext>> logger)
    {
        _logger = logger;
    }

    public async Task<T> TrackAsync<T>(string operationName, Func<Task<T>> operation)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            var result = await operation();
            stopwatch.Stop();
            
            _logger.LogInformation(
                "PostgreSQL operation {OperationName} completed in {ElapsedMs}ms",
                operationName,
                stopwatch.ElapsedMilliseconds);
                
            return result;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex,
                "PostgreSQL operation {OperationName} failed after {ElapsedMs}ms",
                operationName,
                stopwatch.ElapsedMilliseconds);
            throw;
        }
    }

    public async Task TrackAsync(string operationName, Func<Task> operation)
    {
        await TrackAsync(operationName, async () =>
        {
            await operation();
            return Task.CompletedTask;
        });
    }
}

/// <summary>
/// Transaction behavior handlers
/// </summary>
public interface ITransactionBehaviorHandler
{
    Task ExecuteAsync(Func<Task> operation);
    Task<T> ExecuteAsync<T>(Func<Task<T>> operation);
}

public class PerRequestTransactionHandler : ITransactionBehaviorHandler
{
    public Task ExecuteAsync(Func<Task> operation) => operation();
    public Task<T> ExecuteAsync<T>(Func<Task<T>> operation) => operation();
}

public class PerOperationTransactionHandler : ITransactionBehaviorHandler
{
    private readonly IUnitOfWork _unitOfWork;

    public PerOperationTransactionHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task ExecuteAsync(Func<Task> operation)
    {
        await _unitOfWork.BeginTransactionAsync();
        try
        {
            await operation();
            await _unitOfWork.CommitAsync();
        }
        catch
        {
            await _unitOfWork.RollbackAsync();
            throw;
        }
    }

    public async Task<T> ExecuteAsync<T>(Func<Task<T>> operation)
    {
        await _unitOfWork.BeginTransactionAsync();
        try
        {
            var result = await operation();
            await _unitOfWork.CommitAsync();
            return result;
        }
        catch
        {
            await _unitOfWork.RollbackAsync();
            throw;
        }
    }
}

public class ExplicitTransactionHandler : ITransactionBehaviorHandler
{
    public Task ExecuteAsync(Func<Task> operation) => operation();
    public Task<T> ExecuteAsync<T>(Func<Task<T>> operation) => operation();
}

/// <summary>
/// PostgreSQL performance health check
/// </summary>
public class PostgresPerformanceHealthCheck<TContext> : IHealthCheck
    where TContext : DbContext, IDbContext
{
    private readonly TContext _context;
    private readonly ILogger<PostgresPerformanceHealthCheck<TContext>> _logger;

    public PostgresPerformanceHealthCheck(TContext context, ILogger<PostgresPerformanceHealthCheck<TContext>> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            
            // Simple performance test
            await _context.Database.ExecuteSqlRawAsync("SELECT 1", cancellationToken);
            
            stopwatch.Stop();
            
            var responseTime = stopwatch.ElapsedMilliseconds;
            
            return responseTime < 1000
                ? HealthCheckResult.Healthy($"PostgreSQL responsive in {responseTime}ms")
                : HealthCheckResult.Degraded($"PostgreSQL slow response: {responseTime}ms");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "PostgreSQL performance health check failed");
            return HealthCheckResult.Unhealthy("PostgreSQL performance check failed", ex);
        }
    }
}