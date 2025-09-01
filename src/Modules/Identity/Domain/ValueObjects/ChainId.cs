namespace Axon.Modules.Identity.Domain.ValueObjects;

/// <summary>
/// Blockchain chain identifier for Wallet BC.
/// Normalized chain key that must belong to supported set.
/// </summary>
[ValueObject<string>(
    conversions: Conversions.SystemTextJson | Conversions.TypeConverter | Conversions.EfCoreValueConverter)]
public readonly partial struct ChainId
{
    public static readonly ChainId Solana = From("solana");
    public static readonly ChainId Ethereum = From("ethereum");
    public static readonly ChainId Polygon = From("polygon");

    private static readonly HashSet<string> SupportedChains = new(StringComparer.OrdinalIgnoreCase)
    {
        "solana",
        "ethereum",
        "polygon"
    };

    private static Validation Validate(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return Validation.Invalid("Chain cannot be empty.");

        var normalized = input.ToLowerInvariant();

        if (normalized.Length < 2)
            return Validation.Invalid("Chain must be at least 2 characters long.");

        if (normalized.Length > 50)
            return Validation.Invalid("Chain cannot exceed 50 characters.");

        // Allow alphanumeric and hyphens only
        if (!IsValidChainFormat(normalized))
            return Validation.Invalid("Chain must contain only lowercase letters, numbers, and hyphens.");

        // Must be in supported set
        if (!SupportedChains.Contains(normalized))
            return Validation.Invalid($"Unsupported chain '{normalized}'. Supported: {string.Join(", ", SupportedChains)}");

        return Validation.Ok;
    }

    private static string NormalizeInput(string input) => input.ToLowerInvariant().Trim();

    private static bool IsValidChainFormat(string chain)
    {
        return chain.All(c => char.IsLetterOrDigit(c) || c == '-');
    }

    public static Result<ChainId, Error> Create(string? value)
    {
        if (value is null)
            return Result.Failure<ChainId, Error>(
                Error.Validation("Chain is required.", "WALLET.CHAIN.REQUIRED"));

        try
        {
            var vo = From(value);
            return Result.Success<ChainId, Error>(vo);
        }
        catch (ValueObjectValidationException ex)
        {
            return Result.Failure<ChainId, Error>(
                Error.Validation(ex.Message, "WALLET.CHAIN.INVALID"));
        }
    }
}