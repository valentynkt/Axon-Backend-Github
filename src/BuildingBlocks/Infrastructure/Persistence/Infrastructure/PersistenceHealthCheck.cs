using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using BuildingBlocks.Persistence.Common.Interfaces;

namespace BuildingBlocks.Persistence.Infrastructure;

/// <summary>
/// Database-agnostic health check implementation for persistence layer
/// Monitors database connectivity, performance, and overall health
/// </summary>
public class PersistenceHealthCheck<TContext> : IPersistenceHealthCheck<TContext>
    where TContext : DbContext, IDbContext
{
    private readonly TContext _context;
    private readonly ILogger<PersistenceHealthCheck<TContext>> _logger;
    private readonly IPerformanceTracker<TContext>? _performanceTracker;
    private readonly PersistenceHealthCheckOptions _options;

    public PersistenceHealthCheck(
        TContext context, 
        ILogger<PersistenceHealthCheck<TContext>> logger,
        IPerformanceTracker<TContext>? performanceTracker = null,
        PersistenceHealthCheckOptions? options = null)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _performanceTracker = performanceTracker;
        _options = options ?? new PersistenceHealthCheckOptions();
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var detailedResult = await GetDetailedHealthAsync(cancellationToken);
            
            return new HealthCheckResult(
                detailedResult.Status,
                detailedResult.Description,
                detailedResult.Exception,
                detailedResult.Data);
        }
#pragma warning disable CA1031 // Do not catch general exception types - Health checks need to catch all exceptions
        catch (System.Exception ex)
        {
            _logger.LogError(ex, "Health check failed for context {ContextName}", typeof(TContext).Name);
            
            return new HealthCheckResult(
                HealthStatus.Unhealthy,
                $"Health check failed for {typeof(TContext).Name}",
                ex);
        }
