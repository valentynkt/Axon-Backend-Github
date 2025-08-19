using BuildingBlocks.Core.Abstractions.CQRS;

namespace BuildingBlocks.Core.Abstractions.Idempotency;

/// <summary>
/// Marker interface for commands that support idempotency.
/// Commands implementing this interface will be handled by the IdempotencyBehavior
/// to ensure that duplicate command executions return the same result.
/// </summary>
public interface IIdempotentCommand
{
    /// <summary>
    /// Gets an explicit idempotency key if provided by the caller.
    /// If null, the key will be generated using the registered IIdempotencyKeyProvider.
    /// </summary>
    /// <returns>The explicit idempotency key or null for auto-generation.</returns>
    string? GetExplicitIdempotencyKey();

    /// <summary>
    /// Gets the time window during which the command result will be cached.
    /// After this window expires, the command can be executed again.
    /// </summary>
    /// <returns>The idempotency window duration.</returns>
    TimeSpan GetIdempotencyWindow();
}

/// <summary>
/// Typed interface for idempotent commands with a specific response type.
/// </summary>
/// <typeparam name="TResponse">The type of response returned by the command.</typeparam>
public interface IIdempotentCommand<TResponse> : IIdempotentCommand, ICommand<TResponse>
    where TResponse : notnull
{
}