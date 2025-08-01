using System.Diagnostics;
using Axon.Modules.Chat.Application.DTOs;

namespace Axon.Modules.Chat.Infrastructure.Ai.Abstractions;

/// <summary>
/// Service for managing observability and logging during OpenAI operations
/// </summary>
public interface IActivityTracker
{
    /// <summary>
    /// Starts a new activity for message processing
    /// </summary>
    /// <param name="request">AI request being processed</param>
    /// <returns>Started activity</returns>
    Activity? StartActivity(AiRequest request);

    /// <summary>
    /// Sets response-related tags on the activity
    /// </summary>
    /// <param name="activity">Activity to tag</param>
    /// <param name="response">AI response</param>
    /// <param name="duration">Processing duration</param>
    void TagResponse(Activity? activity, AiResponse response, TimeSpan duration);

    /// <summary>
    /// Sets error tag on the activity
    /// </summary>
    /// <param name="activity">Activity to tag</param>
    void TagError(Activity? activity);

    /// <summary>
    /// Logs successful message processing
    /// </summary>
    /// <param name="model">OpenAI model used</param>
    /// <param name="mcpServerCount">Number of MCP servers</param>
    void LogProcessingStart(string model, int mcpServerCount);

    /// <summary>
    /// Logs successful message processing completion
    /// </summary>
    /// <param name="duration">Processing duration</param>
    /// <param name="toolCount">Number of tool executions</param>
    void LogProcessingSuccess(TimeSpan duration, int toolCount);

    /// <summary>
    /// Logs processing failure
    /// </summary>
    /// <param name="ex">Exception that occurred</param>
    /// <param name="duration">Processing duration before failure</param>
    void LogProcessingFailure(Exception ex, TimeSpan duration);
}