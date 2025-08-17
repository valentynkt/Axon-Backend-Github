using Axon.Modules.Chat.Domain.Types;

namespace Axon.Modules.Chat.Application.Abstractions;

/// <summary>
/// Service for creating tool execution instances while maintaining clean architecture boundaries
/// </summary>
public interface IToolExecutionService
{
    /// <summary>
    /// Creates a successful tool execution
    /// </summary>
    ToolExecution CreateSuccess(string toolName, string arguments, string result, TimeSpan executionTime);
    
    /// <summary>
    /// Creates a failed tool execution
    /// </summary>
    ToolExecution CreateFailure(string toolName, string arguments, string errorMessage, TimeSpan executionTime);
    
    /// <summary>
    /// Extracts tool executions from MCP response data
    /// </summary>
    ToolExecution[] ExtractToolExecutions(object[] mcpCalls, TimeSpan totalDuration);
    
    /// <summary>
    /// Extracts tool executions from MCP call items with proper typing
    /// </summary>
    ToolExecution[] ExtractFromMcpCalls<T>(T[] mcpCalls, TimeSpan totalDuration) where T : class;
}