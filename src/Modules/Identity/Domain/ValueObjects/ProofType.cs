namespace Axon.Modules.Identity.Domain.ValueObjects;

/// <summary>
/// Type of proof used to establish wallet ownership.
/// </summary>
[ValueObject<string>(
    conversions: Conversions.SystemTextJson | Conversions.TypeConverter | Conversions.EfCoreValueConverter)]
public readonly partial struct ProofType
{
    public static readonly ProofType DynamicVerified = From("dynamic_verified");
    public static readonly ProofType DirectSignature = From("direct_signature");
    public static readonly ProofType WatchOnly = From("watch_only");

    private static readonly string[] AllowedValues = { "dynamic_verified", "direct_signature", "watch_only" };

    private static Validation Validate(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return Validation.Invalid("Proof type cannot be empty.");

        var normalized = input.ToLowerInvariant();
        if (!AllowedValues.Contains(normalized))
            return Validation.Invalid($"Invalid proof type: {input}. Must be one of: {string.Join(", ", AllowedValues)}.");

        return Validation.Ok;
    }

    private static string NormalizeInput(string input) => input.ToLowerInvariant();

    public bool IsDynamicVerified => Value == DynamicVerified.Value;
    public bool IsDirectSignature => Value == DirectSignature.Value;
    public bool IsWatchOnly => Value == WatchOnly.Value;

    public static Result<ProofType, Error> Create(string? value)
    {
        if (value is null)
            return Result.Failure<ProofType, Error>(
                Error.Validation("Proof type is required.", "IDENTITY.WALLET.PROOF.REQUIRED"));

        try
        {
            var vo = From(value);
            return Result.Success<ProofType, Error>(vo);
        }
        catch (ValueObjectValidationException ex)
        {
            return Result.Failure<ProofType, Error>(
                Error.Validation(ex.Message, "IDENTITY.WALLET.PROOF.INVALID"));
        }
    }
}