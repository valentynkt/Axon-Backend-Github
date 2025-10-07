using BuildingBlocks.Core.Diagnostics.Errors;
using CSharpFunctionalExtensions;

namespace Axon.Modules.Identity.Application.Contracts.Services;

/// <summary>
/// Service for validating wallet authentication challenge messages.
/// Handles JSON parsing, TTL checking, and replay protection.
/// </summary>
public interface IChallengeValidationService
{
    /// <summary>
    /// Validates a wallet authentication challenge message for structure, TTL, and replay protection.
    /// </summary>
    /// <param name="signedMessage">The JSON challenge message that was signed by the wallet</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Success if challenge is valid, Failure with validation error otherwise</returns>
    /// <remarks>
    /// This method performs the following validations:
    /// 1. JSON structure parsing and format validation
    /// 2. Challenge TTL validation via IChallengeService
    ///
    /// The signedMessage must contain: chain_id, wallet_address, aud (audience), nonce, exp, issued_at
    /// </remarks>
    Task<Result<bool, Error>> ValidateWalletChallengeAsync(
        string signedMessage,
        CancellationToken cancellationToken = default);
}