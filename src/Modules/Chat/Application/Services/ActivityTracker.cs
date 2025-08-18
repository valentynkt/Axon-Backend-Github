using System.Diagnostics;
using Axon.Modules.Chat.Application.Abstractions;

namespace Axon.Modules.Chat.Application.Services;

/// <summary>
/// Simple activity tracker implementation
/// </summary>
public sealed class ActivityTracker : IActivityTracker
{
    public void SetTags(Activity? activity, params (string key, object value)[] tags)
    {
        if (activity == null) return;
        
        foreach (var (key, value) in tags)
        {
            activity.SetTag(key, value);
        }
    }
    
    public void MarkError(Activity? activity, Exception exception)
    {
        activity?.SetStatus(ActivityStatusCode.Error, exception.Message);
    }
}