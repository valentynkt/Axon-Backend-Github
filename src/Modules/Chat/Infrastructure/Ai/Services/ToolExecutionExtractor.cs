using System.Diagnostics;
using Axon.Modules.Chat.Domain.Types;
using Axon.Modules.Chat.Infrastructure.Ai.Abstractions;
using Axon.Modules.Chat.Infrastructure.Ai.Models;

namespace Axon.Modules.Chat.Infrastructure.Ai.Services;

/// <summary>
/// Tool execution extractor implementation
/// </summary>
public sealed class ToolExecutionExtractor : IToolExecutionExtractor
{
    public ToolExecution[]? ExtractToolExecutions(ResponsesApiResponse response, TimeSpan duration, Activity? activity)
    {
        // For POC, return null - tool execution parsing can be implemented later
        return null;
    }
}