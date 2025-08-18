// File: /Users/valentynkit/Repos/Axon-Backend/src/Api/Contracts/Chat/ProcessMessageRequest.cs
using System.ComponentModel.DataAnnotations;

namespace Axon.Api.Contracts.Chat;

/// <summary>
/// One chat turn. If <see cref="PreviousResponseId"/> is null, starts a new conversation;
/// otherwise continues the previous one.
/// </summary>
public sealed class ProcessMessageRequest
{
    /// <summary>User message (required).</summary>
    [Required, MinLength(1), MaxLength(8000)]
    public string Message { get; init; } = string.Empty;

    /// <summary>
    /// The prior OpenAI Responses API response id. If present, the model
    /// will continue from that response; if null, a new conversation starts.
    /// </summary>
    public string? PreviousResponseId { get; init; }

    /// <summary>Enable configured MCP servers (optional).</summary>
    public bool UseMcpServers { get; init; }

    /// <summary>
    /// Optional single MCP server URL override for ad-hoc testing.
    /// If set, this takes precedence over configured servers.
    /// </summary>
    [Url]
    public string? McpServerUrl { get; init; }
}