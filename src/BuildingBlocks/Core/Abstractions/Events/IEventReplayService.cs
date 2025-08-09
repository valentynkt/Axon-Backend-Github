using BuildingBlocks.Core.Functional.Results;
using BuildingBlocks.Core.Functional;
using BuildingBlocks.Core.Events.Replay;

namespace BuildingBlocks.Core.Abstractions.Events;

/// <summary>
/// Central service for event replay and recovery operations.
/// Created for Epic 06 Story 04 - Event Replay and Recovery Service.
/// Provides comprehensive event replay capabilities with safety controls, progress monitoring,
/// and validation mechanisms for reliable event system recovery operations.
/// </summary>
public interface IEventReplayService
{
    /// <summary>
    /// Start event replay operation with specified criteria and safety controls.
    /// Validates replay request and initiates asynchronous replay processing with progress tracking.
    /// </summary>
    /// <param name="request">Comprehensive replay parameters with filtering and execution options</param>
    /// <param name="cancellationToken">Cancellation token for async operation</param>
    /// <returns>Result containing replay operation information or detailed error</returns>
    Task<Result<Guid>> StartReplayAsync(
        ReplayRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get real-time status and progress of ongoing replay operation.
    /// Provides comprehensive progress information including processed counts, errors, and ETA.
    /// </summary>
    /// <param name="operationId">Unique identifier of the replay operation</param>
    /// <param name="cancellationToken">Cancellation token for async operation</param>
    /// <returns>Result containing detailed replay status or error information</returns>
    Task<Result<ReplayStatus>> GetStatusAsync(
        Guid operationId,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get detailed progress information for a replay operation.
    /// </summary>
    /// <param name="operationId">Unique identifier of the replay operation</param>
    /// <param name="cancellationToken">Cancellation token for async operation</param>
    /// <returns>Detailed progress information</returns>
    Task<Result<ReplayProgress>> GetProgressAsync(
        Guid operationId,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get performance metrics for a replay operation.
    /// </summary>
    /// <param name="operationId">Unique identifier of the replay operation</param>
    /// <param name="cancellationToken">Cancellation token for async operation</param>
    /// <returns>Performance metrics</returns>
    Task<Result<ReplayPerformanceMetrics>> GetMetricsAsync(
        Guid operationId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Pause an ongoing replay operation.
    /// </summary>
    /// <param name="operationId">Unique identifier of the replay operation</param>
    /// <param name="cancellationToken">Cancellation token for async operation</param>
    /// <returns>Result indicating success or failure</returns>
    Task<Result> PauseAsync(
        Guid operationId,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Resume a paused replay operation.
    /// </summary>
    /// <param name="operationId">Unique identifier of the replay operation</param>
    /// <param name="cancellationToken">Cancellation token for async operation</param>
    /// <returns>Result indicating success or failure</returns>
    Task<Result> ResumeAsync(
        Guid operationId,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Cancel a replay operation.
    /// </summary>
    /// <param name="operationId">Unique identifier of the replay operation</param>
    /// <param name="cancellationToken">Cancellation token for async operation</param>
    /// <returns>Result indicating success or failure</returns>
    Task<Result> CancelAsync(
        Guid operationId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get active replay operations.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token for async operation</param>
    /// <returns>Result containing list of active operations</returns>
    Task<Result<IReadOnlyList<ReplayStatus>>> GetActiveOperationsAsync(
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get replay operation history.
    /// </summary>
    /// <param name="fromUtc">Start time for history query</param>
    /// <param name="toUtc">End time for history query</param>
    /// <param name="cancellationToken">Cancellation token for async operation</param>
    /// <returns>Result containing list of historical replay operations</returns>
    Task<Result<IReadOnlyList<ReplayResult>>> GetHistoryAsync(
        DateTime fromUtc,
        DateTime toUtc,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Cleanup old replay operations.
    /// </summary>
    /// <param name="olderThanUtc">Remove operations older than this date</param>
    /// <param name="cancellationToken">Cancellation token for async operation</param>
    /// <returns>Number of operations cleaned up</returns>
    Task<Result<int>> CleanupAsync(
        DateTime olderThanUtc,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get diagnostic information for a replay operation.
    /// </summary>
    /// <param name="operationId">Unique identifier of the replay operation</param>
    /// <param name="cancellationToken">Cancellation token for async operation</param>
    /// <returns>Diagnostic information</returns>
    Task<Result<ReplayDiagnosticInfo>> GetDiagnosticsAsync(
        Guid operationId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Validate replay request without executing.
    /// </summary>
    /// <param name="request">Replay request to validate</param>
    /// <param name="cancellationToken">Cancellation token for async operation</param>
    /// <returns>Validation result</returns>
    Task<Validation<Unit>> ValidateRequestAsync(
        ReplayRequest request,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get service health status.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token for async operation</param>
    /// <returns>Service health information</returns>
    Task<Result<ReplayServiceHealth>> GetServiceHealthAsync(
        CancellationToken cancellationToken = default);
}