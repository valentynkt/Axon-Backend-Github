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
    
    // LoggerMessage delegates for CA1848 compliance
    private static readonly Action<ILogger, string?, string, double, Exception?> LogToolExecutionAction =
        LoggerMessage.Define<string?, string, double>(
            LogLevel.Debug,
            new EventId(6001, "LogToolExecution"),
            "MCP tool execution: {ToolName} -> {Status} in ~{Duration}ms");
            
    private static readonly Action<ILogger, Exception?> LogMcpToolListFoundAction =
        LoggerMessage.Define(
            LogLevel.Debug,
            new EventId(6002, "LogMcpToolListFound"),
            "Found MCP tool list in response");

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
        // Check if response contains output with tool calls
        if (response.Output == null || response.Output.Length == 0)
        {
            return null;
        }

        var toolExecutions = new List<ToolExecution>();
        
        // Look for MCP tool calls in the output - for now, we just log available tools
        foreach (var outputItem in response.Output)
        {
            if (outputItem.Type == "mcp_list_tools" && outputItem.Content != null)
            {
                LogMcpToolListFoundAction(_logger, null);
                // TODO: Parse and process actual tool executions when the API provides them
            }
            // TODO: Add support for other MCP output types like tool call results
        }
        
        // For now, return null as we're only seeing tool lists, not actual tool executions
        // This will be expanded when we handle actual tool call results
        activity?.SetTag("tools.executed", toolExecutions.Count);
        activity?.SetTag("tools.successful", toolExecutions.Count(t => t.IsSuccess));
        activity?.SetTag("tools.failed", toolExecutions.Count(t => !t.IsSuccess));
        
        return toolExecutions.Count > 0 ? toolExecutions.ToArray() : null;
    }
}