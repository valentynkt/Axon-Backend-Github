using System;

namespace Axon.Modules.Chat.Domain.Internal.Text;

/// <summary>
/// Internal text utility methods optimized for allocation hygiene and EF-friendly patterns.
/// Used by domain hot paths to minimize string allocations and ensure consistent normalization.
/// </summary>
internal static class TextSlices
{
    /// <summary>
    /// Creates a preview of the source string, optimized for minimal allocations.
    /// If source.Length is less than or equal to maxLen, returns the original string reference (0 allocations).
    /// If source.Length is greater than maxLen, returns a substring (1 allocation).
    /// No trimming or ellipsis is applied.
    /// </summary>
    /// <param name="source">Source string to create preview from</param>
    /// <param name="maxLen">Maximum preview length</param>
    /// <returns>Preview string (same reference if within limit, substring if over limit)</returns>
    /// <exception cref="ArgumentNullException">When source is null</exception>
    public static string Preview(string source, int maxLen)
    {
        ArgumentNullException.ThrowIfNull(source);

        // Allocation-optimal path: return same reference when no truncation needed
        if (source.Length <= maxLen)
            return source;

        // Single allocation path: substring when truncation needed
        return source.Substring(0, maxLen);
    }

    /// <summary>
    /// Normalizes a search term for use as a constant in specifications.
    /// Returns string.Empty for null/whitespace (specs treat as pass-through).
    /// Returns ToLowerInvariant() for non-empty terms.
    /// This should ONLY be used on constants, never on entity fields in expressions.
    /// </summary>
    /// <param name="term">Search term to normalize</param>
    /// <returns>Normalized constant term or string.Empty for pass-through</returns>
    public static string NormalizeForSearchConst(string? term)
    {
        if (string.IsNullOrWhiteSpace(term))
            return string.Empty;

        return term.ToLowerInvariant();
    }
}