using System.Diagnostics;

namespace Axon.Modules.Chat.Application.Abstractions;

/// <summary>
/// Service for tracking activities and telemetry across infrastructure operations
/// </summary>
public interface IActivityTracker
{
    /// <summary>
    /// Sets a tag on the current activity
    /// </summary>
    void SetTag(Activity? activity, string key, object? value);
    
    /// <summary>
    /// Sets multiple tags on the current activity
    /// </summary>
    void SetTags(Activity? activity, params (string Key, object? Value)[] tags);
    
    /// <summary>
    /// Marks an activity as having an error
    /// </summary>
    void MarkError(Activity? activity, Exception? exception = null);
    
    /// <summary>
    /// Sets performance metrics on an activity
    /// </summary>
    void SetPerformanceMetrics(Activity? activity, TimeSpan duration, int? itemCount = null);
}