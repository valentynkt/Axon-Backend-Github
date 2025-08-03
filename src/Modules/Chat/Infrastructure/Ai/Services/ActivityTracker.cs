using System.Diagnostics;
using Axon.Modules.Chat.Application.DTOs;
using Axon.Modules.Chat.Infrastructure.Ai.Abstractions;
using Microsoft.Extensions.Logging;

namespace Axon.Modules.Chat.Infrastructure.Ai.Services;

/// <summary>
/// Service responsible for managing observability and logging during OpenAI operations
/// </summary>
public sealed class ActivityTracker : IActivityTracker
{
    private static readonly ActivitySource ActivitySource = new("Axon.Chat.Infrastructure.OpenAi");
    private readonly ILogger<ActivityTracker> _logger;

    // LoggerMessage delegates for CA1848 compliance
    private static readonly Action<ILogger, string, int, Exception?> LogProcessingStartAction =
        LoggerMessage.Define<string, int>(
            LogLevel.Information,
            new EventId(9001, "LogProcessingStart"),
            "Processing message with OpenAI Responses API (Direct MCP) using model {Model} and {McpServerCount} MCP servers");
            
    private static readonly Action<ILogger, double, int, Exception?> LogProcessingSuccessAction =
        LoggerMessage.Define<double, int>(
            LogLevel.Information,
            new EventId(9002, "LogProcessingSuccess"),
            "Successfully processed message in {Duration}ms with {ToolCount} tool executions using Direct MCP");
            
    private static readonly Action<ILogger, double, string, Exception?> LogProcessingFailureAction =
        LoggerMessage.Define<double, string>(
            LogLevel.Error,
            new EventId(9003, "LogProcessingFailure"),
            "Failed to process message with Direct MCP after {Duration}ms: {Error}");

    public ActivityTracker(ILogger<ActivityTracker> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Starts a new activity for message processing
    /// </summary>
    /// <param name="request">AI request being processed</param>
    /// <returns>Started activity</returns>
    public Activity? StartActivity(AiRequest request)
    {
        var activity = ActivitySource.StartActivity("ProcessMessage");
        activity?.SetTag("mcp.server_count", request.McpConfigs?.Count ?? 0);
        activity?.SetTag("message.length", request.Message.Length);
        
        return activity;
    }

    /// <summary>
    /// Sets response-related tags on the activity
    /// </summary>
    /// <param name="activity">Activity to tag</param>
    /// <param name="response">AI response</param>
    /// <param name="duration">Processing duration</param>
    public void TagResponse(Activity? activity, AiResponse response, TimeSpan duration)
    {
        activity?.SetTag("response.length", response.Content.Length);
        activity?.SetTag("duration.ms", duration.TotalMilliseconds);
        activity?.SetTag("response.id", response.ResponseId);
    }

    /// <summary>
    /// Sets error tag on the activity
    /// </summary>
    /// <param name="activity">Activity to tag</param>
    public void TagError(Activity? activity)
    {
        activity?.SetTag("error", true);
    }

    /// <summary>
    /// Logs successful message processing
    /// </summary>
    /// <param name="model">OpenAI model used</param>
    /// <param name="mcpServerCount">Number of MCP servers</param>
    /// <param name="duration">Processing duration</param>
    /// <param name="toolCount">Number of tool executions</param>
    public void LogProcessingStart(string model, int mcpServerCount)
    {
        LogProcessingStartAction(_logger, model, mcpServerCount, null);
    }

    /// <summary>
    /// Logs successful message processing completion
    /// </summary>
    /// <param name="duration">Processing duration</param>
    /// <param name="toolCount">Number of tool executions</param>
    public void LogProcessingSuccess(TimeSpan duration, int toolCount)
    {
        LogProcessingSuccessAction(_logger, duration.TotalMilliseconds, toolCount, null);
    }

    /// <summary>
    /// Logs processing failure
    /// </summary>
    /// <param name="ex">Exception that occurred</param>
    /// <param name="duration">Processing duration before failure</param>
    public void LogProcessingFailure(Exception ex, TimeSpan duration)
    {
        LogProcessingFailureAction(_logger, duration.TotalMilliseconds, ex.Message, ex);
    }
}