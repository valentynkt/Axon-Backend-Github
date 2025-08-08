using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace BuildingBlocks.Infrastructure.Persistence.Infrastructure;

/// <summary>
/// Health check interface for persistence layer components
/// Provides database connectivity and performance health monitoring
/// </summary>
public interface IPersistenceHealthCheck<TContext> : IHealthCheck 
    where TContext : DbContext
{
    /// <summary>
    /// Get detailed health information for the persistence context
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Detailed health check result with metrics</returns>
    Task<DetailedHealthCheckResult> GetDetailedHealthAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Detailed health check result with additional persistence metrics
/// </summary>
public class DetailedHealthCheckResult
{
    public HealthStatus Status { get; set; }
    public string Description { get; set; } = string.Empty;
    public System.Exception? Exception { get; set; }
    public Dictionary<string, object> Data { get; set; } = new();
    public long ResponseTimeMs { get; set; }
    public DateTime CheckTime { get; set; }
    public string ContextName { get; set; } = string.Empty;
    public bool CanConnect { get; set; }
    public string? DatabaseProvider { get; set; }
    public int? OpenConnections { get; set; }
    public PerformanceMetrics? PerformanceMetrics { get; set; }
}