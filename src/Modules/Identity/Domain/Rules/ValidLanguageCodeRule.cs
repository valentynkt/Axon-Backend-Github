using Axon.Modules.Identity.Domain.ValueObjects;
using System.Globalization;

namespace Axon.Modules.Identity.Domain.Rules;

/// <summary>
/// Ensures that a language code follows ISO 639-1 standard.
/// </summary>
internal sealed class ValidLanguageCodeRule : BusinessRule
{
    private readonly PreferredLanguage _language;

    public ValidLanguageCodeRule(PreferredLanguage language)
        : base(
            message: $"Language code '{language.Value}' is not a valid ISO 639-1 language code.",
            code: "IDENTITY.PROFILE.LANGUAGE.INVALID")
    {
        _language = language;
    }

    public override bool IsBroken()
    {
        try
        {
            var culture = CultureInfo.GetCultureInfo(_language.Value);
            return culture.TwoLetterISOLanguageName != _language.Value;
        }
        catch (CultureNotFoundException)
        {
            return true;
        }
    }

    public override ValueTask<bool> IsBrokenAsync(CancellationToken ct = default) 
        => ValueTask.FromResult(IsBroken());
}