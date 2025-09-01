using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace Axon.Modules.Identity.Domain.ValueObjects;

/// <summary>
/// SHA256 hash of an email address, stored in lowercase hex format.
/// Used for privacy-preserving email identification.
/// </summary>
[ValueObject<string>(
    conversions: Conversions.SystemTextJson | Conversions.TypeConverter | Conversions.EfCoreValueConverter)]
public readonly partial struct EmailHash
{
    [GeneratedRegex("^[a-f0-9]{64}$", RegexOptions.Compiled)]
    private static partial Regex HexPattern();

    private static Validation Validate(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return Validation.Invalid("Email hash cannot be empty.");

        var normalized = input.ToLowerInvariant().Trim();

        if (normalized.Length != 64)
            return Validation.Invalid($"Email hash must be exactly 64 characters long. Got: {normalized.Length}");

        if (!HexPattern().IsMatch(normalized))
            return Validation.Invalid("Email hash must be a valid lowercase hexadecimal SHA256 hash.");

        return Validation.Ok;
    }

    private static string NormalizeInput(string input) => input.ToLowerInvariant().Trim();

    /// <summary>
    /// Creates a SHA256 hash from an email address.
    /// </summary>
    public static EmailHash FromEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
            throw new ArgumentException("Email cannot be null or empty.", nameof(email));

        var normalizedEmail = email.ToLowerInvariant().Trim();
        var bytes = Encoding.UTF8.GetBytes(normalizedEmail);
        var hashBytes = SHA256.HashData(bytes);
        var hashString = Convert.ToHexString(hashBytes).ToLowerInvariant();
        
        return From(hashString);
    }

    public static Result<EmailHash, Error> Create(string? value)
    {
        if (value is null)
            return Result.Failure<EmailHash, Error>(
                Error.Validation("Email hash is required.", "IDENTITY.EMAIL.HASH.REQUIRED"));

        try
        {
            var vo = From(value);
            return Result.Success<EmailHash, Error>(vo);
        }
        catch (ValueObjectValidationException ex)
        {
            return Result.Failure<EmailHash, Error>(
                Error.Validation(ex.Message, "IDENTITY.EMAIL.HASH.INVALID"));
        }
    }

    public static Result<EmailHash, Error> CreateFromEmail(string? email)
    {
        if (email is null)
            return Result.Failure<EmailHash, Error>(
                Error.Validation("Email is required.", "IDENTITY.EMAIL.REQUIRED"));

        try
        {
            var hash = FromEmail(email);
            return Result.Success<EmailHash, Error>(hash);
        }
        catch (ArgumentException ex)
        {
            return Result.Failure<EmailHash, Error>(
                Error.Validation(ex.Message, "IDENTITY.EMAIL.INVALID"));
        }
    }
}