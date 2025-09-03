namespace Axon.Modules.Identity.Application.Services;

/// <summary>
/// Service for tracking metrics and telemetry data for Dynamic JWT exchange operations.
/// Provides insights into exchange success rates, wallet processing, and error patterns.
/// </summary>
public interface IExchangeMetricsService
{
    /// <summary>
    /// Records a successful JWT exchange operation
    /// </summary>
    /// <param name="userId">The Dynamic user ID</param>
    /// <param name="created">Whether a new principal was created</param>
    /// <param name="walletsProcessed">Number of wallets processed</param>
    /// <param name="walletsLinked">Number of wallets successfully linked</param>
    /// <param name="conflicts">Number of wallet ownership conflicts</param>
    /// <param name="processingTimeMs">Total processing time in milliseconds</param>
    void RecordExchangeSuccess(string userId, bool created, int walletsProcessed, int walletsLinked, int conflicts, long processingTimeMs);

    /// <summary>
    /// Records a failed JWT exchange operation
    /// </summary>
    /// <param name="errorCode">The error code that caused the failure</param>
    /// <param name="errorType">The type of error (validation, unauthorized, etc.)</param>
    /// <param name="processingTimeMs">Processing time before failure in milliseconds</param>
    void RecordExchangeFailure(string errorCode, string errorType, long processingTimeMs);

    /// <summary>
    /// Records JWT validation metrics
    /// </summary>
    /// <param name="validationTimeMs">JWT validation time in milliseconds</param>
    /// <param name="cacheHit">Whether the validation used cached data</param>
    void RecordJwtValidation(long validationTimeMs, bool cacheHit);

    /// <summary>
    /// Records JWKS fetch metrics
    /// </summary>
    /// <param name="fetchTimeMs">JWKS fetch time in milliseconds</param>
    /// <param name="cacheHit">Whether JWKS was served from cache</param>
    /// <param name="retryCount">Number of retries needed (0 for first attempt success)</param>
    void RecordJwksFetch(long fetchTimeMs, bool cacheHit, int retryCount);

    /// <summary>
    /// Records wallet processing metrics
    /// </summary>
    /// <param name="chain">The blockchain chain</param>
    /// <param name="operation">The operation (activity_upsert, link, default_set)</param>
    /// <param name="success">Whether the operation succeeded</param>
    /// <param name="processingTimeMs">Operation processing time in milliseconds</param>
    void RecordWalletOperation(string chain, string operation, bool success, long processingTimeMs);

    /// <summary>
    /// Records replay attack detection
    /// </summary>
    /// <param name="jti">The JWT ID that was replayed (for logging correlation)</param>
    void RecordReplayAttempt(string jti);
}