namespace Axon.Modules.Identity.Domain.ValueObjects;

/// <summary>
/// Blockchain chain identifier with validation.
/// Ensures chain names follow proper format and are not empty.
/// </summary>
[ValueObject<string>(
    conversions: Conversions.SystemTextJson | Conversions.TypeConverter | Conversions.EfCoreValueConverter)]
public readonly partial struct Chain
{
    public static readonly Chain Ethereum = From("ethereum");
    public static readonly Chain Solana = From("solana");
    public static readonly Chain Polygon = From("polygon");

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

        return Validation.Ok;
    }

    private static string NormalizeInput(string input) => input.ToLowerInvariant().Trim();

    private static bool IsValidChainFormat(string chain)
    {
        return chain.All(c => char.IsLetterOrDigit(c) || c == '-');
    }

    public static Result<Chain, Error> Create(string? value)
    {
        if (value is null)
            return Result.Failure<Chain, Error>(
                Error.Validation("Chain is required.", "IDENTITY.CHAIN.REQUIRED"));

        try
        {
            var vo = From(value);
            return Result.Success<Chain, Error>(vo);
        }
        catch (ValueObjectValidationException ex)
        {
            return Result.Failure<Chain, Error>(
                Error.Validation(ex.Message, "IDENTITY.CHAIN.INVALID"));
        }
    }
}