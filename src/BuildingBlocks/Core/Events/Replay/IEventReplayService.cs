namespace BuildingBlocks.Core.Events.Replay;

/// <summary>
/// Service interface for replaying domain events.
/// Created for Epic 06 Story 04 - Event Replay and Recovery Service.
/// Provides comprehensive event replay capabilities with monitoring and recovery features.
/// </summary>
public interface IEventReplayService
{
    /// <summary>
    /// Start a new replay operation with specified configuration.
    /// </summary>
    /// <param name="request">Replay configuration and parameters</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Result containing the operation ID if successful</returns>
    Task<Result<Guid>> StartReplayAsync(
        ReplayRequest request, 
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get the current status of a replay operation.
    /// </summary>
    /// <param name="operationId">ID of the replay operation</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Current status information</returns>
    Task<Result<ReplayStatus>> GetStatusAsync(
        Guid operationId, 
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get detailed progress information for a replay operation.
    /// </summary>
    /// <param name="operationId">ID of the replay operation</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Detailed progress information</returns>
    Task<Result<ReplayProgress>> GetProgressAsync(
        Guid operationId, 
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get performance metrics for a replay operation.
    /// </summary>
    /// <param name="operationId">ID of the replay operation</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Performance metrics and statistics</returns>
    Task<Result<ReplayPerformanceMetrics>> GetMetricsAsync(
        Guid operationId, 
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Pause a running replay operation.
    /// </summary>
    /// <param name="operationId">ID of the replay operation</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Success result if paused successfully</returns>
    Task<Result> PauseAsync(
        Guid operationId, 
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Resume a paused replay operation.
    /// </summary>
    /// <param name="operationId">ID of the replay operation</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Success result if resumed successfully</returns>
    Task<Result> ResumeAsync(
        Guid operationId, 
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Cancel a replay operation.
    /// </summary>
    /// <param name="operationId">ID of the replay operation</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Success result if cancelled successfully</returns>
    Task<Result> CancelAsync(
        Guid operationId, 
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get a list of all active replay operations.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of active replay operations</returns>
    Task<Result<IReadOnlyList<ReplayStatus>>> GetActiveOperationsAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get replay history within a specified time range.
    /// </summary>
    /// <param name="fromUtc">Start time (UTC)</param>
    /// <param name="toUtc">End time (UTC)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Historical replay information</returns>
    Task<Result<IReadOnlyList<ReplayResult>>> GetHistoryAsync(
        DateTime fromUtc, 
        DateTime toUtc,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Clean up completed replay operations and their associated data.
    /// </summary>
    /// <param name="olderThanUtc">Clean up operations older than this timestamp</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Number of operations cleaned up</returns>
    Task<Result<int>> CleanupAsync(
        DateTime olderThanUtc,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get diagnostic information for troubleshooting.
    /// </summary>
    /// <param name="operationId">ID of the replay operation</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Diagnostic information</returns>
    Task<Result<ReplayDiagnosticInfo>> GetDiagnosticsAsync(
        Guid operationId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Validate a replay request without executing it.
    /// </summary>
    /// <param name="request">Replay request to validate</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Validation result with any errors</returns>
    Task<Validation<Unit>> ValidateRequestAsync(
        ReplayRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get health status of the replay service.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Service health information</returns>
    Task<Result<ReplayServiceHealth>> GetServiceHealthAsync(
        CancellationToken cancellationToken = default);
}