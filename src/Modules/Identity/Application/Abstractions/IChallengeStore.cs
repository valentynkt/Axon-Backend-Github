using Axon.Modules.Identity.Domain.ValueObjects;
using CSharpFunctionalExtensions;

namespace Axon.Modules.Identity.Application.Abstractions;

/// <summary>
/// Challenge information bound to specific chain and address for replay protection.
/// </summary>
public sealed record ChallengeInfo(
    ChainId ChainId, 
    Address CanonicalAddress, 
    DateTimeOffset ExpiresAt);

/// <summary>
/// Service for managing authentication challenges and nonces.
/// Provides secure challenge generation, storage, and validation.
/// </summary>
public interface IChallengeStore
{
    /// <summary>
    /// Generates a new challenge for wallet signature verification.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The generated challenge ID</returns>
    Task<Result<string, Error>> GenerateChallengeAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates that a challenge exists and is still valid.
    /// Returns the challenge information including bound chain and address for verification.
    /// </summary>
    /// <param name="challengeId">The challenge ID to validate</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Challenge information if valid, failure if expired or not found</returns>
    Task<Result<ChallengeInfo, Error>> ValidateChallengeAsync(
        string challengeId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Consumes a challenge, marking it as used to prevent replay attacks.
    /// </summary>
    /// <param name="challengeId">The challenge ID to consume</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Success if consumed, failure if already used or not found</returns>
    Task<Result<Unit, Error>> ConsumeChallengeAsync(
        string challengeId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the challenge message that should be signed by the wallet.
    /// </summary>
    /// <param name="challengeId">The challenge ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The message to be signed</returns>
    Task<Result<string, Error>> GetChallengeMessageAsync(
        string challengeId,
        CancellationToken cancellationToken = default);
}