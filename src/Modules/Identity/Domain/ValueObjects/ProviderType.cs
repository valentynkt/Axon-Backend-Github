namespace Axon.Modules.Identity.Domain.ValueObjects;

/// <summary>
/// Identity provider type for credentials.
/// </summary>
[ValueObject<string>(
    conversions: Conversions.SystemTextJson | Conversions.TypeConverter | Conversions.EfCoreValueConverter)]
public readonly partial struct ProviderType
{
    private static readonly string[] AllowedValues = { "dynamic", "siws", "oidc", "service_api" };

    public static readonly ProviderType Dynamic = From("dynamic");
    public static readonly ProviderType Siws = From("siws");
    public static readonly ProviderType Oidc = From("oidc");
    public static readonly ProviderType ServiceApi = From("service_api");

    private static Validation Validate(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return Validation.Invalid("Provider type cannot be empty.");

        var normalized = input.ToLowerInvariant();
        if (!AllowedValues.Contains(normalized))
            return Validation.Invalid($"Invalid provider type: {input}. Must be one of: {string.Join(", ", AllowedValues)}.");

        return Validation.Ok;
    }

    private static string NormalizeInput(string input) => input.ToLowerInvariant();

    public bool IsDynamic => Value == Dynamic.Value;
    public bool IsSiws => Value == Siws.Value;
    public bool IsOidc => Value == Oidc.Value;
    public bool IsServiceApi => Value == ServiceApi.Value;

    public static Result<ProviderType, Error> Create(string? value)
    {
        if (value is null)
            return Result.Failure<ProviderType, Error>(
                Error.Validation("Provider type is required.", "IDENTITY.CREDENTIAL.PROVIDER.REQUIRED"));

        try
        {
            var vo = From(value);
            return Result.Success<ProviderType, Error>(vo);
        }
        catch (ValueObjectValidationException ex)
        {
            return Result.Failure<ProviderType, Error>(
                Error.Validation(ex.Message, "IDENTITY.CREDENTIAL.PROVIDER.INVALID"));
        }
    }
}