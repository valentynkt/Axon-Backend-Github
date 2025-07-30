using System.ComponentModel.DataAnnotations;

namespace Axon.Modules.Chat.Infrastructure.Configuration;

/// <summary>
/// Configuration options for MCP servers collection
/// </summary>
public sealed class McpServersOptions
{
    /// <summary>
    /// Configuration section name
    /// </summary>
    public const string SectionName = "Chat:McpServers";

    /// <summary>
    /// Dictionary of MCP server configurations keyed by server ID
    /// </summary>
    public Dictionary<string, McpServerOptions> Servers { get; init; } = new();

    /// <summary>
    /// Validate all configured servers
    /// </summary>
    /// <returns>Validation results</returns>
    public IEnumerable<ValidationResult> Validate()
{
    foreach (var (serverId, serverOptions) in Servers)
    {
        // Validate server ID format
        if (string.IsNullOrEmpty(serverId))
        {
            yield return new ValidationResult($"Server ID cannot be empty");
            continue;
        }

        if (!IsValidServerId(serverId))
        {
            yield return new ValidationResult(
                $"Server ID '{serverId}' must contain only alphanumeric characters and underscores");
        }

        // Skip validation for disabled servers
        if (!serverOptions.Enabled)
            continue;

        // Validate server options using the custom validation method
        var context = new ValidationContext(serverOptions) { MemberName = serverId };
        foreach (var result in serverOptions.Validate(context))
        {
            yield return new ValidationResult(
                $"Server '{serverId}': {result.ErrorMessage}");
        }
    }
}

    private static bool IsValidServerId(string serverId)
    {
        return serverId.All(c => char.IsLetterOrDigit(c) || c == '_');
    }
}