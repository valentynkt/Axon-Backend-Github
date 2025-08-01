using Axon.Modules.Chat.Application.Commands.ProcessMessage;
using Axon.Modules.Chat.Application.DTOs;
using Axon.Shared.Common;

namespace Axon.Modules.Chat.Application.Contracts;

/// <summary>
/// Contract for message caching - to be implemented by performance-optimizer
/// </summary>
public interface IMessageCache
{
    /// <summary>
    /// Get cached response for message
    /// </summary>
    /// <param name="messageHash">Hash of the message</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Cached response if available</returns>
    Task<Result<ProcessMessageResponse?>> GetCachedResponseAsync(
        string messageHash, 
        CancellationToken cancellationToken);

    /// <summary>
    /// Cache message response
    /// </summary>
    /// <param name="messageHash">Hash of the message</param>
    /// <param name="response">Response to cache</param>
    /// <param name="ttl">Time to live for cache entry</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Success result</returns>
    Task<Result> CacheResponseAsync(
        string messageHash, 
        ProcessMessageResponse response, 
        TimeSpan ttl,
        CancellationToken cancellationToken);
}