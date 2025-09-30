using BuildingBlocks.Core.Diagnostics.Errors;
using CSharpFunctionalExtensions;

namespace Axon.Modules.Identity.Application.Contracts.Services;

/// <summary>
/// Service for validating wallet authentication challenge messages.
/// Handles JSON parsing, MAC validation, TTL checking, and replay protection.
/// </summary>
public interface IChallengeValidationService
{
    /// <summary>
    /// Validates a wallet authentication challenge message for structure, MAC, TTL, and replay protection.
    /// </summary>
    /// <param name="signedMessage">The JSON challenge message that was signed by the wallet</param>
    /// <param name="mkv">MAC key version (e.g., "v1")</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Success if challenge is valid, Failure with validation error otherwise</returns>
    /// <remarks>
    /// This method performs the following validations:
    /// 1. JSON structure parsing and format validation
    /// 2. Challenge MAC and TTL validation via IChallengeService
    /// 3. Nonce replay protection (check and mark nonce as used)
    ///
    /// The signedMessage must contain: chain_id, wallet_address, aud (audience), nonce, exp, issued_at
    /// </remarks>
    Task<Result<bool, Error>> ValidateWalletChallengeAsync(
        string signedMessage,
        string mkv,
        CancellationToken cancellationToken = default);
}