namespace BuildingBlocks.Application.Events.Consumption.Inbox;

/// <summary>
/// Storage abstraction for inbox pattern to ensure idempotent event processing.
/// Tracks processing state of integration events by their idempotency keys.
/// </summary>
public interface IInboxStore
{
    /// <summary>
    /// Attempt to begin processing an integration event by its idempotency key.
    /// This method should be atomic and handle concurrent access.
    /// </summary>
    /// <param name="idempotencyKey">The idempotency key from the event envelope</param>
    /// <param name="producedAtUtc">When the event was originally produced</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Result indicating whether processing can begin</returns>
    Task<InboxStartResult> TryBeginAsync(
        string idempotencyKey, 
        DateTime producedAtUtc, 
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Mark an integration event as successfully processed.
    /// </summary>
    /// <param name="idempotencyKey">The idempotency key from the event envelope</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Task representing the completion operation</returns>
    Task CompleteAsync(string idempotencyKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// Mark an integration event as failed during processing.
    /// </summary>
    /// <param name="idempotencyKey">The idempotency key from the event envelope</param>
    /// <param name="errorMessage">The error message from the failure</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Task representing the failure operation</returns>
    Task FailAsync(string idempotencyKey, string errorMessage, CancellationToken cancellationToken = default);
}