using Axon.Modules.Identity.Domain.ValueObjects;
using CSharpFunctionalExtensions;

namespace Axon.Modules.Identity.Application.Abstractions;

/// <summary>
/// Service for verifying cryptographic signatures from wallets.
/// Abstracts the complexity of different blockchain signature schemes.
/// </summary>
public interface ISignatureVerifier
{
    /// <summary>
    /// Verifies a signature against a challenge for a given wallet address.
    /// </summary>
    /// <param name="chainId">The blockchain chain identifier</param>
    /// <param name="walletAddress">The wallet address that supposedly signed the challenge</param>
    /// <param name="signature">The signature to verify</param>
    /// <param name="challengeId">The challenge ID that was signed</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Success if signature is valid, failure with error details if not</returns>
    Task<Result<Unit, Error>> VerifySignatureAsync(
        ChainId chainId,
        Address walletAddress,
        string signature,
        string challengeId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if the signature verifier supports the specified chain.
    /// </summary>
    /// <param name="chainId">The chain to check support for</param>
    /// <returns>True if the chain is supported</returns>
    bool SupportsChain(ChainId chainId);
}