using BuildingBlocks.Core.Diagnostics.Errors;

namespace BuildingBlocks.Core.Events;

/// <summary>
/// Specialized error factory for external event publishing operations.
/// Provides domain-specific error creation methods with appropriate categorization and metadata.
/// </summary>
public static class ExternalPublishingErrors
{
    #region Connection Errors

    /// <summary>
    /// Creates an error for message broker connection failures.
    /// </summary>
    public static Error BrokerConnectionFailed(
        string brokerUrl,
        Exception? exception = null,
        string? correlationId = null)
    {
        var metadata = new Dictionary<string, object>
        {
            ["BrokerUrl"] = brokerUrl,
            ["ErrorCategory"] = "Connection"
        };

        return Error.Network(
                message: $"Failed to connect to message broker at {brokerUrl}",
                code: "BROKER_CONNECTION_FAILED",
                exception: exception,
                metadata: metadata)
            .WithCorrelationId(correlationId ?? Guid.NewGuid().ToString());
    }

    /// <summary>
    /// Creates an error for authentication failures with the message broker.
    /// </summary>
    public static Error BrokerAuthenticationFailed(
        string brokerUrl,
        string? username = null,
        string? correlationId = null)
    {
        var metadata = new Dictionary<string, object>
        {
            ["BrokerUrl"] = brokerUrl,
            ["ErrorCategory"] = "Authentication"
        };

        if (!string.IsNullOrEmpty(username))
            metadata["Username"] = username;

        return Error.Unauthorized(
                message: $"Authentication failed for message broker at {brokerUrl}",
                code: "BROKER_AUTH_FAILED",
                metadata: metadata)
            .WithCorrelationId(correlationId ?? Guid.NewGuid().ToString());
    }

    /// <summary>
    /// Creates an error for authorization failures (insufficient permissions).
    /// </summary>
    public static Error InsufficientBrokerPermissions(
        string destination,
        string operation = "publish",
        string? correlationId = null)
    {
        var metadata = new Dictionary<string, object>
        {
            ["Destination"] = destination,
            ["Operation"] = operation,
            ["ErrorCategory"] = "Authorization"
        };

        return Error.Forbidden(
                message: $"Insufficient permissions to {operation} to destination '{destination}'",
                code: "BROKER_INSUFFICIENT_PERMISSIONS",
                metadata: metadata)
            .WithCorrelationId(correlationId ?? Guid.NewGuid().ToString());
    }

    #endregion

    #region Publishing Errors

    /// <summary>
    /// Creates an error for message size limit exceeded.
    /// </summary>
    public static Error MessageSizeExceeded(
        long messageSize,
        long maxSize,
        string eventType,
        string? correlationId = null)
    {
        var metadata = new Dictionary<string, object>
        {
            ["MessageSize"] = messageSize,
            ["MaxAllowedSize"] = maxSize,
            ["EventType"] = eventType,
            ["ErrorCategory"] = "MessageSize"
        };

        return Error.Validation(
                message: $"Message size {messageSize} bytes exceeds maximum allowed size of {maxSize} bytes for event type '{eventType}'",
                code: "MESSAGE_SIZE_EXCEEDED",
                metadata: metadata)
            .WithCorrelationId(correlationId ?? Guid.NewGuid().ToString());
    }

    /// <summary>
    /// Creates an error for destination not found or invalid.
    /// </summary>
    public static Error DestinationNotFound(
        string destination,
        string? correlationId = null)
    {
        var metadata = new Dictionary<string, object>
        {
            ["Destination"] = destination,
            ["ErrorCategory"] = "Routing"
        };

        return Error.NotFound(
                message: $"Destination '{destination}' does not exist or is not accessible",
                code: "DESTINATION_NOT_FOUND",
                metadata: metadata)
            .WithCorrelationId(correlationId ?? Guid.NewGuid().ToString());
    }

