namespace BuildingBlocks.Application.Events.Enveloping;

/// <summary>
/// Standard header constants for integration event envelopes.
/// Provides consistent metadata keys across the application.
/// All header names use kebab-case for consistent transport formatting.
/// </summary>
public static class IntegrationEventHeaders
{
    // Integration envelope metadata
    public const string IntegrationEventId      = "integration.eventId";
    public const string IntegrationEventType    = "integration.eventType"; 
    public const string IntegrationSchemaVersion = "integration.schemaVersion";
    public const string ProducedAt              = "produced.at";

    // Correlation and causation tracking
    public const string CorrelationId           = "correlation.id";
    public const string CausationId             = "causation.id";   // Domain event ID that caused this
    public const string TraceId                 = "trace.id";
    public const string SpanId                  = "trace.span_id";

    // Tenant and user context
    public const string TenantId                = "tenant.id";
    public const string UserId                  = "user.id";
    public const string UserName                = "user.name";

    // Source and aggregate information
    public const string SourceModule            = "source.module";
    public const string AggregateId             = "aggregate.id";
    public const string AggregateType           = "aggregate.type";

    // Outbox and transaction tracking
    public const string OutboxEntryId           = "outbox.entryId";   // when available
    public const string TransactionId           = "transaction.id";    // when available

    // Idempotency and retry safety
    public const string IdempotencyKey          = "idempotency.key";   // stable retry key
    public const string DeliveryAttempt         = "delivery.attempt";  // current attempt number

    // Legacy constants for backward compatibility
    [Obsolete("Use IntegrationEventId instead")]
    public const string EventId                 = "event.id";
    [Obsolete("Use IntegrationEventType instead")]
    public const string EventType               = "event.type";
    [Obsolete("Use IntegrationSchemaVersion instead")]
    public const string EventVersion            = "event.version";
    [Obsolete("Use ProducedAt instead")]
    public const string OccurredAt              = "event.occurred_at";
}