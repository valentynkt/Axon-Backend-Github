namespace Axon.Modules.Identity.Domain.ValueObjects;

/// <summary>
/// Risk tolerance tier for trading and financial operations.
/// </summary>
[ValueObject<string>(
    conversions: Conversions.SystemTextJson | Conversions.TypeConverter | Conversions.EfCoreValueConverter)]
public readonly partial struct RiskTier
{
    private static readonly HashSet<string> AllowedValues = new(StringComparer.OrdinalIgnoreCase)
    {
        "low",
        "medium",
        "high", 
        "critical"
    };

    public static readonly RiskTier Low = From("low");
    public static readonly RiskTier Medium = From("medium");
    public static readonly RiskTier High = From("high");
    public static readonly RiskTier Critical = From("critical");

    private static Validation Validate(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return Validation.Invalid("Risk tier cannot be empty.");

        var normalized = input.ToLowerInvariant();
        if (!AllowedValues.Contains(normalized))
            return Validation.Invalid($"Invalid risk tier: {input}. Must be one of: {string.Join(", ", AllowedValues)}.");

        return Validation.Ok;
    }

    private static string NormalizeInput(string input) => input.ToLowerInvariant();

    public bool IsLow => Value == Low.Value;
    public bool IsMedium => Value == Medium.Value;
    public bool IsHigh => Value == High.Value;
    public bool IsCritical => Value == Critical.Value;

    public static RiskTier Default => Medium;

    public static Result<RiskTier, Error> Create(string? value)
    {
        if (value is null)
            return Result.Success<RiskTier, Error>(Default);

        try
        {
            var vo = From(value);
            return Result.Success<RiskTier, Error>(vo);
        }
        catch (ValueObjectValidationException ex)
        {
            return Result.Failure<RiskTier, Error>(
                Error.Validation(ex.Message, "IDENTITY.PROFILE.RISK_TIER.INVALID"));
        }
    }
}