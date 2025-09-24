using System.Text.Json;
using Axon.Modules.Identity.Application.Common;
using Axon.Modules.Identity.Application.Configuration;
using Axon.Modules.Identity.Application.Contracts.Services;
using Axon.Modules.Identity.Domain.Entities;
using BuildingBlocks.Core.Diagnostics.Errors;
using CSharpFunctionalExtensions;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Axon.Modules.Identity.Infrastructure.Services;

/// <summary>
/// Simplified challenge service using Microsoft Data Protection API.
/// Replaces manual HMAC operations with automatic encryption, key rotation, and tamper protection.
/// </summary>
public sealed class ChallengeService : IChallengeService
{
    private readonly IDataProtector _challengeProtector;
    private readonly UserManager<AxonUserAuth> _userManager;
    private readonly IMemoryCache _cache;
    private readonly ILogger<ChallengeService> _logger;
    private readonly IOptions<AuthenticationOptions> _authOptions;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        WriteIndented = false
    };

    public ChallengeService(
        IDataProtectionProvider dataProtectionProvider,
        UserManager<AxonUserAuth> userManager,
        IMemoryCache cache,
        ILogger<ChallengeService> logger,
        IOptions<AuthenticationOptions> authOptions)
    {
        ArgumentNullException.ThrowIfNull(dataProtectionProvider);
        _challengeProtector = dataProtectionProvider.CreateProtector("Axon.Challenge");
        _userManager = userManager ?? throw new ArgumentNullException(nameof(userManager));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _authOptions = authOptions ?? throw new ArgumentNullException(nameof(authOptions));
    }

    /// <summary>
    /// Generates an authentication challenge for wallet signing using Data Protection API
    /// </summary>
    public Task<Result<AuthenticationChallenge, Error>> GenerateChallengeAsync(
        string chainId,
        string walletAddress,
        string audience,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var now = DateTimeOffset.UtcNow;
            var issuedAt = now.ToUnixTimeSeconds();
            var exp = now.AddMinutes(5).ToUnixTimeSeconds();
            var nonce = Guid.NewGuid().ToString("N");

            // Create the challenge data
            var challengeData = new Dictionary<string, object>
            {
                ["chain_id"] = chainId,
                ["address"] = walletAddress.ToLowerInvariant(),
                ["issued_at"] = issuedAt,
                ["exp"] = exp,
                ["nonce"] = nonce,
                ["aud"] = audience
            };

            var message = JsonSerializer.Serialize(challengeData, JsonOptions);

            // Use Data Protection API for tamper-proof protection with automatic expiration
            var protectedChallenge = _challengeProtector.Protect(message, TimeSpan.FromMinutes(5));

            var challenge = new AuthenticationChallenge(
                ChainId: chainId,
                Address: walletAddress.ToLowerInvariant(),
                IssuedAt: issuedAt,
                Exp: exp,
                Nonce: nonce,
                Aud: audience,
                Message: message,
                Mac: protectedChallenge, // Protected token instead of HMAC
                Mkv: "dp_v1"); // Data Protection version

            _logger.LogDebug("Generated protected challenge for wallet {Address} on chain {ChainId}",
                MaskAddress(walletAddress), chainId);

            return Task.FromResult(Result.Success<AuthenticationChallenge, Error>(challenge));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate challenge");
            return Task.FromResult(Result.Failure<AuthenticationChallenge, Error>(
                Error.Internal("Failed to generate challenge")));
        }
    }

    /// <summary>
    /// Validates the protected token (replaces HMAC validation)
    /// </summary>
    public Result<bool, Error> ValidateMac(
        string message,
        string protectedToken,
        string keyVersion)
    {
        try
        {
            // Handle legacy HMAC tokens during migration
            if (keyVersion != "dp_v1")
            {
                return ValidateLegacyHmac(message, protectedToken, keyVersion);
            }

            // Validate using Data Protection API
            var unprotectedMessage = _challengeProtector.Unprotect(protectedToken);
            var isValid = string.Equals(unprotectedMessage, message, StringComparison.Ordinal);

            if (!isValid)
            {
                _logger.LogWarning("Protected token validation failed");
            }

            return Result.Success<bool, Error>(isValid);
        }
        catch (System.Security.Cryptography.CryptographicException)
        {
            _logger.LogWarning("Protected token validation failed - token expired or tampered");
            return Result.Success<bool, Error>(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to validate protected token");
            return Result.Failure<bool, Error>(
                Error.Internal("Failed to validate protected token"));
        }
    }

    /// <summary>
    /// Validates a challenge message structure and TTL
    /// </summary>
    public Result<bool, Error> ValidateChallenge(
        string message,
        string expectedChainId,
        string expectedAddress,
        string expectedAudience)
    {
        try
        {
            // Parse the message as JSON to extract challenge data
            var challengeData = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(message, JsonOptions);
            if (challengeData == null)
            {
                _logger.LogWarning("Failed to parse challenge message");
                return Result.Success<bool, Error>(false);
            }

            // Validate chain ID
            if (challengeData.TryGetValue("chain_id", out var chainId))
            {
                var chainIdValue = chainId.GetString();
                if (chainIdValue != expectedChainId)
                {
                    _logger.LogWarning("Chain ID mismatch: expected {Expected}, got {Actual}",
                        expectedChainId, chainIdValue);
                    return Result.Success<bool, Error>(false);
                }
            }
            else
            {
                _logger.LogWarning("Missing chain_id in challenge");
                return Result.Success<bool, Error>(false);
            }

            // Validate address (case insensitive)
            if (challengeData.TryGetValue("address", out var address))
            {
                var addressValue = address.GetString();
                if (!string.Equals(addressValue, expectedAddress, StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogWarning("Address mismatch: expected {Expected}, got {Actual}",
                        expectedAddress, addressValue);
                    return Result.Success<bool, Error>(false);
                }
            }
            else
            {
                _logger.LogWarning("Missing address in challenge");
                return Result.Success<bool, Error>(false);
            }

            // Validate audience
            if (challengeData.TryGetValue("aud", out var aud))
            {
                var audValue = aud.GetString();
                if (audValue != expectedAudience)
                {
                    _logger.LogWarning("Audience mismatch: expected {Expected}, got {Actual}",
                        expectedAudience, audValue);
                    return Result.Success<bool, Error>(false);
                }
            }
            else
            {
                _logger.LogWarning("Missing aud in challenge");
                return Result.Success<bool, Error>(false);
            }

            // Check expiry
            if (challengeData.TryGetValue("exp", out var exp) && exp.TryGetInt64(out var expValue))
            {
                var expiryTime = DateTimeOffset.FromUnixTimeSeconds(expValue);
                if (expiryTime <= DateTimeOffset.UtcNow)
                {
                    _logger.LogWarning("Challenge expired at {ExpiryTime}", expiryTime);
                    return Result.Success<bool, Error>(false);
                }
            }
            else
            {
                _logger.LogWarning("Missing or invalid exp in challenge");
                return Result.Success<bool, Error>(false);
            }

            // Check issued at (prevent future challenges)
            if (challengeData.TryGetValue("issued_at", out var issuedAt) && issuedAt.TryGetInt64(out var issuedAtValue))
            {
                var issuedTime = DateTimeOffset.FromUnixTimeSeconds(issuedAtValue);
                // Allow 1 minute clock skew for future challenges
                if (issuedTime > DateTimeOffset.UtcNow.AddMinutes(1))
                {
                    _logger.LogWarning("Challenge issued in future at {IssuedTime}", issuedTime);
                    return Result.Success<bool, Error>(false);
                }
            }

            return Result.Success<bool, Error>(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to validate challenge");
            return Result.Failure<bool, Error>(
                Error.Internal("Failed to validate challenge"));
        }
    }

    /// <summary>
    /// Checks and marks a nonce as used for replay protection using memory cache
    /// </summary>
    public async Task<UnitResult<Error>> CheckAndMarkNonceUsedAsync(
        string signedMessage,
        string mkv,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Extract nonce from signed message
            var messageData = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(signedMessage, JsonOptions);
            if (messageData == null || !messageData.TryGetValue("nonce", out var nonceElement))
            {
                _logger.LogWarning("Invalid message format or missing nonce");
                return UnitResult.Failure(Error.Validation("Invalid message format"));
            }

            var nonce = nonceElement.GetString();
            if (string.IsNullOrEmpty(nonce))
            {
                _logger.LogWarning("Empty nonce in message");
                return UnitResult.Failure(Error.Validation("Missing nonce"));
            }

            // Check if nonce was already used
            var cacheKey = $"nonce:used:{nonce}";
            if (_cache.TryGetValue(cacheKey, out _))
            {
                _logger.LogWarning("Nonce replay attempt detected: {Nonce}", nonce);
                return UnitResult.Failure(Error.Unauthorized("Nonce already used"));
            }

            // Mark nonce as used with 5-minute expiration (same as challenge expiration)
            _cache.Set(cacheKey, true, TimeSpan.FromMinutes(5));

            _logger.LogDebug("Nonce marked as used: {Nonce}", nonce);
            return await Task.FromResult(UnitResult.Success<Error>());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to check nonce");
            return UnitResult.Failure(Error.Internal("Failed to validate nonce"));
        }
    }

    /// <summary>
    /// Legacy HMAC validation for backward compatibility during migration
    /// </summary>
    private Result<bool, Error> ValidateLegacyHmac(string message, string mac, string keyVersion)
    {
        try
        {
            var hmacKeys = _authOptions.Value.HmacKeys;
            if (hmacKeys == null || hmacKeys.Count == 0)
            {
                _logger.LogWarning("No legacy HMAC keys configured for key version: {KeyVersion}", keyVersion);
                return Result.Success<bool, Error>(false);
            }

            var hmacKey = hmacKeys.GetValueOrDefault(keyVersion);
            if (hmacKey == null)
            {
                _logger.LogWarning("Invalid legacy key version: {KeyVersion}", keyVersion);
                return Result.Success<bool, Error>(false);
            }

            using var hmac = System.Security.Cryptography.HMACSHA256.Create();
            hmac.Key = Convert.FromBase64String(hmacKey);
            var messageBytes = System.Text.Encoding.UTF8.GetBytes(message);
            var computedMac = Convert.ToBase64String(hmac.ComputeHash(messageBytes));

            var isValid = System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(
                Convert.FromBase64String(computedMac),
                Convert.FromBase64String(mac));

            if (!isValid)
            {
                _logger.LogWarning("Legacy HMAC validation failed for key version {KeyVersion}", keyVersion);
            }

            return Result.Success<bool, Error>(isValid);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to validate legacy HMAC");
            return Result.Success<bool, Error>(false);
        }
    }

    private static string MaskAddress(string address)
    {
        return address.Length > 8
            ? $"{address[..4]}...{address[^4..]}"
            : address;
    }
}