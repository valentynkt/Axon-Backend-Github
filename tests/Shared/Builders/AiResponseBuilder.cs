using Axon.Modules.Chat.Application.DTOs;
using Axon.Modules.Chat.Domain.Types;

namespace Axon.Tests.Shared.Builders;

/// <summary>
/// Builder for creating AiResponse instances for testing using the Mother Object pattern
/// </summary>
public class AiResponseBuilder
{
    private string _content = "Default AI response";
    private string? _responseId = Guid.NewGuid().ToString();
    private ToolExecution[]? _toolExecutions;

    /// <summary>
    /// Sets the response content
    /// </summary>
    public AiResponseBuilder WithContent(string content)
    {
        _content = content;
        return this;
    }

    /// <summary>
    /// Sets a simple response content
    /// </summary>
    public AiResponseBuilder WithSimpleResponse() => 
        WithContent("Hello! How can I help you?");

    /// <summary>
    /// Sets a complex response content
    /// </summary>
    public AiResponseBuilder WithComplexResponse() => 
        WithContent("Based on the analysis of your data, I found several interesting patterns that suggest...");

    /// <summary>
    /// Sets an empty response content
    /// </summary>
    public AiResponseBuilder WithEmptyContent() => WithContent("");

    /// <summary>
    /// Sets the response ID
    /// </summary>
    public AiResponseBuilder WithResponseId(string? responseId)
    {
        _responseId = responseId;
        return this;
    }

    /// <summary>
    /// Sets a valid response ID
    /// </summary>
    public AiResponseBuilder WithValidResponseId() => 
        WithResponseId(Guid.NewGuid().ToString());

    /// <summary>
    /// Sets no response ID (null)
    /// </summary>
    public AiResponseBuilder WithoutResponseId() => WithResponseId(null);

    /// <summary>
    /// Sets the tool executions
    /// </summary>
    public AiResponseBuilder WithToolExecutions(params ToolExecution[] executions)
    {
        _toolExecutions = executions;
        return this;
    }

    /// <summary>
    /// Sets a single successful tool execution
    /// </summary>
    public AiResponseBuilder WithSuccessfulTool(string toolName = "weather", string input = "{}", string output = "Sunny, 72°F", int durationMs = 500) =>
        WithToolExecutions(ToolExecution.Success(toolName, input, output, TimeSpan.FromMilliseconds(durationMs)));

    /// <summary>
    /// Sets a single failed tool execution
    /// </summary>
    public AiResponseBuilder WithFailedTool(string toolName = "calendar", string input = "{}", string error = "Service unavailable", int durationMs = 250) =>
        WithToolExecutions(ToolExecution.Failure(toolName, input, error, TimeSpan.FromMilliseconds(durationMs)));

    /// <summary>
    /// Sets multiple tool executions with mixed results
    /// </summary>
    public AiResponseBuilder WithMixedToolExecutions() =>
        WithToolExecutions(
            ToolExecution.Success("weather", "{\"location\":\"NYC\"}", "Cloudy, 68°F", TimeSpan.FromMilliseconds(750)),
            ToolExecution.Failure("calendar", "{\"date\":\"today\"}", "Calendar not available", TimeSpan.FromMilliseconds(250)),
            ToolExecution.Success("search", "{\"query\":\"restaurants\"}", "Found 10 restaurants", TimeSpan.FromMilliseconds(1200))
        );

    /// <summary>
    /// Sets multiple successful tool executions
    /// </summary>
    public AiResponseBuilder WithMultipleSuccessfulTools() =>
        WithToolExecutions(
            ToolExecution.Success("tool1", "{}", "result1", TimeSpan.FromMilliseconds(100)),
            ToolExecution.Success("tool2", "{}", "result2", TimeSpan.FromMilliseconds(200)),
            ToolExecution.Success("tool3", "{}", "result3", TimeSpan.FromMilliseconds(150))
        );

    /// <summary>
    /// Sets no tool executions (null)
    /// </summary>
    public AiResponseBuilder WithoutToolExecutions() => WithToolExecutions();

    /// <summary>
    /// Sets up a successful response with tool execution
    /// </summary>
    public AiResponseBuilder AsSuccessfulWithTools() =>
        WithSimpleResponse()
        .WithValidResponseId()
        .WithSuccessfulTool();

    /// <summary>
    /// Sets up a successful response without tools
    /// </summary>
    public AiResponseBuilder AsSuccessfulWithoutTools() =>
        WithSimpleResponse()
        .WithValidResponseId()
        .WithoutToolExecutions();

    /// <summary>
    /// Sets up a response for error scenarios
    /// </summary>
    public AiResponseBuilder AsErrorResponse() =>
        WithContent("I apologize, but I encountered an error processing your request.")
        .WithValidResponseId()
        .WithFailedTool();

    /// <summary>
    /// Builds the AiResponse
    /// </summary>
    public AiResponse Build() => new(
        Content: _content,
        ResponseId: _responseId,
        ToolExecutions: _toolExecutions);

    /// <summary>
    /// Creates a new builder instance (fluent interface)
    /// </summary>
    public static AiResponseBuilder New() => new();

    /// <summary>
    /// Creates a builder with specific content
    /// </summary>
    public static AiResponseBuilder ForContent(string content) => 
        new AiResponseBuilder().WithContent(content);

    /// <summary>
    /// Creates a builder for a successful response
    /// </summary>
    public static AiResponseBuilder Successful() => 
        new AiResponseBuilder().AsSuccessfulWithoutTools();

    /// <summary>
    /// Creates a builder for a successful response with tools
    /// </summary>
    public static AiResponseBuilder SuccessfulWithTools() => 
        new AiResponseBuilder().AsSuccessfulWithTools();

    /// <summary>
    /// Creates a builder for an error response
    /// </summary>
    public static AiResponseBuilder Error() => 
        new AiResponseBuilder().AsErrorResponse();
}