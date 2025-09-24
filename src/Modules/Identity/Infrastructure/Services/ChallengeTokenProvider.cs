using System.Text.Json;
using Axon.Modules.Identity.Application.Common;
using Axon.Modules.Identity.Domain.Entities;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;

namespace Axon.Modules.Identity.Infrastructure.Services;

/// <summary>
/// Identity token provider for generating and validating challenge tokens using Data Protection API.
/// Integrates with ASP.NET Core Identity's token system for automatic security stamp validation.
/// Uses ITimeLimitedDataProtector for built-in MAC versioning and automatic expiration.
/// </summary>
public sealed class ChallengeTokenProvider : IUserTwoFactorTokenProvider<AxonUserAuth>
{
    private const string TokenProviderName = "AxonChallenge";
    private const string TokenPurpose = "Axon.Challenge.Token";
    private static readonly TimeSpan DefaultTokenLifetime = TimeSpan.FromMinutes(5);

    private readonly ITimeLimitedDataProtector _protector;
    private readonly ILogger<ChallengeTokenProvider> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        WriteIndented = false
    };

    public ChallengeTokenProvider(
        IDataProtectionProvider dataProtectionProvider,
        ILogger<ChallengeTokenProvider> logger)
    {
        ArgumentNullException.ThrowIfNull(dataProtectionProvider);
        _protector = dataProtectionProvider.CreateProtector(TokenPurpose).ToTimeLimitedDataProtector();
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Generates a time-limited challenge token for the user with automatic expiration
    /// </summary>
    public Task<string> GenerateAsync(string purpose, UserManager<AxonUserAuth> manager, AxonUserAuth user)
    {
        ArgumentNullException.ThrowIfNull(manager);
        ArgumentNullException.ThrowIfNull(user);

        try
        {
            // Create versioned purpose for MAC versioning support
            var versionedPurpose = $"{purpose}.v1";

            var tokenData = new ChallengeTokenData
            {
                UserId = user.Id,
                Purpose = versionedPurpose,
                SecurityStamp = user.SecurityStamp,
                Nonce = Guid.NewGuid().ToString("N")
            };

            var serializedData = JsonSerializer.Serialize(tokenData, JsonOptions);
            // Use ITimeLimitedDataProtector with automatic expiration and MAC versioning
            var protectedToken = _protector.Protect(serializedData, DefaultTokenLifetime);

            _logger.LogDebug("Generated challenge token for user {UserId} with purpose {Purpose}, expires in {Minutes} minutes",
                user.Id, versionedPurpose, DefaultTokenLifetime.TotalMinutes);

            return Task.FromResult(protectedToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate challenge token for user {UserId}", user.Id);
            throw;
        }
    }

    /// <summary>
    /// Validates a challenge token including security stamp verification and automatic expiration
    /// </summary>
    public Task<bool> ValidateAsync(string purpose, string token, UserManager<AxonUserAuth> manager, AxonUserAuth user)
    {
        ArgumentNullException.ThrowIfNull(manager);
        ArgumentNullException.ThrowIfNull(user);

        if (string.IsNullOrEmpty(token))
        {
            return Task.FromResult(false);
        }

        try
        {
            // Create versioned purpose for MAC versioning support
            var versionedPurpose = $"{purpose}.v1";

            // Unprotect token - ITimeLimitedDataProtector automatically handles expiration and MAC validation
            var unprotectedData = _protector.Unprotect(token);
            var tokenData = JsonSerializer.Deserialize<ChallengeTokenData>(unprotectedData, JsonOptions);

            if (tokenData == null)
            {
                _logger.LogWarning("Invalid challenge token format for user {UserId}", user.Id);
                return Task.FromResult(false);
            }

            // Validate user ID matches
            if (tokenData.UserId != user.Id)
            {
                _logger.LogWarning("Challenge token user ID mismatch for user {UserId}", user.Id);
                return Task.FromResult(false);
            }

            // Validate versioned purpose matches (supports both v1 and legacy formats)
            if (tokenData.Purpose != versionedPurpose && tokenData.Purpose != purpose)
            {
                _logger.LogWarning("Challenge token purpose mismatch for user {UserId}. Expected: {Expected}, Got: {Actual}",
                    user.Id, versionedPurpose, tokenData.Purpose);
                return Task.FromResult(false);
            }

            // Validate security stamp (automatic invalidation when user changes)
            if (tokenData.SecurityStamp != user.SecurityStamp)
            {
                _logger.LogWarning("Challenge token security stamp mismatch for user {UserId} - token invalidated", user.Id);
                return Task.FromResult(false);
            }

            _logger.LogDebug("Challenge token validated successfully for user {UserId} with purpose {Purpose}",
                user.Id, tokenData.Purpose);

            return Task.FromResult(true);
        }
        catch (System.Security.Cryptography.CryptographicException ex)
        {
            _logger.LogWarning(ex, "Challenge token validation failed for user {UserId} - token expired, tampered, or invalid MAC", user.Id);
            return Task.FromResult(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to validate challenge token for user {UserId}", user.Id);
            return Task.FromResult(false);
        }
    }

    /// <summary>
    /// Determines if the provider can generate tokens for the user
    /// </summary>
    public Task<bool> CanGenerateTwoFactorTokenAsync(UserManager<AxonUserAuth> manager, AxonUserAuth user)
    {
        return Task.FromResult(user != null);
    }

    /// <summary>
    /// Token data structure for challenge tokens - ITimeLimitedDataProtector handles expiration automatically
    /// </summary>
    private sealed class ChallengeTokenData
    {
        public Guid UserId { get; set; }
        public string Purpose { get; set; } = string.Empty;
        public string SecurityStamp { get; set; } = string.Empty;
        public string Nonce { get; set; } = string.Empty;
    }
}