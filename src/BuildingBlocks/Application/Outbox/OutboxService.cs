using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using BuildingBlocks.Core.Domain.Events;
using BuildingBlocks.Core.Functional.Results;
using BuildingBlocks.Application.Events.Dispatching;
using BuildingBlocks.Application.Events.Serialization;
using BuildingBlocks.Application.Events.Enveloping;
using BuildingBlocks.Application.Outbox.Retry;
using BuildingBlocks.Application.Outbox.Monitoring;

namespace BuildingBlocks.Application.Outbox;

/// <summary>
/// Service implementation for managing outbox entries and reliable event publishing.
/// Provides atomic event storage and reliable processing with comprehensive error handling.
/// Uses Application layer DTOs and ports only - no Infrastructure dependencies.
/// </summary>
public sealed class OutboxService : IOutboxService
{
    private readonly IOutboxRepository _repository;
    private readonly IEventDispatcher _eventDispatcher;
    private readonly ILogger<OutboxService> _logger;
    private readonly OutboxOptions _options;
    private readonly IEventSerializer _serializer;
    private readonly IEnvelopeContextAccessor _contextAccessor;
    private readonly IOutboxBackoffPolicy _backoffPolicy;
    private readonly IOutboxMetrics _metrics;

    public OutboxService(
        IOutboxRepository repository,
        IEventDispatcher eventDispatcher,
        ILogger<OutboxService> logger,
        IOptions<OutboxOptions> options,
        IEventSerializer serializer,
        IEnvelopeContextAccessor contextAccessor,
        IOutboxBackoffPolicy backoffPolicy,
        IOutboxMetrics metrics)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _eventDispatcher = eventDispatcher ?? throw new ArgumentNullException(nameof(eventDispatcher));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
        _contextAccessor = contextAccessor ?? throw new ArgumentNullException(nameof(contextAccessor));
        _backoffPolicy = backoffPolicy ?? throw new ArgumentNullException(nameof(backoffPolicy));
        _metrics = metrics ?? throw new ArgumentNullException(nameof(metrics));
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

            var eventsStored = await _repository.AddEventsAsync(
                events,
                transactionId,
                traceId,
                requestId,
                metadata,
                cancellationToken);

            _logger.LogDebug(
                "Stored {EventCount} events in outbox for transaction {TransactionId} (Trace: {TraceId})",
                eventsStored, transactionId, traceId);

