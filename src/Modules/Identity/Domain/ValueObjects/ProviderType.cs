using BuildingBlocks.Core.Diagnostics.Errors;
using CSharpFunctionalExtensions;
using Vogen;

namespace Axon.Modules.Identity.Domain.ValueObjects;

/// <summary>
/// Represents an identity provider type (e.g., 'dynamic', 'google', 'github').
/// Creation: <c>ProviderType.From("...")</c> (throws on invalid)
/// Non-throwing: <c>ProviderType.Create("...")</c> (returns Result)
/// JSON: STJ converter generated
/// EF Core: value converter generated  
/// TypeConverter: generated (useful for binding, config, etc.)
/// </summary>
[ValueObject<string>(
    conversions: Conversions.SystemTextJson | Conversions.TypeConverter | Conversions.EfCoreValueConverter)]
public readonly partial struct ProviderType
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

    // Vogen will call this before Validate and before storing the value
    private static string NormalizeInput(string input) => input.Trim().ToLowerInvariant();

    // Vogen passes the normalized input here
    private static Validation Validate(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return Validation.Invalid("ProviderType cannot be null or empty.");

        if (!SupportedProviders.Contains(input))
            return Validation.Invalid($"Unsupported provider type: {input}");

        return Validation.Ok;
    }

    /// <summary>
    /// Non-throwing factory bridging Vogen to CFE <c>Result</c>.
    /// Preferred in application layer to avoid exception-based control flow.
    /// </summary>
    public static Result<ProviderType, Error> Create(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return Result.Failure<ProviderType, Error>(
                Error.Validation("ProviderType cannot be null or empty.", "PROVIDER.TYPE.EMPTY"));
        }

        var normalized = NormalizeInput(value);
        
        if (!SupportedProviders.Contains(normalized))
        {
            return Result.Failure<ProviderType, Error>(
                Error.Validation($"Unsupported provider type: {value}", "PROVIDER.TYPE.UNSUPPORTED"));
        }

        // Use generated TryParse; provider null is fine
        return TryParse(value, provider: null, out var vo)
            ? Result.Success<ProviderType, Error>(vo)
            : Result.Failure<ProviderType, Error>(
                Error.Validation($"Invalid provider type: {value}", "PROVIDER.TYPE.INVALID"));
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

    public override string ToString() => Value;
}