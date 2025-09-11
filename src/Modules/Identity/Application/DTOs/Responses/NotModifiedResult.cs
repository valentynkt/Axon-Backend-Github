namespace Axon.Modules.Identity.Application.DTOs.Responses;

/// <summary>
/// Special response type indicating that content has not been modified
/// and HTTP 304 Not Modified should be returned.
/// Used for efficient ETag-based caching support.
/// </summary>
public sealed record NotModifiedResult
{
    public static readonly NotModifiedResult Instance = new();
    
    private NotModifiedResult() { }
}