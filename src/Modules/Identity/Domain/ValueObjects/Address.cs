namespace Axon.Modules.Identity.Domain.ValueObjects;

/// <summary>
/// Simple normalized wallet address value object.
/// </summary>
[ValueObject<string>(
    conversions: Conversions.SystemTextJson | Conversions.TypeConverter | Conversions.EfCoreValueConverter)]
public readonly partial struct Address
{
    private static Validation Validate(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return Validation.Invalid("Address cannot be empty.");

        var trimmed = input.Trim();
        
        if (trimmed.Length < 10)
            return Validation.Invalid("Address too short.");

        if (trimmed.Length > 200)
            return Validation.Invalid("Address too long.");

        return Validation.Ok;
    }

    private static string NormalizeInput(string input) => input.Trim();
}