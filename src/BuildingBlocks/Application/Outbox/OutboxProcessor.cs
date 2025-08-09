using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using BuildingBlocks.Core.Functional.Results;

namespace BuildingBlocks.Application.Outbox;

/// <summary>
/// Background service for processing outbox events reliably.
/// Provides continuous processing with health monitoring and graceful shutdown.
/// </summary>
public sealed class OutboxProcessor : BackgroundService, IOutboxProcessor
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<OutboxProcessor> _logger;
    private readonly OutboxOptions _options;
    private readonly List<string> _recentErrors = new();
    private readonly object _recentErrorsLock = new();

    private DateTime? _lastProcessingRun;
    private DateTime? _lastCleanupRun;
    private bool _isRunning;

    public OutboxProcessor(
        IServiceProvider serviceProvider,
        ILogger<OutboxProcessor> logger,
        IOptions<OutboxOptions> options)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
        {
            _logger.LogInformation("Outbox processor is disabled");
            return;
        }

        _logger.LogInformation(
            "Outbox processor starting with interval {ProcessingInterval} and batch size {BatchSize}",
            _options.ProcessingInterval, _options.BatchSize);

        _isRunning = true;

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var processingResult = await ProcessPendingAsync(stoppingToken);
                    _lastProcessingRun = DateTime.UtcNow;

                    if (processingResult.IsFailure)
                    {
                        AddRecentError(processingResult.Error.Message);
                    }

                    // Optional cleanup run
                    if (_options.EnableCleanup && ShouldRunCleanup())
                    {
                        _ = Task.Run(async () => 
                        {
                            await RunCleanupAsync(stoppingToken);
                            _lastCleanupRun = DateTime.UtcNow;
                        }, stoppingToken);
                    }

                    // Wait for the next processing cycle
                    await Task.Delay(_options.ProcessingInterval, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    // Expected when cancellation is requested
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Unexpected error in outbox processor main loop");
                    AddRecentError($"Main loop error: {ex.Message}");

                    // Wait before retrying to avoid tight loop on persistent errors
                    await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
                }
            }
        }
        finally
        {
            _isRunning = false;
            _logger.LogInformation("Outbox processor stopped");
        }
    }

    public async Task<Result<OutboxProcessingResult>> ProcessPendingAsync(CancellationToken cancellationToken = default)
    {
        using var activity = Activity.Current?.Source.StartActivity("OutboxProcessor.ProcessPending");
        
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var outboxService = scope.ServiceProvider.GetRequiredService<IOutboxService>();

            var result = await outboxService.ProcessAllPendingEventsAsync(_options.BatchSize, cancellationToken);

            if (result.IsSuccess && result.Value.ProcessedCount > 0)
            {
                _logger.LogDebug(
                    "Processed {ProcessedCount} outbox events: {SuccessfulCount} successful, {FailedCount} failed in {ElapsedMs}ms",
                    result.Value.ProcessedCount, result.Value.SuccessfulCount, result.Value.FailedCount, 
                    result.Value.ProcessingDuration.TotalMilliseconds);

                activity?.SetTag("entries.processed", result.Value.ProcessedCount);
                activity?.SetTag("entries.successful", result.Value.SuccessfulCount);
                activity?.SetTag("entries.failed", result.Value.FailedCount);
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process pending outbox events");
            
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.AddException(ex);

            return Result<OutboxProcessingResult>.Failure(Error.Internal(
                "Outbox processor failed to process pending events",
                "OUTBOX_PROCESSOR_FAILED",
                ex));
        }
    }

    public async Task<Result<OutboxProcessingResult>> ProcessPendingAsync(
        Guid transactionId, 
        CancellationToken cancellationToken = default)
    {
        using var activity = Activity.Current?.Source.StartActivity("OutboxProcessor.ProcessPendingTransaction");
        activity?.SetTag("transaction.id", transactionId);

        try
        {
            using var scope = _serviceProvider.CreateScope();
            var outboxService = scope.ServiceProvider.GetRequiredService<IOutboxService>();

            var result = await outboxService.ProcessPendingEventsAsync(transactionId, cancellationToken);

            if (result.IsSuccess && result.Value.ProcessedCount > 0)
            {
                _logger.LogDebug(
                    "Processed {ProcessedCount} outbox events for transaction {TransactionId}: {SuccessfulCount} successful, {FailedCount} failed",
                    result.Value.ProcessedCount, transactionId, result.Value.SuccessfulCount, result.Value.FailedCount);
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, 
                "Failed to process pending outbox events for transaction {TransactionId}",
                transactionId);

            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.AddException(ex);

            return Result<OutboxProcessingResult>.Failure(Error.Internal(
                $"Outbox processor failed to process events for transaction {transactionId}",
                "OUTBOX_PROCESSOR_TRANSACTION_FAILED",
                ex));
        }
    }

    public async Task<Result<OutboxProcessorHealth>> GetHealthAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var outboxService = scope.ServiceProvider.GetRequiredService<IOutboxService>();

            var statisticsResult = await outboxService.GetStatisticsAsync(cancellationToken);
            if (statisticsResult.IsFailure)
            {
                return Result<OutboxProcessorHealth>.Failure(statisticsResult.Error);
            }

            var statistics = statisticsResult.Value;
            var timeSinceLastRun = _lastProcessingRun.HasValue 
                ? DateTime.UtcNow - _lastProcessingRun.Value 
                : (TimeSpan?)null;

            List<string> recentErrorsCopy;
            lock (_recentErrorsLock)
            {
                recentErrorsCopy = _recentErrors.TakeLast(10).ToList();
            }

            var health = new OutboxProcessorHealth(
                IsRunning: _isRunning,
                LastProcessingRun: _lastProcessingRun,
                TimeSinceLastRun: timeSinceLastRun,
                PendingEventCount: statistics.PendingCount,
                DeadLetterEventCount: statistics.DeadLetterCount,
                RecentErrors: recentErrorsCopy);

            return Result<OutboxProcessorHealth>.Success(health);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get outbox processor health");

            return Result<OutboxProcessorHealth>.Failure(Error.Internal(
                "Failed to get outbox processor health",
                "OUTBOX_PROCESSOR_HEALTH_FAILED",
                ex));
        }
    }

    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!_options.Enabled)
        {
            _logger.LogInformation("Outbox processor is disabled, not starting");
            return;
        }

        _logger.LogInformation("Starting outbox processor");
        await base.StartAsync(cancellationToken);
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Stopping outbox processor gracefully");
        _isRunning = false;
        
        await base.StopAsync(cancellationToken);
        
        _logger.LogInformation("Outbox processor stopped successfully");
    }

    private async Task RunCleanupAsync(CancellationToken cancellationToken)
    {
        using var activity = Activity.Current?.Source.StartActivity("OutboxProcessor.Cleanup");
        
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var repository = scope.ServiceProvider.GetRequiredService<IOutboxRepository>();

            var deletedCount = await repository.CleanupCompletedEntriesAsync(
                _options.CompletedRetentionPeriod,
                _options.CleanupBatchSize,
                cancellationToken);

            if (deletedCount > 0)
            {
                _logger.LogInformation(
                    "Cleanup completed: removed {DeletedCount} completed outbox entries older than {RetentionPeriod}",
                    deletedCount, _options.CompletedRetentionPeriod);
                
                activity?.SetTag("entries.cleaned", deletedCount);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to run outbox cleanup");
            AddRecentError($"Cleanup error: {ex.Message}");
            
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.AddException(ex);
        }
    }

    private bool ShouldRunCleanup()
    {
        // Simple time-based cleanup trigger - every hour
        // In a production environment, you might want more sophisticated scheduling
        var now = DateTime.UtcNow;
        var timeSinceLastCleanup = _lastCleanupRun.HasValue ? now - _lastCleanupRun.Value : TimeSpan.MaxValue;
        
        return timeSinceLastCleanup >= _options.CleanupInterval;
    }

    private void AddRecentError(string errorMessage)
    {
        lock (_recentErrorsLock)
        {
            _recentErrors.Add($"{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} - {errorMessage}");
            
            // Keep only the most recent errors
            if (_recentErrors.Count > 50)
            {
                _recentErrors.RemoveRange(0, _recentErrors.Count - 50);
            }
        }
    }
}