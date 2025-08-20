// /BuildingBlocks/Core/Abstractions/Idempotency/IIdempotentCommand.cs

using BuildingBlocks.Core.Abstractions.CQRS;

namespace BuildingBlocks.Core.Abstractions.Idempotency;

/// <summary>Marker for commands that should be idempotent.</summary>
public interface IIdempotentCommand
{
    /// <summary>Explicit key provided by caller (e.g., API header). Null → derive automatically.</summary>
    string? GetExplicitIdempotencyKey() => null;

    /// <summary>Override cache window per request. Null → options.DefaultWindow.</summary>
    TimeSpan? GetIdempotencyWindow() => null;
}

/// <summary>Typed idempotent command.</summary>
public interface IIdempotentCommand<TResponse> : IIdempotentCommand, ICommand<TResponse>
    where TResponse : notnull
{ }