    /// <summary>
    /// Creates an error for message broker capacity exceeded.
    /// </summary>
    public static Error BrokerCapacityExceeded(
        string destination,
        string? correlationId = null)
    {
        var metadata = new Dictionary<string, object>
        {
            ["Destination"] = destination,
            ["ErrorCategory"] = "Capacity"
        };

        return Error.External(
                message: $"Message broker capacity exceeded for destination '{destination}'",
                code: "BROKER_CAPACITY_EXCEEDED",
                metadata: metadata)
            .WithCorrelationId(correlationId ?? Guid.NewGuid().ToString());
    }

    /// <summary>
    /// Creates an error for rate limit exceeded.
    /// </summary>
    public static Error PublishRateLimitExceeded(
        int currentRate,
        int maxRate,
        TimeSpan retryAfter,
        string? correlationId = null)
    {
        var metadata = new Dictionary<string, object>
        {
            ["CurrentRate"] = currentRate,
            ["MaxAllowedRate"] = maxRate,
            ["ErrorCategory"] = "RateLimit"
        };

        return Error.RateLimit(
                message: $"Publish rate limit exceeded: {currentRate}/s > {maxRate}/s",
                code: "PUBLISH_RATE_LIMIT_EXCEEDED",
                retryAfter: retryAfter,
                metadata: metadata)
            .WithCorrelationId(correlationId ?? Guid.NewGuid().ToString());
    }

    #endregion

    #region Serialization Errors

    /// <summary>
    /// Creates an error for event serialization failures.
    /// </summary>
    public static Error SerializationFailed(
        string eventType,
        Exception exception,
        string? correlationId = null)
    {
        var metadata = new Dictionary<string, object>
        {
            ["EventType"] = eventType,
            ["ErrorCategory"] = "Serialization",
            ["SerializationError"] = exception.Message
        };

        return Error.Internal(
                message: $"Failed to serialize event of type '{eventType}': {exception.Message}",
                code: "EVENT_SERIALIZATION_FAILED",
                exception: exception,
                metadata: metadata)
            .WithCorrelationId(correlationId ?? Guid.NewGuid().ToString());
    }

    /// <summary>
    /// Creates an error for unsupported event type.
    /// </summary>
    public static Error UnsupportedEventType(
        string eventType,
        string? correlationId = null)
    {
        var metadata = new Dictionary<string, object>
        {
            ["EventType"] = eventType,
            ["ErrorCategory"] = "Serialization"
        };

        return Error.Validation(
                message: $"Event type '{eventType}' is not supported for external publishing",
                code: "UNSUPPORTED_EVENT_TYPE",
                metadata: metadata)
            .WithCorrelationId(correlationId ?? Guid.NewGuid().ToString());
    }

    #endregion

    #region Timeout and Reliability Errors

    /// <summary>
    /// Creates an error for publish operation timeout.
    /// </summary>
    public static Error PublishTimeout(
        TimeSpan timeout,
        string eventType,
        string? destination = null,
        string? correlationId = null)
    {
        var metadata = new Dictionary<string, object>
        {
            ["EventType"] = eventType,
            ["TimeoutDuration"] = timeout.ToString(),
            ["ErrorCategory"] = "Timeout"
        };

        if (!string.IsNullOrEmpty(destination))
            metadata["Destination"] = destination;

        return Error.Timeout(
                message: $"Publish operation for event type '{eventType}' timed out after {timeout.TotalSeconds} seconds",
                code: "PUBLISH_TIMEOUT",
                timeout: timeout,
                metadata: metadata)
            .WithCorrelationId(correlationId ?? Guid.NewGuid().ToString());
    }

    /// <summary>
    /// Creates an error for delivery confirmation timeout.
    /// </summary>
    public static Error DeliveryConfirmationTimeout(
        TimeSpan timeout,
        string messageId,
        string? correlationId = null)
    {
        var metadata = new Dictionary<string, object>
        {
            ["MessageId"] = messageId,
            ["TimeoutDuration"] = timeout.ToString(),
            ["ErrorCategory"] = "DeliveryConfirmation"
        };

        return Error.Timeout(
                message: $"Delivery confirmation timeout for message {messageId} after {timeout.TotalSeconds} seconds",
                code: "DELIVERY_CONFIRMATION_TIMEOUT",
                timeout: timeout,
                metadata: metadata)
            .WithCorrelationId(correlationId ?? Guid.NewGuid().ToString());
    }

