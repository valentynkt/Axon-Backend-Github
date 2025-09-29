using System.Text;
using Axon.Modules.Identity.Application.Common;
using Axon.Modules.Identity.Application.Contracts.Services;
using BuildingBlocks.Core.Diagnostics.Errors;
using CSharpFunctionalExtensions;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using NSec.Cryptography;
using SimpleBase;

namespace Axon.Modules.Identity.Infrastructure.Services;

/// <summary>
/// Ed25519 signature verifier implementation for Solana wallet authentication
/// </summary>
public sealed class Ed25519SignatureVerifier : IWalletSignatureVerifier
{
    private readonly ILogger<Ed25519SignatureVerifier> _logger;

    public Ed25519SignatureVerifier(ILogger<Ed25519SignatureVerifier> logger)
    {
        _logger = logger;
    }

    public Result<bool, Error> VerifySignature(
        string chainId,
        string address,
        string message,
        string signature)
    {
        try
        {
            // Input validation
            if (string.IsNullOrWhiteSpace(chainId))
            {
                return Result.Failure<bool, Error>(
                    Error.Validation("Chain ID cannot be empty", AuthErrors.ValidationError));
            }

            if (string.IsNullOrWhiteSpace(address))
            {
                return Result.Failure<bool, Error>(
                    Error.Validation("Address cannot be empty", AuthErrors.SignatureInvalidAddress));
            }

            if (string.IsNullOrWhiteSpace(message))
            {
                return Result.Failure<bool, Error>(
                    Error.Validation("Message cannot be empty", AuthErrors.ValidationError));
            }

            if (string.IsNullOrWhiteSpace(signature))
            {
                return Result.Failure<bool, Error>(
                    Error.Validation("Signature cannot be empty", AuthErrors.SignatureInvalidSignature));
            }

            // Currently only support Solana
            if (!string.Equals(chainId, "solana", StringComparison.OrdinalIgnoreCase))
            {
                return Result.Failure<bool, Error>(
                    Error.NotSupported($"Chain '{chainId}' not supported. Only 'solana' is supported in V1",
                        AuthErrors.SignatureUnsupportedChain));
            }

            // Decode Solana address (Base58) to 32-byte public key
            byte[] publicKeyBytes;
            try
            {
                publicKeyBytes = Base58.Bitcoin.Decode(address);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to decode Solana address as Base58");
                return Result.Failure<bool, Error>(
                    Error.Validation("Invalid Solana address format", AuthErrors.SignatureInvalidAddress));
            }

            if (publicKeyBytes.Length != 32)
            {
                return Result.Failure<bool, Error>(
                    Error.Validation($"Invalid Solana public key length: {publicKeyBytes.Length} bytes (expected 32)",
                        AuthErrors.SignatureInvalidKeyLength));
            }

            // Auto-detect and decode signature
            byte[] signatureBytes;
            try
            {
                signatureBytes = DecodeSignature(signature);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to decode signature");
                return Result.Failure<bool, Error>(
                    Error.Validation("Invalid signature format", AuthErrors.SignatureInvalidSignature));
            }

            if (signatureBytes.Length != 64)
            {
                return Result.Failure<bool, Error>(
                    Error.Validation($"Invalid signature length: {signatureBytes.Length} bytes (expected 64)",
                        AuthErrors.SignatureInvalidLength));
            }

            // Import public key using NSec
            PublicKey publicKey;
            try
            {
                publicKey = PublicKey.Import(
                    SignatureAlgorithm.Ed25519,
                    publicKeyBytes,
                    KeyBlobFormat.RawPublicKey);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to import Ed25519 public key");
                return Result.Failure<bool, Error>(
                    Error.Validation("Invalid public key format", AuthErrors.SignatureInvalidPublicKey));
            }

            // Verify signature on exact UTF-8 bytes (no BOM, no normalization)
            var messageBytes = Encoding.UTF8.GetBytes(message);
            var isValid = SignatureAlgorithm.Ed25519.Verify(publicKey, messageBytes, signatureBytes);

            if (!isValid)
            {
                var addressHint = GetAddressHint(address);
                _logger.LogWarning("Ed25519 signature verification failed for address {AddressHint}", addressHint);
                return Result.Failure<bool, Error>(
                    Error.Unauthorized("Invalid signature", AuthErrors.SignatureInvalidSignature));
            }

            var addrHint = GetAddressHint(address);
            _logger.LogDebug("Ed25519 signature verified successfully for address {AddressHint}", addrHint);
            return Result.Success<bool, Error>(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during signature verification");
            return Result.Failure<bool, Error>(
                Error.Internal("Signature verification failed", AuthErrors.SignatureVerificationError));
        }
    }

    /// <summary>
    /// Auto-detect signature encoding and decode
    /// </summary>
    private static byte[] DecodeSignature(string signature)
    {
        // Try Base58 first (common for Solana)
        if (TryDecodeBase58(signature, out var base58Bytes))
            return base58Bytes;

        // Then Base64 (strict)
        if (TryDecodeBase64(signature, out var base64Bytes))
            return base64Bytes;

        // Then Base64Url
        try
        {
            return Base64UrlEncoder.DecodeBytes(signature);
        }
        catch
        {
            throw new FormatException("Unsupported signature encoding. Supported formats: Base58, Base64, Base64Url");
        }
    }

    /// <summary>
    /// Try to decode as Base58 using Bitcoin alphabet
    /// </summary>
    private static bool TryDecodeBase58(string input, out byte[] bytes)
    {
        try
        {
            bytes = Base58.Bitcoin.Decode(input);
            return true;
        }
        catch
        {
            bytes = Array.Empty<byte>();
            return false;
        }
    }

    /// <summary>
    /// Try to decode as standard Base64
    /// </summary>
    private static bool TryDecodeBase64(string input, out byte[] bytes)
    {
        try
        {
            bytes = Convert.FromBase64String(input);
            return true;
        }
        catch
        {
            bytes = Array.Empty<byte>();
            return false;
        }
    }

    /// <summary>
    /// Get address hint for logging (only first 8 characters for privacy)
    /// </summary>
    private static string GetAddressHint(string address)
    {
        if (string.IsNullOrEmpty(address))
            return "empty";

        return address.Length > 8 ? address[..8] + "..." : address;
    }
}