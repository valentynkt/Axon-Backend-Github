using Axon.Modules.Chat.Application.Commands.ProcessMessage;

namespace Axon.Tests.Shared.Builders;

/// <summary>
/// Builder for creating ProcessMessageCommand instances for testing using the Mother Object pattern
/// </summary>
public class ProcessMessageCommandBuilder
{
    private string _message = "Default test message";
    private string? _mcpServerUrl;
    private Dictionary<string, string>? _mcpHeaders;
    private string[]? _allowedTools;
    private string? _previousResponseId;

    /// <summary>
    /// Sets the message content
    /// </summary>
    public ProcessMessageCommandBuilder WithMessage(string message)
    {
        _message = message;
        return this;
    }

    /// <summary>
    /// Sets a simple message for basic scenarios
    /// </summary>
    public ProcessMessageCommandBuilder WithSimpleMessage() => WithMessage("Hello, AI!");

    /// <summary>
    /// Sets a complex message for advanced scenarios
    /// </summary>
    public ProcessMessageCommandBuilder WithComplexMessage() => 
        WithMessage("Please analyze this data and provide insights using available tools.");

    /// <summary>
    /// Sets an empty message (for validation testing)
    /// </summary>
    public ProcessMessageCommandBuilder WithEmptyMessage() => WithMessage("");

    /// <summary>
    /// Sets a null message (for validation testing)
    /// </summary>
    public ProcessMessageCommandBuilder WithNullMessage() => WithMessage(null!);

    /// <summary>
    /// Sets a whitespace-only message (for validation testing)
    /// </summary>
    public ProcessMessageCommandBuilder WithWhitespaceMessage() => WithMessage("   ");

    /// <summary>
    /// Sets the MCP server URL
    /// </summary>
    public ProcessMessageCommandBuilder WithMcpServerUrl(string url)
    {
        _mcpServerUrl = url;
        return this;
    }

    /// <summary>
    /// Sets a valid MCP server URL
    /// </summary>
    public ProcessMessageCommandBuilder WithValidMcpServer() => 
        WithMcpServerUrl("https://api.example.com/mcp");

    /// <summary>
    /// Sets an invalid MCP server URL (for validation testing)
    /// </summary>
    public ProcessMessageCommandBuilder WithInvalidMcpServer() => 
        WithMcpServerUrl("not-a-valid-url");

    /// <summary>
    /// Sets MCP headers
    /// </summary>
    public ProcessMessageCommandBuilder WithMcpHeaders(Dictionary<string, string> headers)
    {
        _mcpHeaders = headers;
        return this;
    }

    /// <summary>
    /// Sets authentication headers for MCP
    /// </summary>
    public ProcessMessageCommandBuilder WithAuthHeaders() => 
        WithMcpHeaders(new Dictionary<string, string>
        {
            { "Authorization", "Bearer test-token" },
            { "X-API-Key", "test-api-key" }
        });

    /// <summary>
    /// Sets custom headers for MCP
    /// </summary>
    public ProcessMessageCommandBuilder WithCustomHeaders(string key, string value) => 
        WithMcpHeaders(new Dictionary<string, string> { { key, value } });

    /// <summary>
    /// Sets allowed tools
    /// </summary>
    public ProcessMessageCommandBuilder WithAllowedTools(params string[] tools)
    {
        _allowedTools = tools;
        return this;
    }

    /// <summary>
    /// Sets common allowed tools
    /// </summary>
    public ProcessMessageCommandBuilder WithCommonTools() => 
        WithAllowedTools("weather", "calendar", "search");

    /// <summary>
    /// Sets a single allowed tool
    /// </summary>
    public ProcessMessageCommandBuilder WithSingleTool(string tool) => 
        WithAllowedTools(tool);

    /// <summary>
    /// Sets no allowed tools (empty array)
    /// </summary>
    public ProcessMessageCommandBuilder WithNoTools() => 
        WithAllowedTools();

    /// <summary>
    /// Sets the previous response ID
    /// </summary>
    public ProcessMessageCommandBuilder WithPreviousResponseId(string responseId)
    {
        _previousResponseId = responseId;
        return this;
    }

    /// <summary>
    /// Sets a valid previous response ID
    /// </summary>
    public ProcessMessageCommandBuilder WithValidPreviousResponse() => 
        WithPreviousResponseId(Guid.NewGuid().ToString());

    /// <summary>
    /// Sets up a complete MCP configuration
    /// </summary>
    public ProcessMessageCommandBuilder WithFullMcpConfiguration() =>
        WithValidMcpServer()
        .WithAuthHeaders()
        .WithCommonTools()
        .WithValidPreviousResponse();

    /// <summary>
    /// Sets up a minimal valid command
    /// </summary>
    public ProcessMessageCommandBuilder AsMinimalValid() => WithSimpleMessage();

    /// <summary>
    /// Sets up a command for error testing
    /// </summary>
    public ProcessMessageCommandBuilder AsInvalid() => WithEmptyMessage();

    /// <summary>
    /// Builds the ProcessMessageCommand
    /// </summary>
    public ProcessMessageCommand Build() => new(
        Message: _message,
        McpServerUrl: _mcpServerUrl,
        McpHeaders: _mcpHeaders,
        AllowedTools: _allowedTools,
        PreviousResponseId: _previousResponseId);

    /// <summary>
    /// Creates a new builder instance (fluent interface)
    /// </summary>
    public static ProcessMessageCommandBuilder New() => new();

    /// <summary>
    /// Creates a builder with a specific message
    /// </summary>
    public static ProcessMessageCommandBuilder ForMessage(string message) => 
        new ProcessMessageCommandBuilder().WithMessage(message);

    /// <summary>
    /// Creates a builder for a minimal valid command
    /// </summary>
    public static ProcessMessageCommandBuilder MinimalValid() => 
        new ProcessMessageCommandBuilder().AsMinimalValid();

    /// <summary>
    /// Creates a builder for a complete MCP-enabled command
    /// </summary>  
    public static ProcessMessageCommandBuilder WithMcp() => 
        new ProcessMessageCommandBuilder().WithFullMcpConfiguration();
}