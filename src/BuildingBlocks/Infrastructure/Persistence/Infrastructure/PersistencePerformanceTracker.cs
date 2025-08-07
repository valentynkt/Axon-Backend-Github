using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BuildingBlocks.Persistence.Infrastructure;

/// <summary>
/// Database-agnostic performance tracker for persistence operations
/// Tracks execution time, success/failure rates, and provides detailed metrics
/// </summary>
public sealed class PersistencePerformanceTracker<TContext> : IPerformanceTracker<TContext>
    where TContext : DbContext
{
    private readonly ILogger<PersistencePerformanceTracker<TContext>> _logger;
    private readonly ConcurrentDictionary<string, List<OperationResult>> _operationHistory;
    private readonly object _lockObject = new();

    public PersistencePerformanceTracker(ILogger<PersistencePerformanceTracker<TContext>> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _operationHistory = new ConcurrentDictionary<string, List<OperationResult>>();
    }

    public async Task<T> TrackAsync<T>(string operationName, Func<Task<T>> operation, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentException.ThrowIfNullOrWhiteSpace(operationName);

        var stopwatch = Stopwatch.StartNew();
        var startTime = DateTime.UtcNow;
        
        try
        {
            _logger.LogDebug("Starting database operation: {OperationName}", operationName);
            
            var result = await operation();
            stopwatch.Stop();
            
            RecordOperation(operationName, stopwatch.ElapsedMilliseconds, true, startTime);
            
            _logger.LogInformation(
                "Database operation {OperationName} completed successfully in {ElapsedMs}ms",
                operationName,
                stopwatch.ElapsedMilliseconds);
                
            return result;
        }
        catch (DbUpdateException ex)
        {
            stopwatch.Stop();
            RecordOperation(operationName, stopwatch.ElapsedMilliseconds, false, startTime);
            
            _logger.LogError(ex,
                "Database update operation {OperationName} failed after {ElapsedMs}ms",
                operationName,
                stopwatch.ElapsedMilliseconds);
            throw;
        }
        catch (InvalidOperationException ex)
        {
            stopwatch.Stop();
            RecordOperation(operationName, stopwatch.ElapsedMilliseconds, false, startTime);
            
            _logger.LogError(ex,
                "Database operation {OperationName} failed with invalid operation after {ElapsedMs}ms",
                operationName,
                stopwatch.ElapsedMilliseconds);
            throw;
        }
        catch (TimeoutException ex)
        {
            stopwatch.Stop();
            RecordOperation(operationName, stopwatch.ElapsedMilliseconds, false, startTime);
            
            _logger.LogError(ex,
                "Database operation {OperationName} timed out after {ElapsedMs}ms",
                operationName,
                stopwatch.ElapsedMilliseconds);
            throw;
        }
        catch (OperationCanceledException ex) when (cancellationToken.IsCancellationRequested)
        {
            stopwatch.Stop();
            RecordOperation(operationName, stopwatch.ElapsedMilliseconds, false, startTime);
            
            _logger.LogWarning(ex,
                "Database operation {OperationName} was cancelled after {ElapsedMs}ms",
                operationName,
                stopwatch.ElapsedMilliseconds);
            throw;
        }
        catch (System.Exception ex)
        {
            stopwatch.Stop();
            RecordOperation(operationName, stopwatch.ElapsedMilliseconds, false, startTime);
            
            _logger.LogError(ex,
                "Database operation {OperationName} failed with unexpected error after {ElapsedMs}ms",
                operationName,
                stopwatch.ElapsedMilliseconds);
            throw;
        }
    }

    public async Task TrackAsync(string operationName, Func<Task> operation, CancellationToken cancellationToken = default)
    {
        await TrackAsync(operationName, async () =>
        {
            await operation();
            return Task.CompletedTask;
        }, cancellationToken);
    }

    public T Track<T>(string operationName, Func<T> operation)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentException.ThrowIfNullOrWhiteSpace(operationName);

        var stopwatch = Stopwatch.StartNew();
        var startTime = DateTime.UtcNow;
        
        try
        {
            _logger.LogDebug("Starting synchronous database operation: {OperationName}", operationName);
            
            var result = operation();
            stopwatch.Stop();
            
            RecordOperation(operationName, stopwatch.ElapsedMilliseconds, true, startTime);
            
            _logger.LogInformation(
                "Synchronous database operation {OperationName} completed successfully in {ElapsedMs}ms",
                operationName,
                stopwatch.ElapsedMilliseconds);
                
            return result;
        }
        catch (System.Exception ex)
        {
            stopwatch.Stop();
            RecordOperation(operationName, stopwatch.ElapsedMilliseconds, false, startTime);
            
            _logger.LogError(ex,
                "Synchronous database operation {OperationName} failed after {ElapsedMs}ms",
                operationName,
                stopwatch.ElapsedMilliseconds);
            throw;
        }
    }

    public void Track(string operationName, Action operation)
    {
        Track(operationName, () =>
        {
            operation();
            return Task.CompletedTask;
        });
    }

    public Task<PerformanceMetrics> GetMetricsAsync(CancellationToken cancellationToken = default)
    {
        var contextName = typeof(TContext).Name;
        var allOperations = new List<OperationResult>();
        
        foreach (var operationList in _operationHistory.Values)
        {
            lock (_lockObject)
            {
                allOperations.AddRange(operationList);
            }
        }

        var metrics = new PerformanceMetrics
        {
            ContextName = contextName,
            TotalOperations = allOperations.Count,
            SuccessfulOperations = allOperations.Count(o => o.Success),
            FailedOperations = allOperations.Count(o => !o.Success),
            LastOperationTime = allOperations.LastOrDefault()?.ExecutedAt ?? DateTime.MinValue
        };

        if (allOperations.Count > 0)
        {
            metrics.AverageExecutionTimeMs = (long)allOperations.Average(o => o.ExecutionTimeMs);
            metrics.TotalExecutionTimeMs = allOperations.Sum(o => o.ExecutionTimeMs);
        }

        // Generate operation breakdown
        var operationGroups = allOperations.GroupBy(o => o.OperationName);
        foreach (var group in operationGroups)
        {
            var operations = group.ToList();
            metrics.OperationBreakdown[group.Key] = new OperationMetrics
            {
                OperationName = group.Key,
                ExecutionCount = operations.Count,
                AverageExecutionTimeMs = (long)operations.Average(o => o.ExecutionTimeMs),
                MinExecutionTimeMs = operations.Min(o => o.ExecutionTimeMs),
                MaxExecutionTimeMs = operations.Max(o => o.ExecutionTimeMs),
                SuccessCount = operations.Count(o => o.Success),
                FailureCount = operations.Count(o => !o.Success),
                LastExecuted = operations.Max(o => o.ExecutedAt)
            };
        }

        return Task.FromResult(metrics);
    }

    private void RecordOperation(string operationName, long executionTimeMs, bool success, DateTime startTime)
    {
        var result = new OperationResult
        {
            OperationName = operationName,
            ExecutionTimeMs = executionTimeMs,
            Success = success,
            ExecutedAt = startTime
        };

        _operationHistory.AddOrUpdate(
            operationName,
            _ => new List<OperationResult> { result },
            (_, existingList) =>
            {
                lock (_lockObject)
                {
                    existingList.Add(result);
                    // Keep only last 1000 entries per operation to prevent memory leaks
                    if (existingList.Count > 1000)
                    {
                        existingList.RemoveRange(0, existingList.Count - 1000);
                    }
                    return existingList;
                }
            });
    }

    private sealed class OperationResult
    {
        public string OperationName { get; set; } = string.Empty;
        public long ExecutionTimeMs { get; set; }
        public bool Success { get; set; }
        public DateTime ExecutedAt { get; set; }
    }
}