using System.Diagnostics;
using Axon.Modules.Chat.Application.Abstractions;

namespace Axon.Modules.Chat.Application.Services;

/// <summary>
/// Implementation of activity tracker service for telemetry operations
/// </summary>
public sealed class ActivityTracker : IActivityTracker
{
    public void SetTag(Activity? activity, string key, object? value)
    {
        activity?.SetTag(key, value);
    }

    public void SetTags(Activity? activity, params (string Key, object? Value)[] tags)
    {
        ArgumentNullException.ThrowIfNull(tags);
        
        if (activity == null) return;

        foreach (var (key, value) in tags)
        {
            activity.SetTag(key, value);
        }
    }

    public void MarkError(Activity? activity, Exception? exception = null)
    {
        activity?.SetTag("error", true);
        if (exception != null)
        {
            activity?.SetTag("error.message", exception.Message);
            activity?.SetTag("error.type", exception.GetType().Name);
        }
    }

    public void SetPerformanceMetrics(Activity? activity, TimeSpan duration, int? itemCount = null)
    {
        activity?.SetTag("duration.ms", duration.TotalMilliseconds);
        if (itemCount.HasValue)
        {
            activity?.SetTag("items.count", itemCount.Value);
        }
    }
}