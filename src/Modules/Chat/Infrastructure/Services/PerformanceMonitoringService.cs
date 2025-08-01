using System.Diagnostics;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Axon.Modules.Chat.Infrastructure.Services;

/// <summary>
/// Performance monitoring service for tracking optimization impact
/// Collects metrics for cache hit rates, memory usage, and execution times
/// </summary>
public interface IPerformanceMonitoringService
{
    /// <summary>
    /// Record execution time for a specific operation
    /// </summary>
    void RecordExecutionTime(string operationName, TimeSpan executionTime);
    
    /// <summary>
    /// Record cache performance metrics
    /// </summary>
    void RecordCacheMetrics(string cacheName, long hits, long misses);
    
    /// <summary>
    /// Record memory usage statistics
    /// </summary>
    void RecordMemoryUsage(long bytesUsed, long bytesAllocated);
    
    /// <summary>
    /// Get performance summary report
    /// </summary>
    PerformanceSummary GetPerformanceSummary();
    
    /// <summary>
    /// Get detailed metrics for a specific operation
    /// </summary>
    OperationMetrics? GetOperationMetrics(string operationName);
}

/// <summary>
/// Performance summary containing key metrics
/// </summary>
public sealed record PerformanceSummary(
    DateTime GeneratedAt,
    TimeSpan SystemUptime,
    double AverageExecutionTimeMs,
    double CacheHitRate,
    long TotalMemoryUsageMB,
    int ActiveOperations,
    double PerformanceScore);

/// <summary>
/// Metrics for a specific operation
/// </summary>
public sealed record OperationMetrics(
    string OperationName,
    long ExecutionCount,
    TimeSpan AverageExecutionTime,
    TimeSpan MinExecutionTime,
    TimeSpan MaxExecutionTime,
    DateTime LastExecuted);

/// <summary>
/// Implementation of performance monitoring service
/// </summary>
public sealed class PerformanceMonitoringService : IPerformanceMonitoringService, IHostedService, IDisposable
{
    private readonly ILogger<PerformanceMonitoringService> _logger;
    private readonly Timer _reportingTimer;
    private readonly DateTime _startTime;
    
    // Operation tracking
    private readonly Dictionary<string, List<TimeSpan>> _operationTimes = new();
    private readonly Dictionary<string, (long Hits, long Misses)> _cacheMetrics = new();
    private readonly List<(DateTime Timestamp, long BytesUsed, long BytesAllocated)> _memorySnapshots = new();
    
    private readonly object _metricsLock = new();
    private const int MaxOperationHistorySize = 1000;
    private const int MaxMemorySnapshotsSize = 100;

    public PerformanceMonitoringService(ILogger<PerformanceMonitoringService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _startTime = DateTime.UtcNow;
        
        // Report performance metrics every 5 minutes
        _reportingTimer = new Timer(ReportPerformanceMetrics, null, 
            TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(5));
    }

    /// <inheritdoc />
    public void RecordExecutionTime(string operationName, TimeSpan executionTime)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(operationName);
        
        lock (_metricsLock)
        {
            if (!_operationTimes.TryGetValue(operationName, out var times))
            {
                times = new List<TimeSpan>();
                _operationTimes[operationName] = times;
            }
            
            times.Add(executionTime);
            
            // Keep only recent measurements to prevent memory bloat
            if (times.Count > MaxOperationHistorySize)
            {
                times.RemoveRange(0, times.Count - MaxOperationHistorySize);
            }
        }
        
