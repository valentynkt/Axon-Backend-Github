using BuildingBlocks.Core.Abstractions.Idempotency;

namespace Axon.Modules.Identity.Application.Common.Commands;

/// <summary>
/// Base class for idempotent Identity module commands.
/// Provides default idempotency configuration suitable for identity operations.
/// </summary>
/// <typeparam name="TResponse">The type of response returned by the command</typeparam>
public abstract record IdentityIdempotentCommand<TResponse> : IdentityBaseCommand<TResponse>, IIdempotentCommand<TResponse>
    where TResponse : notnull
{
    /// <summary>
    /// Identity operations require a shorter idempotency window than chat operations.
    /// Default window is 5 minutes for identity state changes.
    /// </summary>
    /// <returns>Idempotency window of 5 minutes</returns>
    public virtual TimeSpan? GetIdempotencyWindow() => TimeSpan.FromMinutes(5);

    /// <summary>
    /// Provides explicit idempotency key if needed.
    /// Override in derived classes for custom key generation logic.
    /// </summary>
    /// <returns>null for default key generation based on request content</returns>
    public virtual string? GetExplicitIdempotencyKey() => null;

    /// <summary>
    /// Indicates whether to cache failure responses for idempotency.
    /// Default is false - only successful responses are cached.
    /// </summary>
    /// <returns>false - failures are not cached by default</returns>
    public virtual bool CacheFailures => false;
}