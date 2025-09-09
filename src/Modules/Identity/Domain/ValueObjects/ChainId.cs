namespace Axon.Modules.Identity.Domain.ValueObjects;

/// <summary>
/// Represents a blockchain network chain identifier.
/// </summary>
public sealed record ChainId
{
    public string Value { get; }

    private ChainId(string value)
    {
        Value = value;
    }

    public static ChainId From(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("ChainId cannot be null or empty.", nameof(value));

        return new ChainId(value.Trim());
    }

    public static implicit operator string(ChainId chainId) => chainId.Value;
    public static implicit operator ChainId(string value) => From(value);

    public override string ToString() => Value;
}