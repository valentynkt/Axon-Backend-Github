using BuildingBlocks.Core.Functional.Results;
using BuildingBlocks.Core.Functional;

namespace BuildingBlocks.Core.Abstractions.Events;

/// <summary>
/// Central service for event replay and recovery operations.
/// Created for Epic 06 Story 04 - Event Replay & Recovery Service.
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
    Task<Result<ReplayOperation>> StartReplayAsync(
        ReplayRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get real-time status and progress of ongoing replay operation.
    /// Provides comprehensive progress information including processed counts, errors, and ETA.
    /// </summary>
    /// <param name="replayOperationId">Unique identifier of the replay operation</param>
    /// <param name="cancellationToken">Cancellation token for async operation</param>
    /// <returns>Result containing detailed replay status or error information</returns>
    Task<Result<ReplayStatus>> GetReplayStatusAsync(
        Guid replayOperationId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Stop ongoing replay operation gracefully with proper cleanup.
    /// Ensures in-flight operations complete and resources are properly released.
    /// </summary>
    /// <param name="replayOperationId">Unique identifier of the replay operation to stop</param>
    /// <param name="cancellationToken">Cancellation token for async operation</param>
    /// <returns>Result indicating success or detailed failure information</returns>
    Task<Result<Unit>> StopReplayAsync(
        Guid replayOperationId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get replay operation history and comprehensive statistics.
    /// Useful for auditing, monitoring, and operational insights.
    /// </summary>
    /// <param name="request">History query parameters with filtering and pagination</param>
    /// <param name="cancellationToken">Cancellation token for async operation</param>
    /// <returns>Result containing list of historical replay operations</returns>
    Task<Result<IReadOnlyList<ReplayOperation>>> GetReplayHistoryAsync(
        ReplayHistoryRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Validate replay request without executing (dry run mode).
    /// Performs comprehensive validation including permission checks, data integrity verification,
    /// and safety constraint validation without actual event processing.
    /// </summary>
    /// <param name="request">Replay request to validate</param>
    /// <param name="cancellationToken">Cancellation token for async operation</param>
    /// <returns>Result containing validation results with detailed analysis</returns>
    Task<Result<ReplayValidationResult>> ValidateReplayAsync(
        ReplayRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get comprehensive service health status including performance metrics and operational status.
    /// Used for health checks, monitoring, and operational diagnostics.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token for async operation</param>
    /// <returns>Result containing detailed health information</returns>
    Task<Result<ReplayServiceHealth>> GetHealthAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Cancel all running replay operations gracefully.
    /// Emergency operation for system maintenance or critical situations.
    /// </summary>
    /// <param name="reason">Reason for cancelling all operations</param>
    /// <param name="cancellationToken">Cancellation token for async operation</param>
    /// <returns>Result with information about cancelled operations</returns>
    Task<Result<ReplayCancellationResult>> CancelAllReplaysAsync(
        string reason,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get detailed metrics for replay operations over specified time period.
    /// Provides comprehensive analytics for performance monitoring and optimization.
    /// </summary>
    /// <param name="request">Metrics query parameters with time range and filtering</param>
    /// <param name="cancellationToken">Cancellation token for async operation</param>
    /// <returns>Result containing detailed replay metrics and analytics</returns>
    Task<Result<ReplayMetrics>> GetReplayMetricsAsync(
        ReplayMetricsRequest request,
        CancellationToken cancellationToken = default);
}