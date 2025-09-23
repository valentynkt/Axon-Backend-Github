using System.Text;
using Microsoft.Extensions.Logging;
using NSec.Cryptography;
using SimpleBase;

namespace Axon.Modules.Identity.Infrastructure.Services;

/// <summary>
/// Minimal signature verifier - 20 lines vs 200+ in the original
/// Only supports Solana Ed25519 signatures for now
/// </summary>
public sealed class SimpleSignatureVerifier
{
    private readonly ILogger<SimpleSignatureVerifier> _logger;

    public SimpleSignatureVerifier(ILogger<SimpleSignatureVerifier> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Verify Ed25519 signature for Solana wallets
    /// Simplified from the complex custom implementation
    /// </summary>
    public bool VerifySignature(string address, string message, string signature)
    {
        try
        {
            // Decode Base58 encoded public key (Solana wallet address)
            var publicKeyBytes = Base58.Bitcoin.Decode(address);

            // Decode Base58 encoded signature
            var signatureBytes = Base58.Bitcoin.Decode(signature);

            // Convert message to bytes
            var messageBytes = Encoding.UTF8.GetBytes(message);
            // Use NSec to verify Ed25519 signature
            var algorithm = SignatureAlgorithm.Ed25519;
            var publicKey = PublicKey.Import(algorithm, publicKeyBytes, KeyBlobFormat.RawPublicKey);
            var isValid = algorithm.Verify(publicKey, messageBytes, signatureBytes);

            _logger.LogDebug("Signature verification result: {IsValid} for address {Address}", isValid, address);
            return isValid;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Signature verification failed for address {Address}", address);
            return false;
        }
    }
}