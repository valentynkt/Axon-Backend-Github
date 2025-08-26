using BuildingBlocks.Core.Abstractions.Idempotency;

namespace Axon.Modules.Chat.Application.Common.Commands;

/// <summary>
/// Base class for idempotent Chat module commands.
/// Provides default idempotency configuration suitable for AI processing operations.
/// </summary>
/// <typeparam name="TResponse">The type of response returned by the command</typeparam>
public abstract record ChatIdempotentCommand<TResponse> : ChatBaseCommand<TResponse>, IIdempotentCommand<TResponse>
    where TResponse : notnull
{
    /// <summary>
    /// Chat operations require a longer idempotency window due to AI processing time.
    /// Default window is 15 minutes to account for AI response generation delays.
    /// </summary>
    /// <returns>Idempotency window of 15 minutes</returns>
    public virtual TimeSpan? GetIdempotencyWindow() => TimeSpan.FromMinutes(15);

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