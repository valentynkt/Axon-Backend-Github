# Story 2: Ed25519 Signature Verification Service

## Overview
Implement a secure Ed25519 signature verification service for Solana wallet authentication using NSec.Cryptography library, with support for automatic encoding detection and extensibility for future blockchain support.

## Success Criteria
- Ed25519 signatures from Solana wallets can be verified
- Automatic detection of Base58/Base64 encoding
- Type-safe implementation using NSec.Cryptography
- Extensible interface for future EVM support
- Comprehensive error handling and logging

## Tasks

### Task 2.1: Install NSec.Cryptography Package
**File:** `src/Modules/Identity/Infrastructure/Axon.Modules.Identity.Infrastructure.csproj`

- [ ] Add NSec.Cryptography NuGet package (v25.4.0 or latest)
- [ ] Verify package compatibility with .NET 10

```xml
<PackageReference Include="NSec.Cryptography" Version="25.4.0" />
```

### Task 2.2: Create Wallet Signature Verifier Interface
**File:** `src/Modules/Identity/Application/Contracts/Services/IWalletSignatureVerifier.cs`

- [ ] Define verification interface
- [ ] Include chain-specific parameters
- [ ] Use Result pattern for error handling

```csharp
namespace Axon.Modules.Identity.Application.Contracts.Services;

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
```

### Task 2.3: Implement Ed25519 Signature Verifier
**File:** `src/Modules/Identity/Infrastructure/Services/Ed25519SignatureVerifier.cs`

- [ ] Implement IWalletSignatureVerifier for Solana
- [ ] Add Base58 decoding support
- [ ] Auto-detect signature encoding
- [ ] Validate Solana address format (32 bytes)
- [ ] Use NSec for cryptographic operations

```csharp
using NSec.Cryptography;
using SimpleBase;
using Microsoft.IdentityModel.Tokens;

namespace Axon.Modules.Identity.Infrastructure.Services;

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
            // Currently only support Solana
            if (!string.Equals(chainId, "solana", StringComparison.OrdinalIgnoreCase))
            {
                return Result.Failure<bool, Error>(
                    Error.NotSupported($"Chain '{chainId}' not supported. Only 'solana' is supported in V1"));
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
                    Error.Validation("Invalid Solana address format", "SIGNATURE.INVALID_ADDRESS"));
            }

            if (publicKeyBytes.Length != 32)
            {
                return Result.Failure<bool, Error>(
                    Error.Validation($"Invalid Solana public key length: {publicKeyBytes.Length} bytes (expected 32)",
                        "SIGNATURE.INVALID_KEY_LENGTH"));
            }

            // Auto-detect and decode signature
            byte[] signatureBytes = DecodeSignature(signature);
            if (signatureBytes.Length != 64)
            {
                return Result.Failure<bool, Error>(
                    Error.Validation($"Invalid signature length: {signatureBytes.Length} bytes (expected 64)",
                        "SIGNATURE.INVALID_LENGTH"));
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
                    Error.Validation("Invalid public key format", "SIGNATURE.INVALID_PUBLIC_KEY"));
            }

            // Verify signature on exact UTF-8 bytes (no BOM, no normalization)
            var messageBytes = Encoding.UTF8.GetBytes(message);
            var isValid = SignatureAlgorithm.Ed25519.Verify(publicKey, messageBytes, signatureBytes);

            if (!isValid)
            {
                var addrHint = string.IsNullOrEmpty(address) ? "empty" : (address.Length > 8 ? address[..8] : address);
                _logger.LogWarning("Ed25519 signature verification failed for address {AddrHint}", addrHint);
                return Result.Failure<bool, Error>(
                    Error.Unauthorized("Invalid signature", "SIGNATURE.INVALID_SIGNATURE"));
            }

            var addressHint = string.IsNullOrEmpty(address) ? "empty" : (address.Length > 8 ? address[..8] : address);
            _logger.LogDebug("Ed25519 signature verified successfully for address {AddrHint}", addressHint);
            return Result.Success<bool, Error>(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during signature verification");
            return Result.Failure<bool, Error>(
                Error.Internal("Signature verification failed", "SIGNATURE.VERIFICATION_ERROR"));
        }
    }

    private byte[] DecodeSignature(string signature)
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
            throw new FormatException("Unsupported signature encoding");
        }
    }

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
}
```

### Task 2.4: Create Base58 Helper (if SimpleBase not available)
**File:** `src/BuildingBlocks/Core/Encoding/Base58.cs` (if needed)