    /// <summary>
    /// Creates an error for message delivery failure (negative acknowledgment).
    /// </summary>
    public static Error MessageDeliveryFailed(
        string messageId,
        string reason,
        string? correlationId = null)
    {
        var metadata = new Dictionary<string, object>
        {
            ["MessageId"] = messageId,
            ["FailureReason"] = reason,
            ["ErrorCategory"] = "DeliveryFailure"
        };

        return Error.External(
                message: $"Message {messageId} delivery failed: {reason}",
                code: "MESSAGE_DELIVERY_FAILED",
                metadata: metadata)
            .WithCorrelationId(correlationId ?? Guid.NewGuid().ToString());
    }

    #endregion

    #region Batch Operation Errors

    /// <summary>
    /// Creates an error for batch operation size exceeded.
    /// </summary>
    public static Error BatchSizeExceeded(
        int batchSize,
        int maxBatchSize,
        string? correlationId = null)
    {
        var metadata = new Dictionary<string, object>
        {
            ["BatchSize"] = batchSize,
            ["MaxBatchSize"] = maxBatchSize,
            ["ErrorCategory"] = "BatchSize"
        };

        return Error.Validation(
                message: $"Batch size {batchSize} exceeds maximum allowed batch size of {maxBatchSize}",
                code: "BATCH_SIZE_EXCEEDED",
                metadata: metadata)
            .WithCorrelationId(correlationId ?? Guid.NewGuid().ToString());
    }

    /// <summary>
    /// Creates an error for partial batch failure.
    /// </summary>
    public static Error PartialBatchFailure(
        int successCount,
        int failureCount,
        string? correlationId = null)
    {
        var metadata = new Dictionary<string, object>
        {
            ["SuccessCount"] = successCount,
            ["FailureCount"] = failureCount,
            ["TotalCount"] = successCount + failureCount,
            ["ErrorCategory"] = "PartialBatchFailure"
        };

        return Error.External(
                message: $"Batch operation partially failed: {successCount} succeeded, {failureCount} failed",
                code: "PARTIAL_BATCH_FAILURE",
                metadata: metadata)
            .WithCorrelationId(correlationId ?? Guid.NewGuid().ToString());
    }

    #endregion

    #region Configuration Errors

    /// <summary>
    /// Creates an error for invalid publisher configuration.
    /// </summary>
    public static Error InvalidPublisherConfiguration(
        string configurationKey,
        string reason,
        string? correlationId = null)
    {
        var metadata = new Dictionary<string, object>
        {
            ["ConfigurationKey"] = configurationKey,
            ["ErrorCategory"] = "Configuration"
        };

        return Error.Configuration(
                message: $"Invalid publisher configuration for '{configurationKey}': {reason}",
                code: "INVALID_PUBLISHER_CONFIG",
                configKey: configurationKey,
                metadata: metadata)
            .WithCorrelationId(correlationId ?? Guid.NewGuid().ToString());
    }

    /// <summary>
    /// Creates an error for missing required configuration.
    /// </summary>
    public static Error MissingRequiredConfiguration(
        string configurationKey,
        string? correlationId = null)
    {
        var metadata = new Dictionary<string, object>
        {
            ["ConfigurationKey"] = configurationKey,
            ["ErrorCategory"] = "Configuration"
        };

        return Error.Configuration(
                message: $"Required configuration '{configurationKey}' is missing",
                code: "MISSING_REQUIRED_CONFIG",
                configKey: configurationKey,
                metadata: metadata)
            .WithCorrelationId(correlationId ?? Guid.NewGuid().ToString());
    }

    #endregion
}