            return Result<int>.Success(eventsStored);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to store {EventCount} events in outbox for transaction {TransactionId}",
                events.Count, transactionId);

            return Result<int>.Failure(Error.Persistence(
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

            return Result<OutboxProcessingResult>.Failure(Error.Internal(
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

            // Record fetch metrics
            _metrics.RecordFetched(entries.Count);

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

            // Record processing metrics
            _metrics.RecordProcessed(result.ProcessedCount, result.SuccessfulCount, result.FailedCount, result.MovedToDeadLetterCount, stopwatch.Elapsed);

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

            return Result<OutboxProcessingResult>.Failure(Error.Internal(
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

            return Result<OutboxStatistics>.Failure(Error.Internal(
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

            return Result<OutboxProcessingResult>.Failure(Error.Internal(
                "Failed to retry failed outbox events",
                "OUTBOX_RETRY_FAILED",
                ex));
        }
    }

    public async Task<Result<OutboxProcessingResult>> ReprocessDeadLetterEventsAsync(
        IReadOnlyList<Guid>? entryIds = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var activity = Activity.Current?.Source.StartActivity("ReprocessDeadLetterEvents");

            IReadOnlyList<OutboxEventEntry> entries;

            if (entryIds?.Any() == true)
            {
                entries = await _repository.GetByIdsAsync(entryIds, cancellationToken);
                entries = entries.Where(e => e.Status == OutboxEventStatus.DeadLetter).ToList();
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
            await _repository.ResetDeadLetterToPendingAsync(entryIds, cancellationToken);

            _logger.LogInformation("Reprocessing {EntryCount} dead letter outbox events", entries.Count);

            // Re-fetch entries after reset to get updated status
            entries = entryIds?.Any() == true
                ? await _repository.GetByIdsAsync(entryIds, cancellationToken)
                : await _repository.GetPendingAsync(_options.BatchSize, _options.ProcessingTimeoutMinutes, cancellationToken: cancellationToken);

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

            return Result<OutboxProcessingResult>.Failure(Error.Internal(
                "Failed to reprocess dead letter events",
                "OUTBOX_REPROCESS_FAILED",
                ex));
        }
    }

    private async Task<OutboxProcessingResult> ProcessEntries(
        IReadOnlyList<OutboxEventEntry> entries,
        CancellationToken cancellationToken)
    {
        if (!entries.Any())
        {
            return new OutboxProcessingResult(
                ProcessedCount: 0,
                SuccessfulCount: 0,
                FailedCount: 0,
                MovedToDeadLetterCount: 0,
                ProcessingDuration: TimeSpan.Zero,
                Errors: []);
        }

        // Mark all entries as processing
        await _repository.MarkAsProcessingAsync(entries, cancellationToken);

        var completed = new List<OutboxEventEntry>();
        var failures = new List<OutboxFailureInfo>();

        foreach (var entry in entries)
        {
            try
            {
                // Deserialize event using IEventSerializer
                var eventType = Type.GetType(entry.EventType);
                if (eventType == null)
                {
                    var errorMessage = $"Event type '{entry.EventType}' not found. Assembly may not be loaded.";
                    failures.Add(new OutboxFailureInfo(entry.Id, errorMessage, DateTime.UtcNow));
                    continue;
                }

                var domainEvent = _serializer.Deserialize(entry.EventData, eventType) as IDomainEvent;
                if (domainEvent == null)
                {
                    var errorMessage = $"Failed to deserialize event of type '{entry.EventType}' or result is not IDomainEvent";
                    failures.Add(new OutboxFailureInfo(entry.Id, errorMessage, DateTime.UtcNow));
                    continue;
                }

                // Build context from outbox entry
                var context = new IntegrationEnvelopeContext(
                    TraceId: entry.TraceId,
                    RequestId: entry.RequestId?.ToString(),
                    TenantId: entry.TenantId,
                    Metadata: DeserializeMetadata(entry.Metadata),
                    OutboxEntryId: entry.Id,
                    TransactionId: entry.TransactionId);

                // Push context and dispatch event
                using (_contextAccessor.Push(context))
                {
                    await _eventDispatcher.SendAsync([domainEvent], cancellationToken: cancellationToken);
                }
                
                completed.Add(entry);

                _logger.LogDebug(
                    "Successfully processed outbox event {EntryId} of type {EventType}",
                    entry.Id, entry.EventType);
            }
            catch (Exception ex)
            {
                var errorMessage = $"Exception processing outbox entry: {ex.Message}";
                failures.Add(new OutboxFailureInfo(entry.Id, errorMessage, DateTime.UtcNow));

                _logger.LogError(ex,
                    "Exception occurred while processing outbox event {EntryId} of type {EventType}",
                    entry.Id, entry.EventType);
            }
        }

        // Bulk finalize status updates
        if (completed.Count > 0)
        {
            await _repository.MarkAsCompletedAsync(completed, cancellationToken);
        }

        var movedToDeadLetterCount = 0;
        if (failures.Count > 0)
        {
            // Compute backoff decisions for each failure using the policy
            var computedFailures = new List<OutboxFailureInfo>();
            foreach (var failure in failures)
            {
                var entry = entries.FirstOrDefault(e => e.Id == failure.EntryId);
                if (entry != null)
                {
                    var decision = _backoffPolicy.Compute(entry.RetryCount, failure.FailedAt, _options);
                    var computedFailure = failure with 
                    { 
                        NextRetryAtUtc = decision.NextRetryAtUtc,
                        MoveToDeadLetter = decision.MoveToDeadLetter
                    };
                    computedFailures.Add(computedFailure);

                    // Record metrics for backoff decisions
                    if (decision.MoveToDeadLetter)
                    {
                        _metrics.RecordPermanentFailure();
                        movedToDeadLetterCount++;
                    }
                    else if (decision.NextRetryAtUtc.HasValue)
                    {
                        var delay = decision.NextRetryAtUtc.Value - failure.FailedAt;
                        _metrics.RecordRetryScheduled(delay);
                    }
                }
                else
                {
                    computedFailures.Add(failure);
                }
            }

            await _repository.MarkAsFailedAsync(computedFailures, cancellationToken);
        }

        var errors = failures.Select(f => new OutboxProcessingError(
            f.EntryId,
            entries.FirstOrDefault(e => e.Id == f.EntryId)?.EventType ?? "Unknown",
            f.ErrorMessage,
            f.FailedAt)).ToList();

        return new OutboxProcessingResult(
            ProcessedCount: entries.Count,
            SuccessfulCount: completed.Count,
            FailedCount: failures.Count,
            MovedToDeadLetterCount: movedToDeadLetterCount,
            ProcessingDuration: TimeSpan.Zero, // Will be set by caller
            Errors: errors);
    }

    private static IReadOnlyDictionary<string, object>? DeserializeMetadata(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try
        {
            return System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(json);
        }
        catch 
        { 
            return null; 
        }
    }
}