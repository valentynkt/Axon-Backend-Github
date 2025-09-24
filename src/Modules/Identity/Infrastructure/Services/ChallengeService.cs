using System.Text.Json;
using Axon.Modules.Identity.Application.Common;
using Axon.Modules.Identity.Application.Configuration;
using Axon.Modules.Identity.Application.Contracts.Services;
using Axon.Modules.Identity.Domain.Entities;
using Axon.Modules.Identity.Domain.ValueObjects;
using BuildingBlocks.Core.Diagnostics.Errors;
using CSharpFunctionalExtensions;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Axon.Modules.Identity.Infrastructure.Services;

/// <summary>
/// Simplified challenge service using Microsoft Identity's token system and Data Protection API.
/// Leverages built-in MAC validation and unified TokenReplayCache for production-ready replay protection.
/// </summary>
public sealed class ChallengeService : IChallengeService
{
    private readonly UserManager<AxonUserAuth> _userManager;
    private readonly ChallengeTokenProvider _tokenProvider;
    private readonly TokenReplayCache _replayCache;
    private readonly ILogger<ChallengeService> _logger;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        WriteIndented = false
    };

    public ChallengeService(
        UserManager<AxonUserAuth> userManager,
        ChallengeTokenProvider tokenProvider,
        TokenReplayCache replayCache,
        ILogger<ChallengeService> logger)
    {
        _userManager = userManager ?? throw new ArgumentNullException(nameof(userManager));
        _tokenProvider = tokenProvider ?? throw new ArgumentNullException(nameof(tokenProvider));
        _replayCache = replayCache ?? throw new ArgumentNullException(nameof(replayCache));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Generates an authentication challenge using Identity's token system
    /// </summary>
    public async Task<Result<AuthenticationChallenge, Error>> GenerateChallengeAsync(
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

            // Find or create a temporary user for challenge generation
            var tempUserId = $"challenge:{walletAddress.ToLowerInvariant()}";
            var user = await _userManager.FindByNameAsync(tempUserId) ??
                       AxonUserAuth.Create(
                           new AxonUserId(Guid.NewGuid()),
                           "challenge",
                           "axon",
                           walletAddress,
                           null,
                           null);

            if (await _userManager.FindByNameAsync(tempUserId) == null)
            {
                user.UserName = tempUserId;
                await _userManager.CreateAsync(user);
            }

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

            // Generate protected token using Identity's token provider
            var protectedChallenge = await _tokenProvider.GenerateAsync(
                $"Challenge:{chainId}:{audience}",
                _userManager,
                user);

            var challenge = new AuthenticationChallenge(
                ChainId: chainId,
                Address: walletAddress.ToLowerInvariant(),
                IssuedAt: issuedAt,
                Exp: exp,
                Nonce: nonce,
                Aud: audience,
                Message: message,
                Mac: protectedChallenge,
                Mkv: "dataprotection_v1"); // Using ITimeLimitedDataProtector with automatic MAC versioning

            _logger.LogDebug("Generated challenge for wallet {Address} on chain {ChainId}",
                MaskAddress(walletAddress), chainId);

            return Result.Success<AuthenticationChallenge, Error>(challenge);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate challenge");
            return Result.Failure<AuthenticationChallenge, Error>(
                Error.Internal("Failed to generate challenge"));
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
    /// Checks and marks a nonce as used for replay protection using unified TokenReplayCache
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

            // Check if nonce was already used using unified TokenReplayCache
            var isReplay = await _replayCache.TryFindNonceAsync(nonce);
            if (isReplay)
            {
                _logger.LogWarning("Nonce replay attempt detected: {NonceHash}", ComputeNonceHash(nonce));
                return UnitResult.Failure(Error.Unauthorized("Nonce already used"));
            }

            // Mark nonce as used with automatic expiration
            var added = await _replayCache.TryAddNonceAsync(nonce, TimeSpan.FromMinutes(5));
            if (!added)
            {
                _logger.LogWarning("Nonce replay detected during add operation: {NonceHash}", ComputeNonceHash(nonce));
                return UnitResult.Failure(Error.Unauthorized("Nonce already used"));
            }

            _logger.LogDebug("Nonce marked as used: {NonceHash}", ComputeNonceHash(nonce));
            return UnitResult.Success<Error>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to check nonce");
            return UnitResult.Failure(Error.Internal("Failed to validate nonce"));
        }
    }

    // Legacy HMAC validation removed - using Data Protection API with built-in MAC validation

    private static string MaskAddress(string address)
    {
        return address.Length > 8
            ? $"{address[..4]}...{address[^4..]}"
            : address;
    }

    private static string ComputeNonceHash(string nonce)
    {
        var hashBytes = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(nonce));
        return Convert.ToBase64String(hashBytes)[..12]; // Use first 12 chars for compact logging
    }
}