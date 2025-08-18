using System.Diagnostics;

namespace Axon.Modules.Chat.Application.Abstractions;

/// <summary>
/// Application layer activity tracker
/// </summary>
public interface IActivityTracker
{
    /// <summary>
    /// Set tags on activity
    /// </summary>
    void SetTags(Activity? activity, params (string key, object value)[] tags);
    
    /// <summary>
    /// Mark activity as error
    /// </summary>
    void MarkError(Activity? activity, Exception exception);
}