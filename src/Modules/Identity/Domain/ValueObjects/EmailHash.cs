using System.Security.Cryptography;
using System.Text;

namespace Axon.Modules.Identity.Domain.ValueObjects;

/// <summary>
/// Represents a hashed email address for privacy-preserving storage.
/// </summary>
public sealed record EmailHash
{
    public string Value { get; }

    private EmailHash(string value)
    {
        Value = value;
    }

    public static EmailHash FromEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
            throw new ArgumentException("Email cannot be null or empty.", nameof(email));

        var normalizedEmail = email.Trim().ToLowerInvariant();
        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(normalizedEmail));
        var hashString = Convert.ToHexString(hashBytes).ToLowerInvariant();
        
        return new EmailHash(hashString);
    }

    public static EmailHash From(string hashedValue)
    {
        if (string.IsNullOrWhiteSpace(hashedValue))
            throw new ArgumentException("EmailHash value cannot be null or empty.", nameof(hashedValue));

        return new EmailHash(hashedValue.ToLowerInvariant());
    }

    public static implicit operator string(EmailHash emailHash) => emailHash.Value;

    public override string ToString() => Value;
}