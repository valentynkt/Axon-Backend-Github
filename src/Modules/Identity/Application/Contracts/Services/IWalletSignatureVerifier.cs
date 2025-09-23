using BuildingBlocks.Core.Diagnostics.Errors;
using CSharpFunctionalExtensions;

namespace Axon.Modules.Identity.Application.Contracts.Services;

/// <summary>
/// Service for verifying cryptographic signatures from blockchain wallets
/// </summary>
public interface IWalletSignatureVerifier
{
    /// <summary>
    /// Verifies a cryptographic signature for a given message
    /// </summary>
    /// <param name="chainId">Blockchain identifier (e.g., "solana", "ethereum")</param>
    /// <param name="address">Wallet address (public key representation)</param>
    /// <param name="message">Original message that was signed (UTF-8 string)</param>
    /// <param name="signature">Cryptographic signature (Base58 or Base64 encoded)</param>
    /// <returns>Success if valid, Failure(Unauthorized) if invalid</returns>
    Result<bool, Error> VerifySignature(
        string chainId,
        string address,
        string message,
        string signature);
}