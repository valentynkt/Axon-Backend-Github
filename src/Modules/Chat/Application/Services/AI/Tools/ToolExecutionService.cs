using System.Text.Json;
using Axon.Modules.Chat.Application.Abstractions;
using Axon.Modules.Chat.Domain.Types;
using Microsoft.Extensions.Logging;
using System.Reflection;

namespace Axon.Modules.Chat.Application.Services.AI.Tools;

/// <summary>
/// Implementation of tool execution service maintaining clean architecture boundaries
/// </summary>
public sealed class ToolExecutionService : IToolExecutionService
{
    private const string UnknownToolName = "unknown_tool";
    private readonly ILogger<ToolExecutionService> _logger;

    // LoggerMessage delegate for improved performance
    private static readonly Action<ILogger, string, string, double, Exception?> McpToolExecutionLogged = 
        LoggerMessage.Define<string, string, double>(
            LogLevel.Debug,
            new EventId(1, nameof(McpToolExecutionLogged)),
            "MCP tool execution: {ToolName} -> {Status} in ~{Duration}ms");

    public ToolExecutionService(ILogger<ToolExecutionService> logger)
    {
        _logger = logger;
    }

    public ToolExecution CreateSuccess(string toolName, string arguments, string result, TimeSpan executionTime)
    {
        return ToolExecution.Success(toolName, arguments, result, executionTime);
    }

    public ToolExecution CreateFailure(string toolName, string arguments, string errorMessage, TimeSpan executionTime)
    {
        return ToolExecution.Failure(toolName, arguments, errorMessage, executionTime);
    }

    public ToolExecution[] ExtractToolExecutions(object[] mcpCalls, TimeSpan totalDuration)
    {
        if (mcpCalls == null || mcpCalls.Length == 0)
        {
            return Array.Empty<ToolExecution>();
        }

        var toolExecutions = new List<ToolExecution>();
        var averageExecutionTime = TimeSpan.FromMilliseconds(totalDuration.TotalMilliseconds / mcpCalls.Length);

        foreach (var mcpCall in mcpCalls)
        {
            // Extract properties using reflection to avoid Infrastructure type dependencies
            var mcpCallType = mcpCall.GetType();
            var toolName = mcpCallType.GetProperty("ToolName")?.GetValue(mcpCall)?.ToString() ?? UnknownToolName;
            var arguments = mcpCallType.GetProperty("Arguments")?.GetValue(mcpCall);
            var error = mcpCallType.GetProperty("Error")?.GetValue(mcpCall)?.ToString();
            var output = mcpCallType.GetProperty("Output")?.GetValue(mcpCall);

            var argumentsJson = JsonSerializer.Serialize(arguments ?? new object());

            var toolExecution = error != null
                ? CreateFailure(toolName, argumentsJson, error, averageExecutionTime)
                : CreateSuccess(toolName, argumentsJson, JsonSerializer.Serialize(output ?? ""), averageExecutionTime);

            toolExecutions.Add(toolExecution);

            McpToolExecutionLogged(_logger, toolName, error != null ? "Failed" : "Success", averageExecutionTime.TotalMilliseconds, null);
        }

        return toolExecutions.ToArray();
    }
    
    public ToolExecution[] ExtractFromMcpCalls<T>(T[] mcpCalls, TimeSpan totalDuration) where T : class
    {
        return ExtractToolExecutions(mcpCalls.Cast<object>().ToArray(), totalDuration);
    }
}