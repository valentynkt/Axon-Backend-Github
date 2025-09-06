namespace Axon.Modules.Identity.Domain.ValueObjects;

/// <summary>
/// Access mode for wallet ownership.
/// </summary>
[ValueObject<string>(
    conversions: Conversions.SystemTextJson | Conversions.TypeConverter | Conversions.EfCoreValueConverter)]
public readonly partial struct AccessMode
{
    private static readonly Lazy<HashSet<string>> _allowedValues = new(() => new(StringComparer.OrdinalIgnoreCase)
    {
        "signing",
        "watch_only"
    });
    
    private static HashSet<string> AllowedValues => _allowedValues.Value;

    public static readonly AccessMode Signing = From("signing");
    public static readonly AccessMode WatchOnly = From("watch_only");

    private static Validation Validate(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return Validation.Invalid("Access mode cannot be empty.");

        var normalized = input.ToLowerInvariant();
        if (!AllowedValues.Contains(normalized))
            return Validation.Invalid($"Invalid access mode: {input}. Must be one of: {string.Join(", ", AllowedValues)}.");

        return Validation.Ok;
    }

    private static string NormalizeInput(string input) => input.ToLowerInvariant();

    public bool IsSigning => Value == Signing.Value;
    public bool IsWatchOnly => Value == WatchOnly.Value;

    public static AccessMode Default => Signing;

    public static Result<AccessMode, Error> Create(string? value)
    {
        if (value is null)
            return Result.Success<AccessMode, Error>(Default);

        try
        {
            var vo = From(value);
            return Result.Success<AccessMode, Error>(vo);
        }
        catch (ValueObjectValidationException ex)
        {
            return Result.Failure<AccessMode, Error>(
                Error.Validation(ex.Message, "IDENTITY.WALLET.ACCESS_MODE.INVALID"));
        }
    }
}