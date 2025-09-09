namespace Axon.Modules.Identity.Domain.ValueObjects;

/// <summary>
/// Represents a cryptographic proof type for wallet ownership verification.
/// </summary>
public sealed record ProofType
{
    public string Value { get; }

    private ProofType(string value)
    {
        Value = value;
    }

    public static ProofType From(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("ProofType cannot be null or empty.", nameof(value));

        return new ProofType(value.ToLowerInvariant().Trim());
    }

    // Common proof types
    public static ProofType Signature => From("signature");
    public static ProofType Message => From("message");
    public static ProofType Transaction => From("transaction");

    public static implicit operator string(ProofType proofType) => proofType.Value;
    public static implicit operator ProofType(string value) => From(value);

    public override string ToString() => Value;
}