using Microsoft.Extensions.Logging;

namespace BuildingBlocks.Core.Events.Replay;

/// <summary>
/// Default implementation of event replay service.
/// Created for Epic 06 Story 04 - Event Replay and Recovery Service.
/// Manages the lifecycle of replay operations with comprehensive monitoring and recovery.
/// </summary>
public sealed class EventReplayManager : IEventReplayService
{
    private readonly ILogger<EventReplayManager> _logger;
    private readonly Dictionary<Guid, InternalReplayOperation> _activeOperations = new();
    private readonly object _lock = new();
    private readonly ReplayServiceConfiguration _configuration;
    
    public EventReplayManager(
        ILogger<EventReplayManager> logger,
        ReplayServiceConfiguration configuration)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
    }

    /// <inheritdoc />
    public async Task<Result<Guid>> StartReplayAsync(
        ReplayRequest request, 
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Validate request first
            var validationResult = await ValidateRequestAsync(request, cancellationToken);
            if (!validationResult.IsValid)
            {
                _logger.LogWarning("Replay request validation failed: {Errors}", 
                    string.Join(", ", validationResult.Errors.Select(e => e.Message)));
                return Result<Guid>.Failure(Error.Validation("Invalid replay request", validationResult.Errors));
            }

            // Check service capacity
            lock (_lock)
            {
                if (_activeOperations.Count >= _configuration.MaxConcurrentOperations)
                {
                    _logger.LogWarning("Cannot start replay - maximum concurrent operations reached ({Max})",
                        _configuration.MaxConcurrentOperations);
                    return Result<Guid>.Failure(Error.Conflict("Service at capacity"));
                }
            }

            var operationId = Guid.NewGuid();
            var operation = new InternalReplayOperation
            {
                Id = operationId,
                Name = request.OperationName,
                Configuration = request.Configuration,
                Status = ReplayOperationStatus.Starting,
                StartedAtUtc = DateTime.UtcNow,
                Phase = ReplayPhase.Initializing,
                HealthStatus = ReplayHealthStatus.Healthy
            };

            lock (_lock)
            {
                _activeOperations[operationId] = operation;
            }

            _logger.LogInformation("Started replay operation {OperationId}: {Name}", 
                operationId, request.OperationName);

            // Start the actual replay process asynchronously
            _ = Task.Run(async () => await ExecuteReplayAsync(operation, cancellationToken), 
                        cancellationToken);

            return Result<Guid>.Success(operationId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start replay operation");
            return Result<Guid>.Failure(Error.Unexpected("Failed to start replay operation", ex));
        }
    }

    /// <inheritdoc />
    public async Task<Result<ReplayStatus>> GetStatusAsync(
        Guid operationId, 
        CancellationToken cancellationToken = default)
    {
        try
        {
            lock (_lock)
            {
                if (!_activeOperations.TryGetValue(operationId, out var operation))
                {
                    return Result<ReplayStatus>.Failure(Error.NotFound($"Replay operation {operationId} not found"));
                }

                var status = CreateReplayStatus(operation);
                return Result<ReplayStatus>.Success(status);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get replay status for {OperationId}", operationId);
            return Result<ReplayStatus>.Failure(Error.Unexpected("Failed to get replay status", ex));
        }
    }

    /// <inheritdoc />
    public async Task<Result<ReplayProgress>> GetProgressAsync(
        Guid operationId, 
        CancellationToken cancellationToken = default)
    {
        try
        {
            lock (_lock)
            {
                if (!_activeOperations.TryGetValue(operationId, out var operation))
                {
                    return Result<ReplayProgress>.Failure(Error.NotFound($"Replay operation {operationId} not found"));
                }

                var progress = CreateReplayProgress(operation);
                return Result<ReplayProgress>.Success(progress);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get replay progress for {OperationId}", operationId);
            return Result<ReplayProgress>.Failure(Error.Unexpected("Failed to get replay progress", ex));
        }
    }

    /// <inheritdoc />
    public async Task<Result<ReplayPerformanceMetrics>> GetMetricsAsync(
        Guid operationId, 
        CancellationToken cancellationToken = default)
    {
        try
        {
            lock (_lock)
            {
                if (!_activeOperations.TryGetValue(operationId, out var operation))
                {
                    return Result<ReplayPerformanceMetrics>.Failure(
                        Error.NotFound($"Replay operation {operationId} not found"));
                }

                var metrics = operation.PerformanceMetrics ?? CreateDefaultMetrics();
                return Result<ReplayPerformanceMetrics>.Success(metrics);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get replay metrics for {OperationId}", operationId);
            return Result<ReplayPerformanceMetrics>.Failure(Error.Unexpected("Failed to get replay metrics", ex));
        }
    }

    /// <inheritdoc />
    public async Task<Result> PauseAsync(
        Guid operationId, 
        CancellationToken cancellationToken = default)
    {
        try
        {
            lock (_lock)
            {
                if (!_activeOperations.TryGetValue(operationId, out var operation))
                {
                    return Result.Failure(Error.NotFound($"Replay operation {operationId} not found"));
                }

                if (operation.Status != ReplayOperationStatus.Running)
                {
                    return Result.Failure(Error.Conflict("Cannot pause operation that is not running"));
                }

                operation.Status = ReplayOperationStatus.Paused;
                operation.CancellationTokenSource?.Cancel();

                _logger.LogInformation("Paused replay operation {OperationId}", operationId);
                return Result.Success();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to pause replay operation {OperationId}", operationId);
            return Result.Failure(Error.Unexpected("Failed to pause replay operation", ex));
        }
    }

    /// <inheritdoc />
    public async Task<Result> ResumeAsync(
        Guid operationId, 
        CancellationToken cancellationToken = default)
    {
        try
        {
            lock (_lock)
            {
                if (!_activeOperations.TryGetValue(operationId, out var operation))
                {
                    return Result.Failure(Error.NotFound($"Replay operation {operationId} not found"));
                }

                if (operation.Status != ReplayOperationStatus.Paused)
                {
                    return Result.Failure(Error.Conflict("Cannot resume operation that is not paused"));
                }

                operation.Status = ReplayOperationStatus.Running;
                operation.CancellationTokenSource = new CancellationTokenSource();

                _logger.LogInformation("Resumed replay operation {OperationId}", operationId);

                // Restart the replay process
                _ = Task.Run(async () => await ExecuteReplayAsync(operation, cancellationToken), 
                            cancellationToken);

                return Result.Success();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to resume replay operation {OperationId}", operationId);
            return Result.Failure(Error.Unexpected("Failed to resume replay operation", ex));
        }
    }

    /// <inheritdoc />
    public async Task<Result> CancelAsync(
        Guid operationId, 
        CancellationToken cancellationToken = default)
    {
        try
        {
            lock (_lock)
            {
                if (!_activeOperations.TryGetValue(operationId, out var operation))
                {
                    return Result.Failure(Error.NotFound($"Replay operation {operationId} not found"));
                }

                operation.Status = ReplayOperationStatus.Cancelled;
                operation.CompletedAtUtc = DateTime.UtcNow;
                operation.CancellationTokenSource?.Cancel();

                _logger.LogInformation("Cancelled replay operation {OperationId}", operationId);
                return Result.Success();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to cancel replay operation {OperationId}", operationId);
            return Result.Failure(Error.Unexpected("Failed to cancel replay operation", ex));
        }
    }

    /// <inheritdoc />
    public async Task<Result<IReadOnlyList<ReplayStatus>>> GetActiveOperationsAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            lock (_lock)
            {
                var activeStatuses = _activeOperations.Values
                    .Where(op => op.IsActive)
                    .Select(CreateReplayStatus)
                    .ToList();

                return Result<IReadOnlyList<ReplayStatus>>.Success(activeStatuses);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get active operations");
            return Result<IReadOnlyList<ReplayStatus>>.Failure(
                Error.Unexpected("Failed to get active operations", ex));
        }
    }

    /// <inheritdoc />
    public async Task<Result<IReadOnlyList<ReplayResult>>> GetHistoryAsync(
        DateTime fromUtc, 
        DateTime toUtc, 
        CancellationToken cancellationToken = default)
    {
        // This would typically query a persistent store
        // For this implementation, we'll return completed operations from memory
        try
        {
            lock (_lock)
            {
                var completedOperations = _activeOperations.Values
                    .Where(op => !op.IsActive && 
                                op.StartedAtUtc >= fromUtc && 
                                op.StartedAtUtc <= toUtc)
                    .Select(CreateReplayResult)
                    .ToList();

                return Result<IReadOnlyList<ReplayResult>>.Success(completedOperations);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get replay history");
            return Result<IReadOnlyList<ReplayResult>>.Failure(
                Error.Unexpected("Failed to get replay history", ex));
        }
    }

    /// <inheritdoc />
    public async Task<Result<int>> CleanupAsync(
        DateTime olderThanUtc, 
        CancellationToken cancellationToken = default)
    {
        try
        {
            int cleanedCount = 0;
            var operationsToRemove = new List<Guid>();

            lock (_lock)
            {
                foreach (var (id, operation) in _activeOperations)
                {
                    if (!operation.IsActive && operation.StartedAtUtc < olderThanUtc)
                    {
                        operationsToRemove.Add(id);
                    }
                }

                foreach (var id in operationsToRemove)
                {
                    _activeOperations.Remove(id);
                    cleanedCount++;
                }
            }

            _logger.LogInformation("Cleaned up {Count} completed replay operations", cleanedCount);
            return Result<int>.Success(cleanedCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to cleanup replay operations");
            return Result<int>.Failure(Error.Unexpected("Failed to cleanup operations", ex));
        }
    }

    /// <inheritdoc />
    public async Task<Result<ReplayDiagnosticInfo>> GetDiagnosticsAsync(
        Guid operationId, 
        CancellationToken cancellationToken = default)
    {
        try
        {
            lock (_lock)
            {
                if (!_activeOperations.TryGetValue(operationId, out var operation))
                {
                    return Result<ReplayDiagnosticInfo>.Failure(
                        Error.NotFound($"Replay operation {operationId} not found"));
                }

                var diagnostics = operation.DiagnosticInfo ?? CreateDefaultDiagnostics();
                return Result<ReplayDiagnosticInfo>.Success(diagnostics);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get diagnostics for {OperationId}", operationId);
            return Result<ReplayDiagnosticInfo>.Failure(
                Error.Unexpected("Failed to get replay diagnostics", ex));
        }
    }

    /// <inheritdoc />
    public async Task<Validation<Unit>> ValidateRequestAsync(
        ReplayRequest request, 
        CancellationToken cancellationToken = default)
    {
        var errors = new List<Error>();

        if (request == null)
        {
            errors.Add(Error.Validation("Request cannot be null"));
        }
        else
        {
            if (string.IsNullOrWhiteSpace(request.OperationName))
            {
                errors.Add(Error.Validation("Operation name is required"));
            }

            if (request.Configuration == null)
            {
                errors.Add(Error.Validation("Configuration is required"));
            }
            else
            {
                // Validate configuration
                if (request.Configuration.BatchSize <= 0)
                {
                    errors.Add(Error.Validation("Batch size must be greater than zero"));
                }

                if (request.Configuration.MaxConcurrency <= 0)
                {
                    errors.Add(Error.Validation("Max concurrency must be greater than zero"));
                }

                if (request.Configuration.MaxRetryAttempts < 0)
                {
                    errors.Add(Error.Validation("Max retry attempts cannot be negative"));
                }
            }

            if (request.TimeRange != null)
            {
                if (request.TimeRange.StartUtc >= request.TimeRange.EndUtc)
                {
                    errors.Add(Error.Validation("Start time must be before end time"));
                }
            }
        }

        return errors.Count == 0 
            ? Validation<Unit>.Valid(Unit.Value) 
            : Validation<Unit>.Invalid(errors);
    }

    /// <inheritdoc />
    public async Task<Result<ReplayServiceHealth>> GetServiceHealthAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            lock (_lock)
            {
                var activeCount = _activeOperations.Values.Count(op => op.IsActive);
                var averageRate = _activeOperations.Values
                    .Where(op => op.IsActive)
                    .Average(op => op.AverageProcessingRate ?? 0.0);

                var health = new ReplayServiceHealth
                {
                    Status = DetermineHealthStatus(activeCount),
                    ActiveOperationsCount = activeCount,
                    MaxConcurrentOperations = _configuration.MaxConcurrentOperations,
                    ResourceUtilizationPercent = (double)activeCount / _configuration.MaxConcurrentOperations * 100.0,
                    AverageProcessingRate = averageRate,
                    Uptime = DateTime.UtcNow - _configuration.StartTime,
                    ServiceVersion = _configuration.ServiceVersion
                };

                return Result<ReplayServiceHealth>.Success(health);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get service health");
            return Result<ReplayServiceHealth>.Failure(
                Error.Unexpected("Failed to get service health", ex));
        }
    }

    private async Task ExecuteReplayAsync(InternalReplayOperation operation, CancellationToken cancellationToken)
    {
        // This would contain the actual replay logic
        // For this implementation, we'll simulate the process
        try
        {
            operation.Status = ReplayOperationStatus.Running;
            operation.Phase = ReplayPhase.DiscoveringEvents;

            // Simulate replay process
            await Task.Delay(1000, cancellationToken);
            
            operation.Phase = ReplayPhase.ProcessingEvents;
            operation.TotalEventsCount = 1000; // Simulated
            
            // Simulate processing events
            for (int i = 0; i < operation.TotalEventsCount && !cancellationToken.IsCancellationRequested; i++)
            {
                await Task.Delay(10, cancellationToken);
                operation.ProcessedEventsCount = i + 1;
                operation.ProcessingRate = operation.ProcessedEventsCount / DateTime.UtcNow.Subtract(operation.StartedAtUtc).TotalSeconds;
            }

            operation.Status = ReplayOperationStatus.Completed;
            operation.Phase = ReplayPhase.Completed;
            operation.CompletedAtUtc = DateTime.UtcNow;

            _logger.LogInformation("Completed replay operation {OperationId}", operation.Id);
        }
        catch (OperationCanceledException)
        {
            if (operation.Status != ReplayOperationStatus.Cancelled)
            {
                operation.Status = ReplayOperationStatus.Paused;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Replay operation {OperationId} failed", operation.Id);
            operation.Status = ReplayOperationStatus.Failed;
            operation.Phase = ReplayPhase.Terminated;
            operation.ErrorMessage = ex.Message;
            operation.CompletedAtUtc = DateTime.UtcNow;
        }
    }

    private ReplayStatus CreateReplayStatus(InternalReplayOperation operation)
    {
        var progressPercentage = operation.TotalEventsCount > 0
            ? (double)operation.ProcessedEventsCount / operation.TotalEventsCount.Value * 100.0
            : null;

        return new ReplayStatus
        {
            OperationId = operation.Id,
            OperationName = operation.Name,
            Status = operation.Status,
            StartedAtUtc = operation.StartedAtUtc,
            CompletedAtUtc = operation.CompletedAtUtc,
            CurrentPhase = operation.Phase,
            TotalEventsCount = operation.TotalEventsCount,
            ProcessedEventsCount = operation.ProcessedEventsCount,
            FailedEventsCount = operation.FailedEventsCount,
            SkippedEventsCount = operation.SkippedEventsCount,
            ProgressPercentage = progressPercentage,
            ProcessingRatePerSecond = operation.ProcessingRate,
            AverageProcessingRatePerSecond = operation.AverageProcessingRate,
            ErrorMessage = operation.ErrorMessage,
            HealthStatus = operation.HealthStatus,
            LastUpdatedUtc = DateTime.UtcNow
        };
    }

    private ReplayProgress CreateReplayProgress(InternalReplayOperation operation)
    {
        return new ReplayProgress
        {
            OperationId = operation.Id,
            CurrentPhase = operation.Phase,
            TotalEventsCount = operation.TotalEventsCount,
            ProcessedEventsCount = operation.ProcessedEventsCount,
            FailedEventsCount = operation.FailedEventsCount,
            SkippedEventsCount = operation.SkippedEventsCount,
            ProcessingRatePerSecond = operation.ProcessingRate,
            LastUpdatedUtc = DateTime.UtcNow
        };
    }

    private ReplayResult CreateReplayResult(InternalReplayOperation operation)
    {
        return new ReplayResult
        {
            OperationId = operation.Id,
            OperationName = operation.Name,
            FinalStatus = operation.Status,
            StartedAtUtc = operation.StartedAtUtc,
            CompletedAtUtc = operation.CompletedAtUtc ?? DateTime.UtcNow,
            TotalEventsProcessed = operation.ProcessedEventsCount,
            SuccessfulEventsCount = operation.ProcessedEventsCount - operation.FailedEventsCount,
            FailedEventsCount = operation.FailedEventsCount,
            SkippedEventsCount = operation.SkippedEventsCount,
            AverageProcessingRate = operation.AverageProcessingRate ?? 0.0,
            Configuration = operation.Configuration,
            ErrorMessage = operation.ErrorMessage
        };
    }

    private ReplayPerformanceMetrics CreateDefaultMetrics()
    {
        return new ReplayPerformanceMetrics
        {
            EventsPerSecond = 0.0,
            AverageEventProcessingTimeMs = 0.0,
            MinEventProcessingTimeMs = 0.0,
            MaxEventProcessingTimeMs = 0.0,
            P95ProcessingTimeMs = 0.0,
            P99ProcessingTimeMs = 0.0,
            AverageBatchProcessingTimeMs = 0.0,
            AverageDelayBetweenBatchesMs = 0.0,
            PeakMemoryUsageBytes = 0L,
            AverageMemoryUsageBytes = 0L,
            PeakCpuUsagePercent = 0.0,
            AverageCpuUsagePercent = 0.0,
            TotalNetworkRequests = 0L,
            FailedNetworkRequests = 0L,
            AverageNetworkRequestTimeMs = 0.0,
            TotalDataReadBytes = 0L,
            TotalDataWrittenBytes = 0L,
            GarbageCollectionCount = 0,
            TotalGcTimeMs = 0.0,
            ResourceWaitCount = 0,
            TotalResourceWaitTimeMs = 0.0,
            CurrentConcurrencyLevel = 0,
            MaxConcurrencyLevel = 0,
            CacheHits = 0L,
            CacheMisses = 0L,
            TotalRetryAttempts = 0L,
            SuccessfulRetries = 0L,
            EfficiencyScore = 0.0,
            LastUpdatedUtc = DateTime.UtcNow
        };
    }

    private ReplayDiagnosticInfo CreateDefaultDiagnostics()
    {
        return new ReplayDiagnosticInfo
        {
            CollectedAtUtc = DateTime.UtcNow
        };
    }

    private ReplayHealthStatus DetermineHealthStatus(int activeOperations)
    {
        var utilizationPercent = (double)activeOperations / _configuration.MaxConcurrentOperations * 100.0;

        return utilizationPercent switch
        {
            <= 70.0 => ReplayHealthStatus.Healthy,
            <= 85.0 => ReplayHealthStatus.Degraded,
            <= 95.0 => ReplayHealthStatus.Critical,
            _ => ReplayHealthStatus.Unavailable
        };
    }
}

/// <summary>
/// Internal representation of a replay operation.
/// </summary>
internal sealed class InternalReplayOperation
{
    public Guid Id { get; init; }
    public required string Name { get; init; }
    public required ReplayConfiguration Configuration { get; init; }
    public ReplayOperationStatus Status { get; set; }
    public DateTime StartedAtUtc { get; init; }
    public DateTime? CompletedAtUtc { get; set; }
    public ReplayPhase Phase { get; set; }
    public long? TotalEventsCount { get; set; }
    public long ProcessedEventsCount { get; set; }
    public long FailedEventsCount { get; set; }
    public long SkippedEventsCount { get; set; }
    public double? ProcessingRate { get; set; }
    public double? AverageProcessingRate { get; set; }
    public string? ErrorMessage { get; set; }
    public ReplayHealthStatus HealthStatus { get; set; }
    public ReplayPerformanceMetrics? PerformanceMetrics { get; set; }
    public ReplayDiagnosticInfo? DiagnosticInfo { get; set; }
    public CancellationTokenSource? CancellationTokenSource { get; set; }

    public bool IsActive => Status is ReplayOperationStatus.Running or 
                                   ReplayOperationStatus.Starting or 
                                   ReplayOperationStatus.Paused;
}

/// <summary>
/// Configuration for the replay service.
/// </summary>
public sealed record ReplayServiceConfiguration
{
    public int MaxConcurrentOperations { get; init; } = 10;
    public DateTime StartTime { get; init; } = DateTime.UtcNow;
    public string? ServiceVersion { get; init; }
}