#pragma warning restore CA1031
    }

    public async Task<DetailedHealthCheckResult> GetDetailedHealthAsync(CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var checkTime = DateTime.UtcNow;
        var contextName = typeof(TContext).Name;
        
        var result = new DetailedHealthCheckResult
        {
            CheckTime = checkTime,
            ContextName = contextName
        };

        try
        {
            _logger.LogDebug("Starting health check for context {ContextName}", contextName);

            // Test basic connectivity
            await TestConnectivityAsync(result, cancellationToken);
            
            // Test performance if available
            if (_performanceTracker != null)
            {
                await TestPerformanceAsync(result, cancellationToken);
            }
            
            // Test database operations
            await TestBasicOperationsAsync(result, cancellationToken);
            
            stopwatch.Stop();
            result.ResponseTimeMs = stopwatch.ElapsedMilliseconds;
            
            // Determine overall health status
            DetermineHealthStatus(result);
            
            _logger.LogInformation(
                "Health check completed for {ContextName} with status {Status} in {ResponseTimeMs}ms",
                contextName,
                result.Status,
                result.ResponseTimeMs);
                
            return result;
        }
        catch (DbUpdateException ex)
        {
            stopwatch.Stop();
            result.ResponseTimeMs = stopwatch.ElapsedMilliseconds;
            result.Status = HealthStatus.Unhealthy;
            result.Description = $"Database update failed for {contextName}";
            result.Exception = ex;
            
            _logger.LogError(ex, "Database health check failed with update exception for {ContextName}", contextName);
            return result;
        }
        catch (InvalidOperationException ex)
        {
            stopwatch.Stop();
            result.ResponseTimeMs = stopwatch.ElapsedMilliseconds;
            result.Status = HealthStatus.Unhealthy; 
            result.Description = $"Invalid operation in database health check for {contextName}";
            result.Exception = ex;
            
            _logger.LogError(ex, "Database health check failed with invalid operation for {ContextName}", contextName);
            return result;
        }
        catch (TimeoutException ex)
        {
            stopwatch.Stop();
            result.ResponseTimeMs = stopwatch.ElapsedMilliseconds;
            result.Status = HealthStatus.Unhealthy;
            result.Description = $"Database health check timed out for {contextName}";
            result.Exception = ex;
            
            _logger.LogError(ex, "Database health check timed out for {ContextName}", contextName);
            return result;
        }
        catch (OperationCanceledException ex) when (cancellationToken.IsCancellationRequested)
        {
            stopwatch.Stop();
            result.ResponseTimeMs = stopwatch.ElapsedMilliseconds;
            result.Status = HealthStatus.Unhealthy;
            result.Description = $"Database health check was cancelled for {contextName}";
            result.Exception = ex;
            
            _logger.LogWarning("Database health check was cancelled for {ContextName}", contextName);
            return result;
        }
#pragma warning disable CA1031 // Do not catch general exception types - Health checks need to catch all exceptions
        catch (System.Exception ex)
        {
            stopwatch.Stop();
            result.ResponseTimeMs = stopwatch.ElapsedMilliseconds;
            result.Status = HealthStatus.Unhealthy;
            result.Description = $"Database health check failed with unexpected error for {contextName}";
            result.Exception = ex;
            
            _logger.LogError(ex, "Database health check failed with unexpected error for {ContextName}", contextName);
            return result;
        }
#pragma warning restore CA1031
    }

    private async Task TestConnectivityAsync(DetailedHealthCheckResult result, CancellationToken cancellationToken)
    {
        try
        {
            // Test database connectivity with a simple query
            await _context.Database.ExecuteSqlRawAsync("SELECT 1", cancellationToken);
            result.CanConnect = true;
            result.DatabaseProvider = _context.Database.ProviderName;
            
            result.Data["connectivity"] = "healthy";
            result.Data["provider"] = result.DatabaseProvider ?? "unknown";
        }
        catch (System.Exception ex)
        {
            result.CanConnect = false;
            result.Data["connectivity"] = "failed";
            result.Data["connectivity_error"] = ex.Message;
            throw;
        }
    }

    private async Task TestPerformanceAsync(DetailedHealthCheckResult result, CancellationToken cancellationToken)
    {
        if (_performanceTracker == null) return;

        try
        {
            var metrics = await _performanceTracker.GetMetricsAsync(cancellationToken);
            result.PerformanceMetrics = metrics;
            
            result.Data["performance_total_operations"] = metrics.TotalOperations;
            result.Data["performance_average_time_ms"] = metrics.AverageExecutionTimeMs;
            result.Data["performance_success_rate"] = metrics.TotalOperations > 0 
                ? (double)metrics.SuccessfulOperations / metrics.TotalOperations * 100 
                : 100.0;
        }
#pragma warning disable CA1031 // Do not catch general exception types - Health checks need to catch all exceptions
        catch (System.Exception ex)
        {
            result.Data["performance_error"] = ex.Message;
            _logger.LogWarning(ex, "Failed to retrieve performance metrics during health check");
        }
#pragma warning restore CA1031
    }

    private async Task TestBasicOperationsAsync(DetailedHealthCheckResult result, CancellationToken cancellationToken)
    {
        try
        {
            // Test if we can perform basic database operations
            var canRead = await _context.Database.CanConnectAsync(cancellationToken);
            result.Data["can_perform_operations"] = canRead;
            
            if (!canRead)
            {
                throw new InvalidOperationException("Cannot perform basic database operations");
            }
        }
        catch (System.Exception ex)
        {
            result.Data["operations_error"] = ex.Message;
            throw;
        }
    }

    private void DetermineHealthStatus(DetailedHealthCheckResult result)
    {
        if (!result.CanConnect)
        {
            result.Status = HealthStatus.Unhealthy;
            result.Description = $"Cannot connect to database for {result.ContextName}";
            return;
        }

        // Check response time thresholds
        if (result.ResponseTimeMs > _options.UnhealthyResponseTimeMs)
        {
            result.Status = HealthStatus.Unhealthy;
            result.Description = $"Database response time ({result.ResponseTimeMs}ms) exceeds unhealthy threshold ({_options.UnhealthyResponseTimeMs}ms) for {result.ContextName}";
            return;
        }

        if (result.ResponseTimeMs > _options.DegradedResponseTimeMs)
        {
            result.Status = HealthStatus.Degraded;
            result.Description = $"Database response time ({result.ResponseTimeMs}ms) exceeds degraded threshold ({_options.DegradedResponseTimeMs}ms) for {result.ContextName}";
            return;
        }

        result.Status = HealthStatus.Healthy;
        result.Description = $"Database is healthy for {result.ContextName} (response time: {result.ResponseTimeMs}ms)";
    }
}

/// <summary>
/// Configuration options for persistence health checks
/// </summary>
public class PersistenceHealthCheckOptions
{
    /// <summary>
    /// Response time threshold in milliseconds for degraded status (default: 1000ms)
    /// </summary>
    public long DegradedResponseTimeMs { get; set; } = 1000;
    
    /// <summary>
    /// Response time threshold in milliseconds for unhealthy status (default: 5000ms)
    /// </summary>
    public long UnhealthyResponseTimeMs { get; set; } = 5000;
    
    /// <summary>
    /// Timeout for health check operations in milliseconds (default: 10000ms)
    /// </summary>
    public int TimeoutMs { get; set; } = 10000;
}