namespace Axon.Modules.Identity.Domain.ValueObjects;

/// <summary>
/// Risk tolerance tier for trading and financial operations.
/// </summary>
[ValueObject<string>(
    conversions: Conversions.SystemTextJson | Conversions.TypeConverter | Conversions.EfCoreValueConverter)]
public readonly partial struct RiskTier
{
    public static readonly RiskTier Conservative = From("conservative");
    public static readonly RiskTier Balanced = From("balanced");
    public static readonly RiskTier Aggressive = From("aggressive");

    private static readonly string[] AllowedValues = { "conservative", "balanced", "aggressive" };

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

    public bool IsConservative => Value == Conservative.Value;
    public bool IsBalanced => Value == Balanced.Value;
    public bool IsAggressive => Value == Aggressive.Value;

    public static RiskTier Default => Balanced;

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