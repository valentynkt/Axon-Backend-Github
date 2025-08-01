using System.Linq.Expressions;
using Axon.Modules.Chat.Domain.ValueObjects;
using Axon.Shared.Domain;

namespace Axon.Modules.Chat.Domain.Specifications;

/// <summary>
/// Specification that determines if an MCP server configuration is valid and enabled
/// </summary>
public sealed class McpServerIsEnabledSpecification : Specification<McpServerConfiguration>
{
    public override Expression<Func<McpServerConfiguration, bool>> ToExpression()
    {
        return config =>
            config.IsEnabled &&
            !string.IsNullOrWhiteSpace(config.Url.ToString()) &&
            config.Url.ToString().StartsWith("http");
    }
}

/// <summary>
/// Specification that determines if an MCP server configuration is secure (HTTPS)
/// </summary>
public sealed class McpServerIsSecureSpecification : Specification<McpServerConfiguration>
{
    public override Expression<Func<McpServerConfiguration, bool>> ToExpression()
    {
        return config => config.Url.ToString().StartsWith("https://");
    }
}

/// <summary>
/// Specification that determines if an MCP server has required capabilities
/// </summary>
public sealed class McpServerHasRequiredCapabilitiesSpecification : Specification<McpServerConfiguration>
{
    private readonly string[] _requiredCapabilities;

    public McpServerHasRequiredCapabilitiesSpecification(params string[] requiredCapabilities)
    {
        _requiredCapabilities = requiredCapabilities ?? Array.Empty<string>();
    }

    public override Expression<Func<McpServerConfiguration, bool>> ToExpression()
    {
        return config => _requiredCapabilities.All(capability => 
            config.Capabilities.Contains(capability));
    }
}

/// <summary>
/// Value object representing MCP server configuration for specifications
/// </summary>
public sealed record McpServerConfiguration
{
    public McpServerUrl Url { get; }
    public bool IsEnabled { get; }
    public IReadOnlySet<string> Capabilities { get; }
    public string? ApiKey { get; }
    public TimeSpan Timeout { get; }

    public McpServerConfiguration(
        McpServerUrl url,
        bool isEnabled = true,
        IReadOnlySet<string>? capabilities = null,
        string? apiKey = null,
        TimeSpan? timeout = null)
    {
        Url = url;
        IsEnabled = isEnabled;
        Capabilities = capabilities ?? new HashSet<string>();
        ApiKey = apiKey;
        Timeout = timeout ?? TimeSpan.FromSeconds(30);
    }

    /// <summary>
    /// Creates an enabled MCP server configuration
    /// </summary>
    public static McpServerConfiguration CreateEnabled(
        McpServerUrl url,
        IReadOnlySet<string>? capabilities = null,
        string? apiKey = null,
        TimeSpan? timeout = null)
    {
        return new McpServerConfiguration(url, true, capabilities, apiKey, timeout);
    }

    /// <summary>
    /// Creates a disabled MCP server configuration
    /// </summary>
    public static McpServerConfiguration CreateDisabled(McpServerUrl url)
    {
        return new McpServerConfiguration(url, false);
    }

    /// <summary>
    /// Determines if this configuration supports a specific capability
    /// </summary>
    public bool SupportsCapability(string capability)
    {
        return Capabilities.Contains(capability);
    }

    /// <summary>
    /// Determines if this configuration is ready for use
    /// </summary>
    public bool IsReady => IsEnabled && !string.IsNullOrWhiteSpace(Url.ToString());
}