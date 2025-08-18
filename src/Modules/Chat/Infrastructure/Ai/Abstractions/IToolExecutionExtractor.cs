using System.Diagnostics;
using Axon.Modules.Chat.Domain.Types;
using Axon.Modules.Chat.Infrastructure.Ai.Models;

namespace Axon.Modules.Chat.Infrastructure.Ai.Abstractions;

/// <summary>
/// Extracts tool executions from responses
/// </summary>
public interface IToolExecutionExtractor
{
    /// <summary>
    /// Extract tool executions from response
    /// </summary>
    ToolExecution[]? ExtractToolExecutions(ResponsesApiResponse response, TimeSpan duration, Activity? activity);
}