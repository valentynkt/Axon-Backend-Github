using System.Diagnostics;
using Axon.Modules.Chat.Domain.Types;
using Axon.Modules.Chat.Infrastructure.Ai.Models;

namespace Axon.Modules.Chat.Infrastructure.Ai.Abstractions;

/// <summary>
/// Service for extracting tool execution data from OpenAI responses
/// </summary>
public interface IToolExecutionExtractor
{
    /// <summary>
    /// Extract tool execution information from OpenAI Responses API response
    /// </summary>
    /// <param name="response">OpenAI API response</param>
    /// <param name="totalDuration">Total processing duration</param>
    /// <param name="activity">Activity for tracing</param>
    /// <returns>Array of tool executions or null if no tools were executed</returns>
    ToolExecution[]? ExtractToolExecutions(
        ResponsesApiResponse response, 
        TimeSpan totalDuration, 
        Activity? activity);
}