using BuildingBlocks.Core.Abstractions.Authentication;

namespace BuildingBlocks.Core.Abstractions.Idempotency;

/// <summary>
/// Provides idempotency key generation strategies for specific command types.
/// This allows each module to define how keys should be generated based on command data.
/// </summary>
/// <typeparam name="TCommand">The type of command for which to generate keys.</typeparam>
public interface IIdempotencyKeyProvider<TCommand>
    where TCommand : IIdempotentCommand
{
    /// <summary>
    /// Generates an idempotency key for the specified command.
    /// The key should be deterministic and unique per logical operation.
    /// </summary>
    /// <param name="command">The command to generate a key for.</param>
    /// <param name="currentUser">The current user context for user-specific keys.</param>
    /// <returns>A unique idempotency key for the command.</returns>
    string GenerateKey(TCommand command, ICurrentUserService currentUser);
}