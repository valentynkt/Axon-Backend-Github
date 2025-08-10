using System.Text;

namespace Axon.Modules.Chat.Domain.Tests.EdgeCases.TestData;

/// <summary>
/// Helper for generating strings of exact lengths for boundary testing.
/// </summary>
public static class StringGenerators
{
    /// <summary>
    /// Generates a string of exact length using repeating characters.
    /// </summary>
    public static string OfLength(int length, char fillChar = 'A')
    {
        if (length < 0) throw new ArgumentOutOfRangeException(nameof(length));
        if (length == 0) return string.Empty;
        
        return new string(fillChar, length);
    }

    /// <summary>
    /// Generates a string of exact length with varied content to avoid uniform patterns.
    /// </summary>
    public static string OfLengthVaried(int length)
    {
        if (length < 0) throw new ArgumentOutOfRangeException(nameof(length));
        if (length == 0) return string.Empty;
        
        var sb = new StringBuilder(length);
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789 ";
        
        for (int i = 0; i < length; i++)
        {
            sb.Append(chars[i % chars.Length]);
        }
        
        return sb.ToString();
    }

    // Specific generators for boundary values
    public static class Title
    {
        public static string Length1 => OfLength(1);
        public static string Length200 => OfLength(200);
        public static string Length201 => OfLength(201);
    }

    public static class Content
    {
        public static string Length1 => OfLength(1);
        public static string Length99 => OfLength(99);
        public static string Length100 => OfLength(100);
        public static string Length101 => OfLength(101);
        public static string Length100K => OfLengthVaried(100_000);
        public static string Length100001 => OfLengthVaried(100_001);
    }
}