- [ ] Add Base58 encoding/decoding utilities
- [ ] Use existing library or implement minimal version

### Task 2.5: Register Service in DI Container
**File:** `src/Modules/Identity/Infrastructure/DependencyInjection/ServiceRegistration.cs`

- [ ] Register IWalletSignatureVerifier as Singleton (stateless, pure verify-only)
- [ ] Wire up Ed25519SignatureVerifier implementation
- [ ] Confirm NSec has no per-request state

```csharp
services.AddSingleton<IWalletSignatureVerifier, Ed25519SignatureVerifier>();
```

### Task 2.6: Create Error Codes
**File:** `src/Modules/Identity/Application/Common/AuthErrors.cs`

- [ ] Add signature-related error codes
- [ ] Maintain consistency with existing error structure

```csharp
public static class SignatureErrors
{
    public const string InvalidAddress = "SIGNATURE.INVALID_ADDRESS";
    public const string InvalidPublicKey = "SIGNATURE.INVALID_PUBLIC_KEY";
    public const string InvalidSignature = "SIGNATURE.INVALID_SIGNATURE";
    public const string InvalidLength = "SIGNATURE.INVALID_LENGTH";
    public const string VerificationFailed = "SIGNATURE.VERIFICATION_FAILED";
    public const string UnsupportedChain = "SIGNATURE.UNSUPPORTED_CHAIN";
}
```

## Testing Requirements

### Unit Tests
**File:** `tests/Modules/Identity/Infrastructure/Services/Ed25519SignatureVerifierTests.cs`

- [ ] Test valid Ed25519 signatures with known test vectors
- [ ] Test invalid signatures
- [ ] Test Base58 encoding detection
- [ ] Test Base64 encoding detection
- [ ] Test invalid address formats
- [ ] Test wrong public key lengths
- [ ] Test unsupported chains

```csharp
[Test]
public void VerifySignature_ValidSolanaSignature_ReturnsSuccess()
{
    // Use known test vectors from Solana
    var address = "9WzDXwBbmkg8ZTbNMqUxvQRAyrZzDsGYdLVL9zYtAWWM";
    var message = "{\"test\":\"message\"}";
    var signature = "base58_encoded_signature";

    var result = _verifier.VerifySignature("solana", address, message, signature);

    result.IsSuccess.Should().BeTrue();
    result.Value.Should().BeTrue();
}

[Test]
public void VerifySignature_InvalidSignature_ReturnsFailure()
{
    // Test with corrupted signature
}

[Test]
public void VerifySignature_WrongMessageContent_ReturnsInvalid()
{
    // Test signature doesn't match different message
}

[Test]
public void VerifySignature_MessageDiffersBySingleSpace_ReturnsFailure()
{
    // Test that a single space difference causes verification to fail
    // This catches formatting drift issues
    var address = "9WzDXwBbmkg8ZTbNMqUxvQRAyrZzDsGYdLVL9zYtAWWM";
    var originalMessage = "{\"test\":\"message\"}";
    var modifiedMessage = "{\"test\": \"message\"}";
    var signature = "base58_encoded_signature_for_original";

    var result = _verifier.VerifySignature("solana", address, modifiedMessage, signature);

    result.IsFailure.Should().BeTrue();
    result.Error.Code.Should().Be("SIGNATURE.INVALID_SIGNATURE");
}
```

### Integration Tests
- Verify service registration in DI container
- Test with actual Solana wallet signatures
- Validate performance with multiple verifications

## Future Extensibility

### EVM Support (Phase 2)
- Add ECDSA/secp256k1 verification
- Support Ethereum address format (0x...)
- Handle personal_sign message formatting

### Multi-Chain Architecture
```csharp
public interface ISignatureVerifierFactory
{
    ISignatureVerifier GetVerifier(string chainId);
}

public interface ISignatureVerifier
{
    Result<bool, Error> Verify(byte[] publicKey, byte[] message, byte[] signature);
}
```

## Performance Considerations
- NSec uses native libsodium for optimal performance
- Consider caching PublicKey imports for repeated verifications
- Log only address prefixes to avoid PII exposure

## Security Notes
- Never log full signatures or private keys
- Verify on exact UTF-8 bytes (no BOM, no normalization)
- Use constant-time comparison where applicable
- Validate all inputs before cryptographic operations
- Quick fail ordering: chain → address (32 bytes) → signature (64 bytes) → verify