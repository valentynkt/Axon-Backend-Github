using Axon.Shared.Common;

namespace Axon.Modules.Chat.Domain.ValueObjects;

/// <summary>
/// Strongly-typed value object for MCP server URLs with validation
/// </summary>
public readonly record struct McpServerUrl
{
    public Uri Value { get; }

    private McpServerUrl(Uri value) => Value = value;

    /// <summary>
    /// Creates a McpServerUrl from a string URL with validation
    /// </summary>
    /// <param name="url">The URL string to validate</param>
    /// <returns>Result containing McpServerUrl or validation error</returns>
    public static Result<McpServerUrl> Create(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return Error.Validation("MCP server URL cannot be null or empty");
            
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            return Error.Validation("MCP server URL must be a valid absolute URL");
            
        if (uri.Scheme != "https")
            return Error.Validation("MCP server URL must use HTTPS scheme for security");
            
        return new McpServerUrl(uri);
    }

    /// <summary>
    /// Creates a McpServerUrl from a Uri with validation
    /// </summary>
    /// <param name="uri">The Uri to validate</param>
    /// <returns>Result containing McpServerUrl or validation error</returns>
    public static Result<McpServerUrl> Create(Uri? uri)
    {
        if (uri is null)
            return Error.Validation("MCP server URI cannot be null");
            
        if (!uri.IsAbsoluteUri)
            return Error.Validation("MCP server URI must be absolute");
            
        if (uri.Scheme != "https")
            return Error.Validation("MCP server URI must use HTTPS scheme for security");
            
        return new McpServerUrl(uri);
    }

    public override string ToString() => Value.ToString();
    
    public static implicit operator Uri(McpServerUrl mcpServerUrl) => mcpServerUrl.Value;
    public static implicit operator string(McpServerUrl mcpServerUrl) => mcpServerUrl.Value.ToString();
}