using System.Diagnostics;
using Axon.Modules.Chat.Application.DTOs;

namespace Axon.Modules.Chat.Infrastructure.Ai.Abstractions;

/// <summary>
/// Tracks activities for observability
/// </summary>
public interface IActivityTracker
{
    /// <summary>
    /// Start tracking an activity
    /// </summary>
    Activity? StartActivity(AiRequest request);
    
    /// <summary>
    /// Tag an activity
    /// </summary>
    void TagResponse(Activity? activity, AiResponse response, TimeSpan duration);
    
    /// <summary>
    /// Tag error
    /// </summary>
    void TagError(Activity? activity);
    
    /// <summary>
    /// Log processing start
    /// </summary>
    void LogProcessingStart(string model, int mcpServerCount);
    
    /// <summary>
    /// Log processing success
    /// </summary>
    void LogProcessingSuccess(TimeSpan duration, int toolCount);
    
    /// <summary>
    /// Log processing failure
    /// </summary>
    void LogProcessingFailure(Exception ex, TimeSpan duration);
}