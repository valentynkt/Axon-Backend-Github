using System.Globalization;

namespace Axon.Modules.Identity.Domain.ValueObjects;

/// <summary>
/// Preferred language using ISO 639-1 language codes.
/// </summary>
[ValueObject<string>(
    conversions: Conversions.SystemTextJson | Conversions.TypeConverter | Conversions.EfCoreValueConverter)]
public readonly partial struct PreferredLanguage
{
    public static readonly PreferredLanguage English = From("en");
    public static readonly PreferredLanguage Spanish = From("es");
    public static readonly PreferredLanguage French = From("fr");
    public static readonly PreferredLanguage German = From("de");
    public static readonly PreferredLanguage Japanese = From("ja");
    public static readonly PreferredLanguage Korean = From("ko");
    public static readonly PreferredLanguage Chinese = From("zh");

    private static Validation Validate(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return Validation.Invalid("Language code cannot be empty.");

        var normalized = input.ToLowerInvariant().Trim();

        if (normalized.Length != 2)
            return Validation.Invalid($"Language code must be 2 characters long. Got: {input}");

        // Validate against ISO 639-1 codes
        try
        {
            var cultureInfo = CultureInfo.GetCultureInfo(normalized);
            if (cultureInfo.TwoLetterISOLanguageName != normalized)
                return Validation.Invalid($"Invalid ISO 639-1 language code: {input}");
        }
        catch (CultureNotFoundException)
        {
            return Validation.Invalid($"Invalid ISO 639-1 language code: {input}");
        }

        return Validation.Ok;
    }

    private static string NormalizeInput(string input) => input.ToLowerInvariant().Trim();

    public static PreferredLanguage Default => English;

    public bool IsEnglish => Value == English.Value;
    public bool IsSpanish => Value == Spanish.Value;
    public bool IsFrench => Value == French.Value;

    public static Result<PreferredLanguage, Error> Create(string? value)
    {
        if (value is null)
            return Result.Success<PreferredLanguage, Error>(Default);

        try
        {
            var vo = From(value);
            return Result.Success<PreferredLanguage, Error>(vo);
        }
        catch (ValueObjectValidationException ex)
        {
            return Result.Failure<PreferredLanguage, Error>(
                Error.Validation(ex.Message, "IDENTITY.PROFILE.LANGUAGE.INVALID"));
        }
    }
}