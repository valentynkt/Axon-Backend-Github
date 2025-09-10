namespace Axon.Modules.Identity.Domain.ValueObjects;

/// <summary>
/// Represents an identity provider type (e.g., 'dynamic', 'google', 'github').
/// </summary>
public sealed record ProviderType
{
    // Supported providers
    private static readonly HashSet<string> SupportedProviders = new(StringComparer.OrdinalIgnoreCase)
    {
        "dynamic",      // Dynamic.xyz authentication
        "siws",         // Sign-In With Solana (future)
        "manual",       // Manual wallet verification
        "google",       // OAuth providers (future)
        "github",       
        "discord",
        "twitter"
    };

    public string Value { get; }

    private ProviderType(string value)
    {
        Value = value;
    }

    public static ProviderType From(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("ProviderType cannot be null or empty.", nameof(value));

        var normalized = value.ToLowerInvariant().Trim();
        
        if (!IsSupported(normalized))
            throw new ArgumentException($"Unsupported provider type: {value}", nameof(value));

        return new ProviderType(normalized);
    }

    // Common provider types
    public static ProviderType Dynamic => From("dynamic");
    public static ProviderType Manual => From("manual");
    public static ProviderType Siws => From("siws");

    public static bool IsSupported(string provider)
    {
        return !string.IsNullOrWhiteSpace(provider) && 
               SupportedProviders.Contains(provider.Trim().ToLowerInvariant());
    }

    public static IEnumerable<string> GetSupportedProviders() => SupportedProviders;

    /// <summary>
    /// Checks if this provider supports wallet authentication.
    /// </summary>
    public bool SupportsWalletAuth()
    {
        return Value is "dynamic" or "siws" or "manual";
    }

    /// <summary>
    /// Checks if this provider supports OAuth.
    /// </summary>
    public bool SupportsOAuth()
    {
        return Value is "google" or "github" or "discord" or "twitter";
    }

    public static implicit operator string(ProviderType providerType) => providerType.Value;
    public static implicit operator ProviderType(string value) => From(value);

    public override string ToString() => Value;
}