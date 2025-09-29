using Axon.Modules.Identity.Application.Common;
using BuildingBlocks.Core.Diagnostics.Errors;
using CSharpFunctionalExtensions;

namespace Axon.Modules.Identity.Application.Contracts.Services;

/// <summary>
/// Service for authentication challenge generation and validation
/// </summary>
public interface IChallengeService
{
    /// <summary>
    /// Generates an authentication challenge for wallet signing
    /// </summary>
    Task<Result<AuthenticationChallenge, Error>> GenerateChallengeAsync(
        string chainId,
        string walletAddress,
        string audience,
        CancellationToken cancellationToken = default);


    /// <summary>
    /// Validates a challenge message structure and TTL
    /// </summary>
    Result<bool, Error> ValidateChallenge(
        string message,
        string expectedChainId,
        string expectedAddress,
        string expectedAudience);

    /// <summary>
    /// Checks and marks a nonce as used for replay protection
    /// </summary>
    Task<UnitResult<Error>> CheckAndMarkNonceUsedAsync(
        string signedMessage,
        string mkv,
        CancellationToken cancellationToken = default);
}