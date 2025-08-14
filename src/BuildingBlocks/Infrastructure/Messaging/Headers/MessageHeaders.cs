namespace BuildingBlocks.Infrastructure.Messaging.Headers;

/// <summary>
/// Standard message header keys for correlation and context propagation.
/// These headers flow through the messaging infrastructure for distributed tracing and multi-tenancy.
/// </summary>
public static class MessageHeaders
{
    /// <summary>
    /// Distributed trace identifier for correlating requests across services.
    /// Usually from Activity.Current?.Id or OpenTelemetry TraceId.
    /// </summary>
    public const string TraceId = "trace-id";

    /// <summary>
    /// Unique request identifier for correlating related messages.
    /// Generated per command/request for tracking.
    /// </summary>
    public const string RequestId = "request-id";

    /// <summary>
    /// Tenant identifier for multi-tenant systems.
    /// Used to ensure proper data isolation.
    /// </summary>
    public const string TenantId = "tenant-id";

    /// <summary>
    /// User identifier of the initiating user.
    /// Used for audit and authorization downstream.
    /// </summary>
    public const string UserId = "user-id";

    /// <summary>
    /// Correlation identifier for saga/workflow orchestration.
    /// Used by MassTransit sagas and other stateful orchestrations.
    /// </summary>
    public const string CorrelationId = "correlation-id";

    /// <summary>
    /// Message type for polymorphic deserialization.
    /// Usually set automatically by MassTransit.
    /// </summary>
    public const string MessageType = "message-type";

    /// <summary>
    /// Timestamp when the message was published.
    /// ISO 8601 format.
    /// </summary>
    public const string PublishedAt = "published-at";

    /// <summary>
    /// Source service/application that published the message.
    /// </summary>
    public const string SourceService = "source-service";

    /// <summary>
    /// Version of the message schema/contract.
    /// Used for versioning and backwards compatibility.
    /// </summary>
    public const string MessageVersion = "message-version";

    /// <summary>
    /// Environment from which the message originated (dev, staging, prod).
    /// Useful for debugging cross-environment issues.
    /// </summary>
    public const string Environment = "environment";
}