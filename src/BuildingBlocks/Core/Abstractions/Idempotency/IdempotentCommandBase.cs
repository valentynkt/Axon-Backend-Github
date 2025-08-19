using BuildingBlocks.Core.Abstractions.CQRS;

namespace BuildingBlocks.Core.Abstractions.Idempotency;

/// <summary>
/// Base class for commands that support idempotency.
/// Provides default implementations for idempotency configuration
/// while allowing derived classes to override behavior as needed.
/// </summary>
/// <typeparam name="TResponse">The type of response returned by the command.</typeparam>
public abstract record IdempotentCommandBase<TResponse> : CommandBase<TResponse>, IIdempotentCommand<TResponse>
    where TResponse : notnull
{
    /// <summary>
    /// Gets an explicit idempotency key if provided by the caller.
    /// Default implementation returns null, indicating that the key should be auto-generated.
    /// </summary>
    /// <returns>The explicit idempotency key or null for auto-generation.</returns>
    public virtual string? GetExplicitIdempotencyKey() => null;

    /// <summary>
    /// Gets the time window during which the command result will be cached.
    /// Default implementation uses the standard window from IdempotencyDefaults.
    /// </summary>
    /// <returns>The idempotency window duration.</returns>
    public virtual TimeSpan GetIdempotencyWindow() => IdempotencyDefaults.Window;
}