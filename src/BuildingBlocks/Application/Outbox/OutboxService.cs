using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using BuildingBlocks.Core.Domain.Events;
using BuildingBlocks.Core.Functional.Results;
using BuildingBlocks.Infrastructure.Outbox;
using BuildingBlocks.Application.Events;

namespace BuildingBlocks.Application.Outbox;

/// <summary>
/// Service implementation for managing outbox entries and reliable event publishing.
/// Provides atomic event storage and reliable processing with comprehensive error handling.
/// </summary>
public sealed class OutboxService : IOutboxService
{
    private readonly IOutboxRepository _repository;
    private readonly IEventDispatcher _eventDispatcher;
    private readonly ILogger<OutboxService> _logger;
    private readonly OutboxOptions _options;

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public OutboxService(
        IOutboxRepository repository,
        IEventDispatcher eventDispatcher,
        ILogger<OutboxService> logger,
        IOptions<OutboxOptions> options)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _eventDispatcher = eventDispatcher ?? throw new ArgumentNullException(nameof(eventDispatcher));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
    }

    public async Task<Result<int>> StoreEventsAsync(
        IReadOnlyList<IDomainEvent> events,
        Guid transactionId,
        string? traceId = null,
        Guid? requestId = null,
        IReadOnlyDictionary<string, object>? metadata = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (!events.Any())
            {
                _logger.LogDebug("No events to store for transaction {TransactionId}", transactionId);
                return Result<int>.Success(0);
            }

            var serializedMetadata = metadata?.Any() == true 
                ? JsonSerializer.Serialize(metadata, SerializerOptions) 
                : null;

            var outboxEntries = events.Select(domainEvent =>
            {
                var eventData = JsonSerializer.Serialize(domainEvent, domainEvent.GetType(), SerializerOptions);
                var eventType = domainEvent.GetType().AssemblyQualifiedName 
                    ?? throw new InvalidOperationException($"Could not get assembly qualified name for event type {domainEvent.GetType().FullName}");

                return new OutboxEntry(
                    OutboxEntryId.New(),
                    transactionId,
                    eventType,
                    eventData,
                    traceId,
                    requestId,
                    metadata?.GetValueOrDefault("TenantId")?.ToString(),
                    serializedMetadata);
            }).ToList();

            await _repository.AddAsync(outboxEntries, cancellationToken);

            _logger.LogDebug(
                "Stored {EventCount} events in outbox for transaction {TransactionId} (Trace: {TraceId})",
                events.Count, transactionId, traceId);

            return Result<int>.Success(events.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to store {EventCount} events in outbox for transaction {TransactionId}",
                events.Count, transactionId);

            return Result<int>.Failure(Error.Failure(
                "Failed to store events in outbox",
                "OUTBOX_STORE_FAILED",
                ex));
        }
    }

    public async Task<Result<OutboxProcessingResult>> ProcessPendingEventsAsync(
        Guid transactionId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var activity = Activity.Current?.Source.StartActivity("ProcessTransactionOutboxEvents");
            activity?.SetTag("transaction.id", transactionId);

            var entries = await _repository.GetPendingByTransactionAsync(transactionId, cancellationToken);

            if (!entries.Any())
            {
                _logger.LogDebug("No pending events found for transaction {TransactionId}", transactionId);
                return Result<OutboxProcessingResult>.Success(new OutboxProcessingResult(
                    ProcessedCount: 0,
                    SuccessfulCount: 0,
                    FailedCount: 0,
                    MovedToDeadLetterCount: 0,
                    ProcessingDuration: TimeSpan.Zero,
                    Errors: []));
            }

            _logger.LogDebug(
                "Processing {EntryCount} pending outbox events for transaction {TransactionId}",
                entries.Count, transactionId);

            var stopwatch = Stopwatch.StartNew();
            var result = await ProcessEntries(entries, cancellationToken);
            stopwatch.Stop();

            activity?.SetTag("entries.processed", result.ProcessedCount);
            activity?.SetTag("entries.successful", result.SuccessfulCount);
            activity?.SetTag("entries.failed", result.FailedCount);
            activity?.SetTag("processing.duration_ms", stopwatch.ElapsedMilliseconds);

            return Result<OutboxProcessingResult>.Success(result with { ProcessingDuration = stopwatch.Elapsed });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to process pending events for transaction {TransactionId}",
                transactionId);

            return Result<OutboxProcessingResult>.Failure(Error.Failure(
                "Failed to process transaction outbox events",
                "OUTBOX_TRANSACTION_PROCESSING_FAILED",
                ex));
        }
    }

    public async Task<Result<OutboxProcessingResult>> ProcessAllPendingEventsAsync(
        int? batchSize = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (!_options.Enabled)
            {
                _logger.LogDebug("Outbox processing is disabled");
                return Result<OutboxProcessingResult>.Success(new OutboxProcessingResult(
                    ProcessedCount: 0,
                    SuccessfulCount: 0,
                    FailedCount: 0,
                    MovedToDeadLetterCount: 0,
                    ProcessingDuration: TimeSpan.Zero,
                    Errors: []));
            }

            using var activity = Activity.Current?.Source.StartActivity("ProcessAllPendingOutboxEvents");

            var effectiveBatchSize = batchSize ?? _options.BatchSize;
            var entries = await _repository.GetPendingAsync(
                effectiveBatchSize,
                _options.ProcessingTimeoutMinutes,
                cancellationToken: cancellationToken);

            if (!entries.Any())
            {
                _logger.LogDebug("No pending outbox events found for processing");
                return Result<OutboxProcessingResult>.Success(new OutboxProcessingResult(
                    ProcessedCount: 0,
                    SuccessfulCount: 0,
                    FailedCount: 0,
                    MovedToDeadLetterCount: 0,
                    ProcessingDuration: TimeSpan.Zero,
                    Errors: []));
            }

            _logger.LogDebug("Processing {EntryCount} pending outbox events", entries.Count);

            var stopwatch = Stopwatch.StartNew();
            var result = await ProcessEntries(entries, cancellationToken);
            stopwatch.Stop();

            activity?.SetTag("entries.processed", result.ProcessedCount);
            activity?.SetTag("entries.successful", result.SuccessfulCount);
            activity?.SetTag("entries.failed", result.FailedCount);
            activity?.SetTag("processing.duration_ms", stopwatch.ElapsedMilliseconds);

            _logger.LogInformation(
                "Processed {ProcessedCount} outbox events: {SuccessfulCount} successful, {FailedCount} failed, {DeadLetterCount} moved to dead letter in {ElapsedMs}ms",
                result.ProcessedCount, result.SuccessfulCount, result.FailedCount, result.MovedToDeadLetterCount, stopwatch.ElapsedMilliseconds);

            return Result<OutboxProcessingResult>.Success(result with { ProcessingDuration = stopwatch.Elapsed });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process all pending outbox events");

            return Result<OutboxProcessingResult>.Failure(Error.Failure(
                "Failed to process pending outbox events",
                "OUTBOX_PROCESSING_FAILED",
                ex));
        }
    }

    public async Task<Result<OutboxStatistics>> GetStatisticsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var statistics = await _repository.GetStatisticsAsync(cancellationToken);
            return Result<OutboxStatistics>.Success(statistics);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get outbox statistics");

            return Result<OutboxStatistics>.Failure(Error.Failure(
                "Failed to get outbox statistics",
                "OUTBOX_STATISTICS_FAILED",
                ex));
        }
    }

    public async Task<Result<OutboxProcessingResult>> RetryFailedEventsAsync(
        int? maxEntries = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var activity = Activity.Current?.Source.StartActivity("RetryFailedOutboxEvents");

            var effectiveMaxEntries = maxEntries ?? _options.BatchSize;
            var entries = await _repository.GetFailedReadyForRetryAsync(effectiveMaxEntries, cancellationToken);

            if (!entries.Any())
            {
                _logger.LogDebug("No failed outbox events ready for retry");
                return Result<OutboxProcessingResult>.Success(new OutboxProcessingResult(
                    ProcessedCount: 0,
                    SuccessfulCount: 0,
                    FailedCount: 0,
                    MovedToDeadLetterCount: 0,
                    ProcessingDuration: TimeSpan.Zero,
                    Errors: []));
            }

            _logger.LogInformation("Retrying {EntryCount} failed outbox events", entries.Count);

            var stopwatch = Stopwatch.StartNew();
            var result = await ProcessEntries(entries, cancellationToken);
            stopwatch.Stop();

            activity?.SetTag("entries.retried", result.ProcessedCount);
            activity?.SetTag("entries.successful", result.SuccessfulCount);
            activity?.SetTag("retry.duration_ms", stopwatch.ElapsedMilliseconds);

            return Result<OutboxProcessingResult>.Success(result with { ProcessingDuration = stopwatch.Elapsed });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retry failed outbox events");

            return Result<OutboxProcessingResult>.Failure(Error.Failure(
                "Failed to retry failed outbox events",
                "OUTBOX_RETRY_FAILED",
                ex));
        }
    }

    public async Task<Result<OutboxProcessingResult>> ReprocessDeadLetterEventsAsync(
        IReadOnlyList<OutboxEntryId>? entryIds = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var activity = Activity.Current?.Source.StartActivity("ReprocessDeadLetterEvents");

            IReadOnlyList<OutboxEntry> entries;

            if (entryIds?.Any() == true)
            {
                entries = await _repository.GetByIdsAsync(entryIds, cancellationToken);
                entries = entries.Where(e => e.Status == OutboxEntryStatus.DeadLetter).ToList();
            }
            else
            {
                entries = await _repository.GetDeadLetterEntriesAsync(_options.BatchSize, cancellationToken);
            }

            if (!entries.Any())
            {
                _logger.LogDebug("No dead letter outbox events found for reprocessing");
                return Result<OutboxProcessingResult>.Success(new OutboxProcessingResult(
                    ProcessedCount: 0,
                    SuccessfulCount: 0,
                    FailedCount: 0,
                    MovedToDeadLetterCount: 0,
                    ProcessingDuration: TimeSpan.Zero,
                    Errors: []));
            }

            // Reset dead letter entries to pending status
            foreach (var entry in entries)
            {
                entry.ResetToPending();
            }

            await _repository.UpdateBatchAsync(entries, cancellationToken);

            _logger.LogInformation("Reprocessing {EntryCount} dead letter outbox events", entries.Count);

            var stopwatch = Stopwatch.StartNew();
            var result = await ProcessEntries(entries, cancellationToken);
            stopwatch.Stop();

            activity?.SetTag("entries.reprocessed", result.ProcessedCount);
            activity?.SetTag("entries.successful", result.SuccessfulCount);

            return Result<OutboxProcessingResult>.Success(result with { ProcessingDuration = stopwatch.Elapsed });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to reprocess dead letter outbox events");

            return Result<OutboxProcessingResult>.Failure(Error.Failure(
                "Failed to reprocess dead letter events",
                "OUTBOX_REPROCESS_FAILED",
                ex));
        }
    }

    private async Task<OutboxProcessingResult> ProcessEntries(
        IReadOnlyList<OutboxEntry> entries,
        CancellationToken cancellationToken)
    {
        var processedCount = 0;
        var successfulCount = 0;
        var failedCount = 0;
        var movedToDeadLetterCount = 0;
        var errors = new List<OutboxProcessingError>();

        // Process entries with controlled concurrency
        using var semaphore = new SemaphoreSlim(_options.MaxConcurrency, _options.MaxConcurrency);
        
        var processingTasks = entries.Select(async entry =>
        {
            await semaphore.WaitAsync(cancellationToken);
            try
            {
                var result = await ProcessSingleEntry(entry, cancellationToken);
                
                Interlocked.Increment(ref processedCount);
                
                if (result.IsSuccess)
                {
                    Interlocked.Increment(ref successfulCount);
                }
                else
                {
                    Interlocked.Increment(ref failedCount);
                    
                    if (entry.Status == OutboxEventStatus.DeadLetter)
                    {
                        Interlocked.Increment(ref movedToDeadLetterCount);
                    }

                    lock (errors)
                    {
                        errors.Add(new OutboxProcessingError(
                            entry.Id,
                            entry.EventType,
                            result.Error.Message,
                            DateTime.UtcNow));
                    }
                }
            }
            finally
            {
                semaphore.Release();
            }
        });

        await Task.WhenAll(processingTasks);

        return new OutboxProcessingResult(
            ProcessedCount: processedCount,
            SuccessfulCount: successfulCount,
            FailedCount: failedCount,
            MovedToDeadLetterCount: movedToDeadLetterCount,
            ProcessingDuration: TimeSpan.Zero, // Will be set by caller
            Errors: errors);
    }

    private async Task<Result> ProcessSingleEntry(OutboxEntry entry, CancellationToken cancellationToken)
    {
        using var activity = Activity.Current?.Source.StartActivity("ProcessSingleOutboxEntry");
        activity?.SetTag("entry.id", entry.Id.Value);
        activity?.SetTag("entry.type", entry.EventType);
        activity?.SetTag("entry.transaction_id", entry.TransactionId);
        activity?.SetTag("entry.retry_count", entry.RetryCount);

        try
        {
            // Mark as processing
            entry.MarkAsProcessing();
            await _repository.UpdateAsync(entry, cancellationToken);

            // Deserialize event
            var eventType = Type.GetType(entry.EventType);
            if (eventType == null)
            {
                var error = $"Event type '{entry.EventType}' not found. Assembly may not be loaded.";
                entry.MoveToDeadLetter(error);
                await _repository.UpdateAsync(entry, cancellationToken);

                _logger.LogError(
                    "Cannot deserialize outbox event {EntryId}: {Error}",
                    entry.Id, error);

                return Result.Failure(Error.Failure(error, "OUTBOX_EVENT_TYPE_NOT_FOUND"));
            }

            var domainEvent = (IDomainEvent?)JsonSerializer.Deserialize(entry.EventData, eventType, SerializerOptions);
            if (domainEvent == null)
            {
                var error = "Failed to deserialize event data";
                entry.MoveToDeadLetter(error);
                await _repository.UpdateAsync(entry, cancellationToken);

                _logger.LogError(
                    "Cannot deserialize outbox event {EntryId} data for type {EventType}",
                    entry.Id, entry.EventType);

                return Result.Failure(Error.Failure(error, "OUTBOX_EVENT_DESERIALIZATION_FAILED"));
            }

            // Dispatch event
            var dispatchResult = await _eventDispatcher.DispatchAsync([domainEvent], cancellationToken);

            if (dispatchResult.IsSuccess)
            {
                entry.MarkAsCompleted();
                await _repository.UpdateAsync(entry, cancellationToken);

                _logger.LogDebug(
                    "Successfully processed outbox event {EntryId} of type {EventType}",
                    entry.Id, entry.EventType);

                activity?.SetStatus(ActivityStatusCode.Ok);
                return Result.Success();
            }
            else
            {
                // Mark as failed and potentially move to dead letter
                var errorMessage = dispatchResult.Error.Message;
                entry.MarkAsFailed(errorMessage, _options.BaseRetryDelayMinutes);

                // Move to dead letter if max retries exceeded
                if (entry.RetryCount >= _options.MaxRetries)
                {
                    entry.MoveToDeadLetter($"Max retries ({_options.MaxRetries}) exceeded. Last error: {errorMessage}");
                    
                    _logger.LogWarning(
                        "Moved outbox event {EntryId} to dead letter after {RetryCount} retries. Type: {EventType}, Error: {Error}",
                        entry.Id, entry.RetryCount, entry.EventType, errorMessage);
                }
                else
                {
                    _logger.LogWarning(
                        "Failed to process outbox event {EntryId} (attempt {RetryCount}/{MaxRetries}). Type: {EventType}, Error: {Error}. Next retry at: {NextRetryAt}",
                        entry.Id, entry.RetryCount, _options.MaxRetries, entry.EventType, errorMessage, entry.NextRetryAt);
                }

                await _repository.UpdateAsync(entry, cancellationToken);

                activity?.SetStatus(ActivityStatusCode.Error, errorMessage);
                return dispatchResult;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Exception occurred while processing outbox event {EntryId} of type {EventType}",
                entry.Id, entry.EventType);

            try
            {
                // Mark as failed
                entry.MarkAsFailed(ex.Message, _options.BaseRetryDelayMinutes);

                if (entry.RetryCount >= _options.MaxRetries)
                {
                    entry.MoveToDeadLetter($"Max retries exceeded due to exception: {ex.Message}");
                }

                await _repository.UpdateAsync(entry, cancellationToken);
            }
            catch (Exception updateEx)
            {
                _logger.LogError(updateEx,
                    "Failed to update outbox entry {EntryId} status after processing exception",
                    entry.Id);
            }

            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.AddException(ex);

            return Result.Failure(Error.Failure($"Exception processing outbox entry: {ex.Message}", "OUTBOX_PROCESSING_EXCEPTION", ex));
        }
    }
}