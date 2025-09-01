namespace Axon.Modules.Identity.Domain.ValueObjects;

/// <summary>
/// Type of principal in the system.
/// </summary>
[ValueObject<string>(
    conversions: Conversions.SystemTextJson | Conversions.TypeConverter | Conversions.EfCoreValueConverter)]
public readonly partial struct PrincipalType
{
    public static readonly PrincipalType Human = From("human");
    public static readonly PrincipalType Service = From("service");

    private static Validation Validate(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return Validation.Invalid("Principal type cannot be empty.");

        var normalized = input.ToLowerInvariant();
        if (normalized is not ("human" or "service"))
            return Validation.Invalid($"Invalid principal type: {input}. Must be 'human' or 'service'.");

        return Validation.Ok;
    }

    private static string NormalizeInput(string input) => input.ToLowerInvariant();

    public bool IsHuman => Value == Human.Value;
    public bool IsService => Value == Service.Value;

    public static Result<PrincipalType, Error> Create(string? value)
    {
        if (value is null)
            return Result.Failure<PrincipalType, Error>(
                Error.Validation("Principal type is required.", "IDENTITY.PRINCIPAL.TYPE.REQUIRED"));

        try
        {
            var vo = From(value);
            return Result.Success<PrincipalType, Error>(vo);
        }
        catch (ValueObjectValidationException ex)
        {
            return Result.Failure<PrincipalType, Error>(
                Error.Validation(ex.Message, "IDENTITY.PRINCIPAL.TYPE.INVALID"));
        }
    }
}