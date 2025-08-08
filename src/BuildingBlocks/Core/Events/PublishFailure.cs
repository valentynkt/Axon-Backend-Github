using BuildingBlocks.Core.Abstractions.Events;
using BuildingBlocks.Core.Diagnostics.Errors;

namespace BuildingBlocks.Core.Events;

/// <summary>
/// Represents a failed attempt to publish an integration event to an external message broker.
/// Contains comprehensive error information and context for debugging and retry logic.
/// </summary>
/// <param name="EventId">Unique identifier of the integration event that failed to publish</param>
/// <param name="EventType">Type name of the integration event for identification</param>
/// <param name="Error">Detailed error information about the publish failure</param>
/// <param name="AttemptedAtUtc">UTC timestamp when the publish attempt was made</param>
/// <param name="AttemptNumber">Which attempt this was (1-based) if retries were configured</param>
/// <param name="Destination">The target destination where publishing was attempted</param>
/// <param name="CanRetry">Whether this failure is considered retryable</param>
/// <param name="RetryAfter">Suggested delay before retry attempt, if applicable</param>
public sealed record PublishFailure(
    Guid EventId,
    string EventType,
    Error Error,
    DateTime AttemptedAtUtc,
    int AttemptNumber = 1,
    string? Destination = null,
    bool CanRetry = true,
    TimeSpan? RetryAfter = null)
{
    /// <summary>
    /// Creates a PublishFailure from an integration event and error.
    /// </summary>
    public static PublishFailure Create(
        IIntegrationEvent integrationEvent,
        Error error,
        int attemptNumber = 1,
        string? destination = null,
        bool canRetry = true,
        TimeSpan? retryAfter = null,
        DateTime? attemptedAtUtc = null)
    {
        return new PublishFailure(
            EventId: integrationEvent.EventId,
            EventType: integrationEvent.EventType,
            Error: error,
            AttemptedAtUtc: attemptedAtUtc ?? DateTime.UtcNow,
            AttemptNumber: attemptNumber,
            Destination: destination,
            CanRetry: canRetry,
            RetryAfter: retryAfter);
    }

    /// <summary>
    /// Creates a non-retryable PublishFailure.
    /// </summary>
    public static PublishFailure NonRetryable(
        IIntegrationEvent integrationEvent,
        Error error,
        int attemptNumber = 1,
        string? destination = null,
        DateTime? attemptedAtUtc = null)
    {
        return Create(
            integrationEvent: integrationEvent,
            error: error,
            attemptNumber: attemptNumber,
            destination: destination,
            canRetry: false,
            retryAfter: null,
            attemptedAtUtc: attemptedAtUtc);
    }

    /// <summary>
    /// Creates a retryable PublishFailure with a suggested retry delay.
    /// </summary>
    public static PublishFailure Retryable(
        IIntegrationEvent integrationEvent,
        Error error,
        TimeSpan retryAfter,
        int attemptNumber = 1,
        string? destination = null,
        DateTime? attemptedAtUtc = null)
    {
        return Create(
            integrationEvent: integrationEvent,
            error: error,
            attemptNumber: attemptNumber,
            destination: destination,
            canRetry: true,
            retryAfter: retryAfter,
            attemptedAtUtc: attemptedAtUtc);
    }

    /// <summary>
    /// Creates a network-related PublishFailure.
    /// </summary>
    public static PublishFailure NetworkFailure(
        IIntegrationEvent integrationEvent,
        string errorMessage,
        Exception? exception = null,
        int attemptNumber = 1,
        string? destination = null,
        DateTime? attemptedAtUtc = null)
    {
        var error = Error.Network(
            message: errorMessage,
            code: "PUBLISH_NETWORK_ERROR",
            exception: exception);

        return Create(
            integrationEvent: integrationEvent,
            error: error,
            attemptNumber: attemptNumber,
            destination: destination,
            canRetry: true,
            retryAfter: TimeSpan.FromSeconds(30),
            attemptedAtUtc: attemptedAtUtc);
    }

    /// <summary>
    /// Creates a timeout-related PublishFailure.
    /// </summary>
    public static PublishFailure TimeoutFailure(
        IIntegrationEvent integrationEvent,
        TimeSpan timeout,
        int attemptNumber = 1,
        string? destination = null,
        DateTime? attemptedAtUtc = null)
    {
        var error = Error.Timeout(
            message: $"Publishing timed out after {timeout.TotalSeconds} seconds",
            code: "PUBLISH_TIMEOUT",
            timeout: timeout);

        return Create(
            integrationEvent: integrationEvent,
            error: error,
            attemptNumber: attemptNumber,
            destination: destination,
            canRetry: true,
            retryAfter: TimeSpan.FromSeconds(60),
            attemptedAtUtc: attemptedAtUtc);
    }

    /// <summary>
    /// Creates a serialization-related PublishFailure (non-retryable).
    /// </summary>
    public static PublishFailure SerializationFailure(
        IIntegrationEvent integrationEvent,
        Exception exception,
        int attemptNumber = 1,
        string? destination = null,
        DateTime? attemptedAtUtc = null)
    {
        var error = Error.Internal(
            message: $"Failed to serialize event for publishing: {exception.Message}",
            code: "PUBLISH_SERIALIZATION_ERROR",
            exception: exception);

        return NonRetryable(
            integrationEvent: integrationEvent,
            error: error,
            attemptNumber: attemptNumber,
            destination: destination,
            attemptedAtUtc: attemptedAtUtc);
    }

    /// <summary>
    /// Creates an authorization-related PublishFailure (non-retryable).
    /// </summary>
    public static PublishFailure AuthorizationFailure(
        IIntegrationEvent integrationEvent,
        string errorMessage,
        int attemptNumber = 1,
        string? destination = null,
        DateTime? attemptedAtUtc = null)
    {
        var error = Error.Forbidden(
            message: errorMessage,
            code: "PUBLISH_AUTHORIZATION_ERROR");

        return NonRetryable(
            integrationEvent: integrationEvent,
            error: error,
            attemptNumber: attemptNumber,
            destination: destination,
            attemptedAtUtc: attemptedAtUtc);
    }
}