        // Log slow operations
        if (executionTime.TotalMilliseconds > 1000) // Log operations > 1 second
        {
            _logger.LogWarning(
                "Slow operation detected: {OperationName} took {ExecutionTime}ms",
                operationName,
                executionTime.TotalMilliseconds);
        }
    }

    /// <inheritdoc />
    public void RecordCacheMetrics(string cacheName, long hits, long misses)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cacheName);
        
        lock (_metricsLock)
        {
            _cacheMetrics[cacheName] = (hits, misses);
        }
    }

    /// <inheritdoc />
    public void RecordMemoryUsage(long bytesUsed, long bytesAllocated)
    {
        lock (_metricsLock)
        {
            _memorySnapshots.Add((DateTime.UtcNow, bytesUsed, bytesAllocated));
            
            // Keep only recent snapshots
            if (_memorySnapshots.Count > MaxMemorySnapshotsSize)
            {
                _memorySnapshots.RemoveRange(0, _memorySnapshots.Count - MaxMemorySnapshotsSize);
            }
        }
    }

    /// <inheritdoc />
    public PerformanceSummary GetPerformanceSummary()
    {
        lock (_metricsLock)
        {
            var uptime = DateTime.UtcNow - _startTime;
            
            // Calculate average execution time across all operations
            var allExecutionTimes = _operationTimes.Values.SelectMany(times => times).ToList();
            var avgExecutionTime = allExecutionTimes.Count > 0 
                ? allExecutionTimes.Average(t => t.TotalMilliseconds) 
                : 0.0;
            
            // Calculate overall cache hit rate
            var totalHits = _cacheMetrics.Values.Sum(m => m.Hits);
            var totalMisses = _cacheMetrics.Values.Sum(m => m.Misses);
            var cacheHitRate = totalHits + totalMisses > 0 
                ? (double)totalHits / (totalHits + totalMisses) 
                : 0.0;
            
            // Get current memory usage
            var gcInfo = GC.GetGCMemoryInfo();
            var memoryUsageMB = gcInfo.HeapSizeBytes / (1024 * 1024);
            
            // Calculate performance score (0-100)
            var performanceScore = CalculatePerformanceScore(avgExecutionTime, cacheHitRate, memoryUsageMB);
            
            return new PerformanceSummary(
                GeneratedAt: DateTime.UtcNow,
                SystemUptime: uptime,
                AverageExecutionTimeMs: avgExecutionTime,
                CacheHitRate: cacheHitRate,
                TotalMemoryUsageMB: memoryUsageMB,
                ActiveOperations: _operationTimes.Count,
                PerformanceScore: performanceScore);
        }
    }

    /// <inheritdoc />
    public OperationMetrics? GetOperationMetrics(string operationName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(operationName);
        
        lock (_metricsLock)
        {
            if (!_operationTimes.TryGetValue(operationName, out var times) || times.Count == 0)
            {
                return null;
            }
            
            return new OperationMetrics(
                OperationName: operationName,
                ExecutionCount: times.Count,
                AverageExecutionTime: TimeSpan.FromTicks((long)times.Average(t => t.Ticks)),
                MinExecutionTime: times.Min(),
                MaxExecutionTime: times.Max(),
                LastExecuted: DateTime.UtcNow); // Approximate, could be enhanced with actual timestamps
        }
    }

    private double CalculatePerformanceScore(double avgExecutionTime, double cacheHitRate, long memoryUsageMB)
    {
        // Simple performance scoring algorithm (can be enhanced)
        var executionTimeScore = Math.Max(0, 100 - avgExecutionTime / 10); // Penalize slow operations
        var cacheScore = cacheHitRate * 100; // Reward high cache hit rates
        var memoryScore = Math.Max(0, 100 - memoryUsageMB / 10); // Penalize high memory usage
        
        return (executionTimeScore + cacheScore + memoryScore) / 3;
    }

    private void ReportPerformanceMetrics(object? state)
    {
        try
        {
            var summary = GetPerformanceSummary();
            
            _logger.LogInformation(
                "Performance Report - Score: {Score:F1}/100, Avg Execution Time: {AvgTime:F1}ms, " +
                "Cache Hit Rate: {CacheHitRate:P1}, Memory Usage: {MemoryMB}MB, Active Operations: {Operations}",
                summary.PerformanceScore,
                summary.AverageExecutionTimeMs,
                summary.CacheHitRate,
                summary.TotalMemoryUsageMB,
                summary.ActiveOperations);
            
            // Report on slow operations
            lock (_metricsLock)
            {
                foreach (var (operationName, times) in _operationTimes)
                {
                    if (times.Count > 0)
                    {
                        var avgTime = times.Average(t => t.TotalMilliseconds);
                        if (avgTime > 500) // Report operations averaging > 500ms
                        {
                            _logger.LogWarning(
                                "Slow operation: {OperationName} - Avg: {AvgTime:F1}ms, Count: {Count}",
                                operationName, avgTime, times.Count);
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate performance report");
        }
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Performance monitoring service started");
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _reportingTimer?.Change(Timeout.Infinite, 0);
        
        // Generate final performance report
        var summary = GetPerformanceSummary();
        _logger.LogInformation(
            "Performance monitoring service stopped. Final score: {Score:F1}/100, Uptime: {Uptime}",
            summary.PerformanceScore,
            summary.SystemUptime);
        
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        _reportingTimer?.Dispose();
        
        var summary = GetPerformanceSummary();
        _logger.LogInformation(
            "PerformanceMonitoringService disposed. Final performance score: {Score:F1}/100",
            summary.PerformanceScore);
    }
}