using System.Diagnostics;
using System.Text.Json;
using Axon.Modules.Chat.Domain.Types;
using Axon.Modules.Chat.Infrastructure.Ai.Abstractions;
using Axon.Modules.Chat.Infrastructure.Ai.Models;
using Microsoft.Extensions.Logging;

namespace Axon.Modules.Chat.Infrastructure.Ai.Services;

/// <summary>
/// Service responsible for extracting tool execution data from OpenAI responses
/// </summary>
public sealed class ToolExecutionExtractor : IToolExecutionExtractor
{
    private readonly ILogger<ToolExecutionExtractor> _logger;
    private const string UnknownToolName = "unknown_tool";

    public ToolExecutionExtractor(ILogger<ToolExecutionExtractor> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Extract tool execution information from OpenAI Responses API response
    /// </summary>
    /// <param name="response">OpenAI API response</param>
    /// <param name="totalDuration">Total processing duration</param>
    /// <param name="activity">Activity for tracing</param>
    /// <returns>Array of tool executions or null if no tools were executed</returns>
    public ToolExecution[]? ExtractToolExecutions(
        ResponsesApiResponse response, 
        TimeSpan totalDuration, 
        Activity? activity)
    {
        // Check if response contains MCP tool calls
        if (response.McpCalls == null || response.McpCalls.Length == 0)
        {
            return null;
        }

        var toolExecutions = new List<ToolExecution>();
        var averageExecutionTime = TimeSpan.FromMilliseconds(totalDuration.TotalMilliseconds / response.McpCalls.Length);
        
        foreach (var mcpCall in response.McpCalls)
        {
            var toolExecution = mcpCall.Error != null
                ? ToolExecution.Failure(
                    toolName: mcpCall.ToolName ?? UnknownToolName,
                    arguments: JsonSerializer.Serialize(mcpCall.Arguments ?? new object()),
                    errorMessage: mcpCall.Error,
                    executionTime: averageExecutionTime)
                : ToolExecution.Success(
                    toolName: mcpCall.ToolName ?? UnknownToolName,
                    arguments: JsonSerializer.Serialize(mcpCall.Arguments ?? new object()),
                    result: JsonSerializer.Serialize(mcpCall.Output ?? ""),
                    executionTime: averageExecutionTime);
                
            toolExecutions.Add(toolExecution);
            
            _logger.LogDebug(
                "MCP tool execution: {ToolName} -> {Status} in ~{Duration}ms",
                mcpCall.ToolName,
                mcpCall.Error != null ? "Failed" : "Success",
                averageExecutionTime.TotalMilliseconds);
        }
        
        activity?.SetTag("tools.executed", toolExecutions.Count);
        activity?.SetTag("tools.successful", toolExecutions.Count(t => t.IsSuccess));
        activity?.SetTag("tools.failed", toolExecutions.Count(t => !t.IsSuccess));
        
        return toolExecutions.ToArray();
    }
}