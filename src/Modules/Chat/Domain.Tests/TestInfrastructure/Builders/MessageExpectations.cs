namespace Axon.Modules.Chat.Domain.Tests.TestInfrastructure.Builders;

/// <summary>
/// Static methods to compute canonical expectations for message content and metadata.
/// Ensures test assertions match exactly what the domain should produce.
/// </summary>
public static class MessageExpectations
{
    /// <summary>
    /// Computes the expected preview for message content per domain rules.
    /// Exactly 100 chars max, plain substring, no ellipsis.
    /// </summary>
    public static string PreviewOf(string content)
    {
        if (string.IsNullOrEmpty(content))
            return string.Empty;
        
        return content.Length <= 100 ? content : content[..100];
    }

    /// <summary>
    /// Determines if a string length is valid for message content.
    /// Valid range: 1..100,000 characters (after trimming).
    /// </summary>
    public static bool IsValidLength(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
            return false;
        
        var trimmedLength = content.Trim().Length;
        return trimmedLength >= 1 && trimmedLength <= 100_000;
    }

    /// <summary>
    /// Computes the expected next sequence number given current message count.
    /// Sequences are 1-based: if count=0, next=1; if count=5, next=6.
    /// </summary>
    public static int ExpectedNextSequence(int currentCount)
    {
        return currentCount + 1;
    }

    /// <summary>
    /// Determines if title length is valid for user-provided titles.
    /// Valid range: 1..200 characters (after trimming).
    /// Note: Empty titles are allowed only on conversation start, not updates.
    /// </summary>
    public static bool IsValidTitleLength(string title)
    {
        if (string.IsNullOrWhiteSpace(title))
            return false;
        
        var trimmedLength = title.Trim().Length;
        return trimmedLength >= 1 && trimmedLength <= 200;
    }

    /// <summary>
    /// Determines if title is valid for conversation start (empty allowed).
    /// </summary>
    public static bool IsValidForStart(string? title)
    {
        if (string.IsNullOrEmpty(title))
            return true;
        
        var trimmedLength = title.Trim().Length;
        return trimmedLength <= 200;
    }

    /// <summary>
    /// Computes expected preview length for given content length.
    /// </summary>
    public static int ExpectedPreviewLength(int contentLength)
    {
        return Math.Min(100, contentLength);
    }
}