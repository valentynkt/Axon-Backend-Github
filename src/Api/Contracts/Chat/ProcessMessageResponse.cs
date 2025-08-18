// File: /Users/valentynkit/Repos/Axon-Backend/src/Api/Contracts/Chat/ProcessMessageResponse.cs
namespace Axon.Api.Contracts.Chat;

/// <summary>Assistant reply for a chat turn.</summary>
public sealed class ProcessMessageResponse
{
    /// <summary>Echo of whether the call succeeded.</summary>
    public bool Success { get; init; }

    /// <summary>Assistant text content (may be empty on error).</summary>
    public string Content { get; init; } = string.Empty;

    /// <summary>OpenAI Responses API response id (use it as PreviousResponseId next time).</summary>
    public string? ResponseId { get; init; }

    /// <summary>Server timestamp (UTC).</summary>
    public DateTime Timestamp { get; init; }

    /// <summary>Optional diagnostic message.</summary>
    public string? Message { get; init; }
}