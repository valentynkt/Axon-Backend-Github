namespace Axon.Modules.Identity.Domain.ValueObjects;

/// <summary>
/// Static validation helpers for simple string-based domain types.
/// Replaces complex value objects with simple validation methods.
/// </summary>
public static class ValidationHelpers
{
    /// <summary>
    /// Validates a chain identifier.
    /// </summary>
    public static Result<string, Error> ValidateChainId(string? chainId)
    {
        if (string.IsNullOrWhiteSpace(chainId))
            return Result.Failure<string, Error>(
                Error.Validation("Chain ID cannot be empty.", "IDENTITY.CHAIN.ID.EMPTY"));

        var normalized = chainId.Trim().ToLowerInvariant();
        
        if (normalized.Length < 2 || normalized.Length > 50)
            return Result.Failure<string, Error>(
                Error.Validation("Chain ID must be between 2 and 50 characters.", "IDENTITY.CHAIN.ID.INVALID_LENGTH"));

        if (!IsValidChainIdFormat(normalized))
            return Result.Failure<string, Error>(
                Error.Validation("Chain ID contains invalid characters.", "IDENTITY.CHAIN.ID.INVALID_FORMAT"));

        return Result.Success<string, Error>(normalized);
    }

    /// <summary>
    /// Validates a tag value.
    /// </summary>
    public static Result<string, Error> ValidateTag(string? tag)
    {
        if (string.IsNullOrWhiteSpace(tag))
            return Result.Failure<string, Error>(
                Error.Validation("Tag cannot be empty.", "IDENTITY.TAG.EMPTY"));

        var normalized = tag.Trim().ToLowerInvariant();
        
        if (normalized.Length < 2 || normalized.Length > 50)
            return Result.Failure<string, Error>(
                Error.Validation("Tag must be between 2 and 50 characters.", "IDENTITY.TAG.INVALID_LENGTH"));

        if (!IsValidTagFormat(normalized))
            return Result.Failure<string, Error>(
                Error.Validation("Tag contains invalid characters. Only letters, numbers, hyphens allowed.", "IDENTITY.TAG.INVALID_FORMAT"));

        return Result.Success<string, Error>(normalized);
    }

    private static bool IsValidChainIdFormat(string chainId)
    {
        // Allow alphanumeric and common chain naming patterns
        return chainId.All(c => char.IsLetterOrDigit(c) || c == '-' || c == '_');
    }

    private static bool IsValidTagFormat(string tag)
    {
        // Allow alphanumeric and hyphens for tags
        return tag.All(c => char.IsLetterOrDigit(c) || c == '-');
    }
}