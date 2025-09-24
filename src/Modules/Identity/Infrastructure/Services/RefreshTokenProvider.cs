using Axon.Modules.Identity.Application.Contracts.Services;
using Axon.Modules.Identity.Domain.Entities;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace Axon.Modules.Identity.Infrastructure.Services;

/// <summary>
/// Identity token provider for generating and validating refresh tokens.
/// Integrates with ASP.NET Core Identity's token system for automatic security stamp validation.
/// Uses ITimeLimitedDataProtector for built-in MAC versioning and automatic expiration.
/// </summary>
public sealed class RefreshTokenProvider : IRefreshTokenProvider, IUserTwoFactorTokenProvider<AxonUserAuth>
{
    private const string TokenProviderName = "AxonRefresh";
    private const string TokenPurpose = "Axon.Refresh.Token";
    private static readonly TimeSpan DefaultTokenLifetime = TimeSpan.FromDays(30);

    private readonly ITimeLimitedDataProtector _protector;
    private readonly ILogger<RefreshTokenProvider> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        WriteIndented = false
    };

    public RefreshTokenProvider(
        IDataProtectionProvider dataProtectionProvider,
        ILogger<RefreshTokenProvider> logger)
    {
        ArgumentNullException.ThrowIfNull(dataProtectionProvider);
        _protector = dataProtectionProvider.CreateProtector(TokenPurpose).ToTimeLimitedDataProtector();
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Generates a long-lived refresh token for the user with automatic expiration and MAC versioning
    /// </summary>
    public Task<string> GenerateAsync(string purpose, UserManager<AxonUserAuth> manager, AxonUserAuth user)
    {
        ArgumentNullException.ThrowIfNull(manager);
        ArgumentNullException.ThrowIfNull(user);

        try
        {
            // Create versioned purpose for MAC versioning support
            var versionedPurpose = $"{purpose}.v1";

            var tokenData = new RefreshTokenData
            {
                UserId = user.Id,
                AxonPrincipalId = user.AxonPrincipalId.Value,
                Purpose = versionedPurpose,
                SecurityStamp = user.SecurityStamp,
                Jti = Guid.NewGuid().ToString("N"),
                ProviderType = user.ProviderType
            };

            var serializedData = JsonSerializer.Serialize(tokenData, JsonOptions);
            // Use ITimeLimitedDataProtector with automatic expiration and MAC versioning
            var protectedToken = _protector.Protect(serializedData, DefaultTokenLifetime);

            _logger.LogDebug("Generated refresh token for user {UserId} with JTI {Jti}, expires in {Days} days",
                user.Id, tokenData.Jti, DefaultTokenLifetime.TotalDays);

            return Task.FromResult(protectedToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate refresh token for user {UserId}", user.Id);
            throw;
        }
    }

    /// <summary>
    /// Validates a refresh token including security stamp verification and automatic expiration
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
            var tokenData = JsonSerializer.Deserialize<RefreshTokenData>(unprotectedData, JsonOptions);

            if (tokenData == null)
            {
                _logger.LogWarning("Invalid refresh token format for user {UserId}", user.Id);
                return Task.FromResult(false);
            }

            // Validate user ID matches
            if (tokenData.UserId != user.Id)
            {
                _logger.LogWarning("Refresh token user ID mismatch for user {UserId}", user.Id);
                return Task.FromResult(false);
            }

            // Validate versioned purpose matches (supports both v1 and legacy formats)
            if (tokenData.Purpose != versionedPurpose && tokenData.Purpose != purpose)
            {
                _logger.LogWarning("Refresh token purpose mismatch for user {UserId}. Expected: {Expected}, Got: {Actual}",
                    user.Id, versionedPurpose, tokenData.Purpose);
                return Task.FromResult(false);
            }

            // Validate security stamp (automatic invalidation when user changes)
            if (tokenData.SecurityStamp != user.SecurityStamp)
            {
                _logger.LogWarning("Refresh token security stamp mismatch for user {UserId} - token invalidated", user.Id);
                return Task.FromResult(false);
            }

            _logger.LogDebug("Refresh token validated successfully for user {UserId} with JTI {Jti}",
                user.Id, tokenData.Jti);

            return Task.FromResult(true);
        }
        catch (System.Security.Cryptography.CryptographicException ex)
        {
            _logger.LogWarning(ex, "Refresh token validation failed for user {UserId} - token expired, tampered, or invalid MAC", user.Id);
            return Task.FromResult(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to validate refresh token for user {UserId}", user.Id);
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
    /// Gets the JTI (JWT ID) from a refresh token for tracking
    /// </summary>
    public string? GetJtiFromToken(string token)
    {
        try
        {
            var unprotectedData = _protector.Unprotect(token);
            var tokenData = JsonSerializer.Deserialize<RefreshTokenData>(unprotectedData, JsonOptions);
            return tokenData?.Jti;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Gets the user ID from a refresh token without full validation (for performance optimization)
    /// </summary>
    public Guid? GetUserIdFromToken(string token)
    {
        try
        {
            var unprotectedData = _protector.Unprotect(token);
            var tokenData = JsonSerializer.Deserialize<RefreshTokenData>(unprotectedData, JsonOptions);
            return tokenData?.UserId;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Token data structure for refresh tokens - ITimeLimitedDataProtector handles expiration automatically
    /// </summary>
    private sealed class RefreshTokenData
    {
        public Guid UserId { get; set; }
        public Guid AxonPrincipalId { get; set; }
        public string Purpose { get; set; } = string.Empty;
        public string SecurityStamp { get; set; } = string.Empty;
        public string Jti { get; set; } = string.Empty;
        public string ProviderType { get; set; } = string.Empty;
    }
}