using BuildingBlocks.Core.Diagnostics.Errors;
using CSharpFunctionalExtensions;
using Vogen;

namespace Axon.Modules.Identity.Domain.ValueObjects;

/// <summary>
/// Represents a cryptographic proof type for wallet ownership verification.
/// Creation: <c>ProofType.From("...")</c> (throws on invalid)
/// Non-throwing: <c>ProofType.Create("...")</c> (returns Result)
/// JSON: STJ converter generated
/// EF Core: value converter generated  
/// TypeConverter: generated (useful for binding, config, etc.)
/// </summary>
[ValueObject<string>(
    conversions: Conversions.SystemTextJson | Conversions.TypeConverter | Conversions.EfCoreValueConverter)]
public readonly partial struct ProofType
{
    // Supported proof types (can be extended)
    private static readonly HashSet<string> SupportedProofTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "signature",
        "message",
        "transaction"
    };

    // Vogen will call this before Validate and before storing the value
    private static string NormalizeInput(string input) => input.Trim().ToLowerInvariant();

    // Vogen passes the normalized input here
    private static Validation Validate(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return Validation.Invalid("ProofType cannot be null or empty.");

        if (!SupportedProofTypes.Contains(input))
            return Validation.Invalid($"Unsupported proof type: {input}");

        return Validation.Ok;
    }

    /// <summary>
    /// Non-throwing factory bridging Vogen to CFE <c>Result</c>.
    /// Preferred in application layer to avoid exception-based control flow.
    /// </summary>
    public static Result<ProofType, Error> Create(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return Result.Failure<ProofType, Error>(
                Error.Validation("ProofType cannot be null or empty.", "PROOF.TYPE.EMPTY"));
        }

        var normalized = NormalizeInput(value);
        
        if (!SupportedProofTypes.Contains(normalized))
        {
            return Result.Failure<ProofType, Error>(
                Error.Validation($"Unsupported proof type: {value}", "PROOF.TYPE.UNSUPPORTED"));
        }

        // Use generated TryParse; provider null is fine
        return TryParse(value, provider: null, out var vo)
            ? Result.Success<ProofType, Error>(vo)
            : Result.Failure<ProofType, Error>(
                Error.Validation($"Invalid proof type: {value}", "PROOF.TYPE.INVALID"));
    }

    // Common proof types
    public static ProofType Signature => From("signature");
    public static ProofType Message => From("message");
    public static ProofType Transaction => From("transaction");

    public static bool IsSupported(string proofType)
    {
        return !string.IsNullOrWhiteSpace(proofType) && 
               SupportedProofTypes.Contains(proofType.Trim().ToLowerInvariant());
    }

    public static IEnumerable<string> GetSupportedProofTypes() => SupportedProofTypes;

    public override string ToString() => Value;
}