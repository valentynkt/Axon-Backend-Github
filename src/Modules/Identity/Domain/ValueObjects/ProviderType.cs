namespace Axon.Modules.Identity.Domain.ValueObjects;

/// <summary>
/// Represents an identity provider type (e.g., 'dynamic', 'google', 'github').
/// </summary>
public sealed record ProviderType
{
    public string Value { get; }

    private ProviderType(string value)
    {
        Value = value;
    }

    public static ProviderType From(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("ProviderType cannot be null or empty.", nameof(value));

        return new ProviderType(value.ToLowerInvariant().Trim());
    }

    // Common provider types
    public static ProviderType Dynamic => From("dynamic");
    public static ProviderType Manual => From("manual");


    public static implicit operator string(ProviderType providerType) => providerType.Value;
    public static implicit operator ProviderType(string value) => From(value);

    public override string ToString() => Value;
}