namespace Axon.Modules.Identity.Application.Configuration;

/// <summary>
/// Unified configuration options for all authentication services.
/// Consolidates settings from AxonJwtService, CanonicalMessageService, and replay guard.
/// </summary>
public sealed class AuthenticationOptions
{
    public const string SectionName = "Authentication";

    // JWT Token Settings
    public string Issuer { get; set; } = string.Empty;
    public string Audience { get; set; } = "axon-api";
    public string SigningKey { get; set; } = string.Empty;

    // Challenge Settings
    public string HmacSecret { get; set; } = string.Empty;
    public string DefaultAudience { get; set; } = "axon-challenge";

    // Timing Settings - SECURITY BEST PRACTICES
    public int ClockSkewSeconds { get; set; } = 30;  // Reduced from 60 for tighter security
    public int MaxTtlSeconds { get; set; } = 300;
    public int DefaultTokenExpirySeconds { get; set; } = 900; // 15 minutes instead of 1 hour
    public int RefreshTokenExpirySeconds { get; set; } = 2592000; // 30 days

    // Replay Protection
    public int ReplayGuardBufferMinutes { get; set; } = 1;

    // Validation
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Issuer))
            throw new InvalidOperationException($"{SectionName}:Issuer is required");

        if (string.IsNullOrWhiteSpace(SigningKey))
            throw new InvalidOperationException($"{SectionName}:SigningKey is required");

        if (string.IsNullOrWhiteSpace(HmacSecret))
            throw new InvalidOperationException($"{SectionName}:HmacSecret is required");

        if (ClockSkewSeconds < 0 || ClockSkewSeconds > 300)
            throw new InvalidOperationException($"{SectionName}:ClockSkewSeconds must be between 0 and 300");

        if (MaxTtlSeconds <= 0 || MaxTtlSeconds > 3600)
            throw new InvalidOperationException($"{SectionName}:MaxTtlSeconds must be between 1 and 3600");

        if (DefaultTokenExpirySeconds <= 0 || DefaultTokenExpirySeconds > 3600)
            throw new InvalidOperationException($"{SectionName}:DefaultTokenExpirySeconds must be between 1 and 3600 seconds");

        if (RefreshTokenExpirySeconds <= 0 || RefreshTokenExpirySeconds > 7776000) // Max 90 days
            throw new InvalidOperationException($"{SectionName}:RefreshTokenExpirySeconds must be between 1 and 7776000 seconds (90 days)");
    }
}