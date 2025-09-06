namespace Axon.Modules.Identity.Domain.ValueObjects;

/// <summary>
/// State of wallet ownership.
/// </summary>
[ValueObject<string>(
    conversions: Conversions.SystemTextJson | Conversions.TypeConverter | Conversions.EfCoreValueConverter)]
public readonly partial struct OwnershipState
{
    private static readonly HashSet<string> AllowedValues = new(StringComparer.OrdinalIgnoreCase)
    {
        "verified",
        "pending", 
        "revoked"
    };

    public static readonly OwnershipState Verified = From("verified");
    public static readonly OwnershipState Pending = From("pending");
    public static readonly OwnershipState Revoked = From("revoked");

    private static Validation Validate(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return Validation.Invalid("Ownership state cannot be empty.");

        var normalized = input.ToLowerInvariant();
        if (!AllowedValues.Contains(normalized))
            return Validation.Invalid($"Invalid ownership state: {input}. Must be one of: {string.Join(", ", AllowedValues)}.");

        return Validation.Ok;
    }

    private static string NormalizeInput(string input) => input.ToLowerInvariant();

    public bool IsVerified => Value == Verified.Value;
    public bool IsPending => Value == Pending.Value;
    public bool IsRevoked => Value == Revoked.Value;

    public static OwnershipState Default => Verified;

    public static Result<OwnershipState, Error> Create(string? value)
    {
        if (value is null)
            return Result.Success<OwnershipState, Error>(Default);

        try
        {
            var vo = From(value);
            return Result.Success<OwnershipState, Error>(vo);
        }
        catch (ValueObjectValidationException ex)
        {
            return Result.Failure<OwnershipState, Error>(
                Error.Validation(ex.Message, "IDENTITY.WALLET.STATE.INVALID"));
        }
    }
}