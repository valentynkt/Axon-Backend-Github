using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Logging;

namespace Axon.Modules.Identity.Infrastructure.Services;

/// <summary>
/// Simple challenge service using DataProtection API
/// Replaces custom HMAC/MAC generation with Microsoft's secure implementation
/// </summary>
public sealed class ChallengeService
{
    private readonly IDataProtector _protector;
    private readonly ILogger<ChallengeService> _logger;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower, WriteIndented = false };

    public ChallengeService(IDataProtectionProvider dataProtectionProvider, ILogger<ChallengeService> logger)
    {
        _protector = dataProtectionProvider.CreateProtector("WalletChallenge");
        _logger = logger;
    }

    /// <summary>
    /// Generate a secure challenge for wallet authentication
    /// Replaces complex canonical message generation with simple protected data
    /// </summary>
    public string GenerateChallenge(string chainId, string walletAddress, string? audience = null)
    {
        var challengeData = new ChallengeData
        {
            ChainId = chainId,
            Address = walletAddress,
            Audience = audience ?? "axon-challenge",
            IssuedAt = DateTimeOffset.UtcNow,
            ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(5), // 5-minute expiry
            Nonce = Guid.NewGuid().ToString("N")
        };

        var json = JsonSerializer.Serialize(challengeData);
        var protectedData = _protector.Protect(json);

        _logger.LogDebug("Generated challenge for wallet {Address} on chain {ChainId}", walletAddress, chainId);
        return protectedData;
    }

    /// <summary>
    /// Validate a challenge and extract the challenge data
    /// Replaces MAC validation with DataProtection unprotect
    /// </summary>
    public (bool IsValid, ChallengeData? Data) ValidateChallenge(string protectedChallenge)
    {
        try
        {
            var json = _protector.Unprotect(protectedChallenge);
            var challengeData = JsonSerializer.Deserialize<ChallengeData>(json);

            if (challengeData == null)
                return (false, null);

            // Check expiry
            if (challengeData.ExpiresAt <= DateTimeOffset.UtcNow)
            {
                _logger.LogWarning("Challenge expired for address {Address}", challengeData.Address);
                return (false, null);
            }

            // Check issued at (prevent future challenges)
            if (challengeData.IssuedAt > DateTimeOffset.UtcNow.AddMinutes(1))
            {
                _logger.LogWarning("Challenge issued in future for address {Address}", challengeData.Address);
                return (false, null);
            }

            return (true, challengeData);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to validate challenge");
            return (false, null);
        }
    }

    /// <summary>
    /// Get the canonical message that should be signed by the wallet
    /// This is what the wallet will actually sign
    /// </summary>
    public static string GetSigningMessage(ChallengeData challengeData)
    {
        return JsonSerializer.Serialize(challengeData, JsonOptions);
    }
}

/// <summary>
/// Challenge data structure
/// Much simpler than the previous canonical message format
/// </summary>
public sealed record ChallengeData
{
    public string ChainId { get; init; } = string.Empty;
    public string Address { get; init; } = string.Empty;
    public string Audience { get; init; } = string.Empty;
    public DateTimeOffset IssuedAt { get; init; }
    public DateTimeOffset ExpiresAt { get; init; }
    public string Nonce { get; init; } = string.Empty;
}