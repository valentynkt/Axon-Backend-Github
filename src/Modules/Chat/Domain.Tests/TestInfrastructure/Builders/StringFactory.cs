using System.Text;

namespace Axon.Modules.Chat.Domain.Tests.TestInfrastructure.Builders;

/// <summary>
/// Deterministic content generators for property-based testing.
/// Provides consistent, repeatable string generation for boundary testing.
/// </summary>
public static class StringFactory
{
    private const string AlphaNumChars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
    private const string LoremChunk = "Lorem ipsum dolor sit amet consectetur adipiscing elit sed do eiusmod tempor incididunt ut labore et dolore magna aliqua ut enim ad minim veniam quis nostrud exercitation ullamco ";

    /// <summary>
    /// Creates a string of exactly the specified length using the given character.
    /// </summary>
    public static string OfLength(int n, char c = 'a')
    {
        if (n < 0) throw new ArgumentOutOfRangeException(nameof(n));
        if (n == 0) return string.Empty;
        return new string(c, n);
    }

    /// <summary>
    /// Creates a string of exactly the specified length using alphanumeric characters.
    /// Content is deterministic based on length for consistent testing.
    /// </summary>
    public static string AlphaNum(int n)
    {
        if (n < 0) throw new ArgumentOutOfRangeException(nameof(n));
        if (n == 0) return string.Empty;

        var sb = new StringBuilder(n);
        for (int i = 0; i < n; i++)
        {
            sb.Append(AlphaNumChars[i % AlphaNumChars.Length]);
        }
        return sb.ToString();
    }

    /// <summary>
    /// Creates a lorem ipsum style string of exactly the specified length.
    /// Reuses chunks efficiently to avoid large memory allocations.
    /// </summary>
    public static string Lorem(int n)
    {
        if (n < 0) throw new ArgumentOutOfRangeException(nameof(n));
        if (n == 0) return string.Empty;

        var sb = new StringBuilder(n);
        int remaining = n;
        
        while (remaining > 0)
        {
            int takeLength = Math.Min(remaining, LoremChunk.Length);
            sb.Append(LoremChunk, 0, takeLength);
            remaining -= takeLength;
        }
        
        return sb.ToString();
    }    // Named boundary generators for common test cases
    public static class Boundaries
    {
        // Content boundaries
        public static string Len1() => OfLength(1);
        public static string Len100() => OfLength(100);
        public static string Len101() => OfLength(101);
        public static string Len100k() => AlphaNum(100_000);
        public static string Len100kPlus1() => AlphaNum(100_001);

        // Title boundaries  
        public static string TitleLen1() => OfLength(1);
        public static string TitleLen200() => AlphaNum(200);
        public static string TitleLen201() => AlphaNum(201);

        // Preview testing
        public static string PreviewLen99() => AlphaNum(99);
        public static string PreviewLen100() => AlphaNum(100);
        public static string PreviewLen101() => AlphaNum(101);
        public static string PreviewLen150() => AlphaNum(150);
    }

    /// <summary>
    /// Generates strings with specific patterns for edge case testing.
    /// </summary>
    public static class Patterns
    {
        /// <summary>
        /// Creates a string with only whitespace characters.
        /// </summary>
        public static string Whitespace(int n) => new string(' ', n);

        /// <summary>
        /// Creates a string with mixed whitespace types.
        /// </summary>
        public static string MixedWhitespace(int n)
        {
            if (n == 0) return string.Empty;
            
            var whitespaceChars = new[] { ' ', '\t', '\n', '\r' };
            var sb = new StringBuilder(n);
            
            for (int i = 0; i < n; i++)
            {
                sb.Append(whitespaceChars[i % whitespaceChars.Length]);
            }
            
            return sb.ToString();
        }

        /// <summary>
        /// Creates a string with leading and trailing whitespace around core content.
        /// </summary>
        public static string WithTrimming(string core, int leadingSpaces = 3, int trailingSpaces = 3)
        {
            return new string(' ', leadingSpaces) + core + new string(' ', trailingSpaces);
        }
    }
}