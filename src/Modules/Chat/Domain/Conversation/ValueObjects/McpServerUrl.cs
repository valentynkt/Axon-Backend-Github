using System;
using Axon.Shared.Common;

namespace Axon.Modules.Chat.Domain.ValueObjects
{
    /// <summary>
    /// Represents the base URL of our MCP server, validated as an absolute URI.
    /// </summary>
    public readonly record struct McpServerUrl(string Value)
    {
        public static Result<McpServerUrl> Create(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
                return Error.Validation("MCP Server URL cannot be empty.", "MCP_URL_EMPTY");
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
                return Error.Validation("Invalid MCP Server URL format.", "MCP_URL_INVALID");

            // normalize (no trailing slash)
            var normalized = uri.ToString().TrimEnd('/');
            return new McpServerUrl(normalized);
        }

        public override string ToString() => Value;
        public static implicit operator string(McpServerUrl u) => u.Value;
        public static explicit operator McpServerUrl(string s) => Create(s).Value;
    }
}