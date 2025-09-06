namespace Axon.Modules.Identity.Domain.ValueObjects;

/// <summary>
/// Optional classification tag for wallets.
/// Must belong to predefined allow-list.
/// Reserved for future policies - no domain behavior depends on tags in MVP.
/// </summary>
[ValueObject<string>(
    conversions: Conversions.SystemTextJson | Conversions.TypeConverter | Conversions.EfCoreValueConverter)]
public readonly partial struct Tag
{
    private static readonly HashSet<string> AllowedTags = new(StringComparer.OrdinalIgnoreCase)
    {
        // Wallet purpose
        "personal",
        "business", 
        "trading",
        "defi",
        "gaming",
        "nft",
        "dao",
        
        // Wallet status
        "test",
        "main",
        
        // Security level
        "hot",
        "cold"
    };

    public static readonly Tag Personal = From("personal");
    public static readonly Tag Business = From("business");
    public static readonly Tag Trading = From("trading");
    public static readonly Tag Defi = From("defi");
    public static readonly Tag Gaming = From("gaming");
    public static readonly Tag Nft = From("nft");
    public static readonly Tag Dao = From("dao");
    public static readonly Tag Test = From("test");
    public static readonly Tag Main = From("main");
    public static readonly Tag Hot = From("hot");
    public static readonly Tag Cold = From("cold");

    private static Validation Validate(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return Validation.Invalid("Tag cannot be empty.");

        var normalized = input.ToLowerInvariant().Trim();

        if (normalized.Length < 2)
            return Validation.Invalid("Tag must be at least 2 characters long.");

        if (normalized.Length > 50)
            return Validation.Invalid("Tag cannot exceed 50 characters.");

        // Allow alphanumeric, hyphens, and underscores
        if (!IsValidTagFormat(normalized))
            return Validation.Invalid("Tag must contain only lowercase letters, numbers, hyphens, and underscores.");

        // Must be in allow-list
        if (!AllowedTags.Contains(normalized))
            return Validation.Invalid($"Tag '{normalized}' is not allowed. Allowed tags: {string.Join(", ", AllowedTags)}");

        return Validation.Ok;
    }

    private static string NormalizeInput(string input) => input.ToLowerInvariant().Trim();

    private static bool IsValidTagFormat(string tag)
    {
        return tag.All(c => char.IsLetterOrDigit(c) || c == '-' || c == '_');
    }

    public static Result<Tag, Error> Create(string? value)
    {
        if (value is null)
            return Result.Failure<Tag, Error>(
                Error.Validation("Tag is required.", "WALLET.TAG.REQUIRED"));

        try
        {
            var vo = From(value);
            return Result.Success<Tag, Error>(vo);
        }
        catch (ValueObjectValidationException ex)
        {
            return Result.Failure<Tag, Error>(
                Error.Validation(ex.Message, "WALLET.TAG.INVALID"));
        }
    }

    /// <summary>
    /// Gets all allowed tags.
    /// </summary>
    public static IReadOnlySet<string> GetAllowedTags() => AllowedTags.ToHashSet(StringComparer.OrdinalIgnoreCase);
}