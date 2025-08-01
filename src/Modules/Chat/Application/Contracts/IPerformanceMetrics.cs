namespace Axon.Modules.Chat.Application.Contracts;

/// <summary>
/// Contract for performance metrics - to be implemented by monitoring-specialist
/// </summary>
public interface IPerformanceMetrics
{
    /// <summary>
    /// Record message processing start
    /// </summary>
    /// <param name="messageId">Message identifier</param>
    /// <param name="messageLength">Length of message</param>
    void RecordProcessingStart(string messageId, int messageLength);

    /// <summary>
    /// Record message processing completion
    /// </summary>
    /// <param name="messageId">Message identifier</param>
    /// <param name="success">Whether processing succeeded</param>
    /// <param name="toolCount">Number of tools executed</param>
    /// <param name="duration">Processing duration</param>
    void RecordProcessingComplete(string messageId, bool success, int toolCount, TimeSpan duration);

    /// <summary>
    /// Record cache hit/miss
    /// </summary>
    /// <param name="messageHash">Message hash</param>
    /// <param name="hit">Whether cache was hit</param>
    void RecordCacheMetrics(string messageHash, bool hit);
}