namespace BuildingBlocks.Application.Events.Consumption.Inbox;

/// <summary>
/// No-operation implementation of IInboxStore.
/// Always allows processing to begin - provides no idempotency guarantees.
/// Suitable for development scenarios or when idempotency is handled at the transport layer.
/// </summary>
public sealed class NoOpInboxStore : IInboxStore
{
    /// <summary>
    /// Always returns Begun to allow processing.
    /// </summary>
    public Task<InboxStartResult> TryBeginAsync(
        string idempotencyKey, 
        DateTime producedAtUtc, 
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(InboxStartResult.Begun);
    }

    /// <summary>
    /// No-op completion tracking.
    /// </summary>
    public Task CompleteAsync(string idempotencyKey, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    /// <summary>
    /// No-op failure tracking.
    /// </summary>
    public Task FailAsync(string idempotencyKey, string errorMessage, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }
}