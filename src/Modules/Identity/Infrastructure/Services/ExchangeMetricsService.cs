using System.Diagnostics.Metrics;
using Axon.Modules.Identity.Application.Services;
using Microsoft.Extensions.Logging;

namespace Axon.Modules.Identity.Infrastructure.Services;

/// <summary>
/// Implementation of exchange metrics service using .NET System.Diagnostics.Metrics API.
/// Provides telemetry data for Dynamic JWT exchange operations.
/// </summary>
public sealed class ExchangeMetricsService : IExchangeMetricsService, IDisposable
{
    private readonly Meter _meter;
    private readonly ILogger<ExchangeMetricsService> _logger;

    // Counters
    private readonly Counter<long> _exchangeSuccessCounter;
    private readonly Counter<long> _exchangeFailureCounter;
    private readonly Counter<long> _jwtValidationCounter;
    private readonly Counter<long> _jwksFetchCounter;
    private readonly Counter<long> _walletOperationCounter;
    private readonly Counter<long> _replayAttemptCounter;

    // Histograms for timing
    private readonly Histogram<double> _exchangeProcessingTime;
    private readonly Histogram<double> _jwtValidationTime;
    private readonly Histogram<double> _jwksFetchTime;
    private readonly Histogram<double> _walletOperationTime;

    // Gauges for current state
    private readonly UpDownCounter<long> _activeExchanges;

    public ExchangeMetricsService(ILogger<ExchangeMetricsService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _meter = new Meter("Axon.Identity.DynamicExchange", "1.0.0");

        // Initialize counters
        _exchangeSuccessCounter = _meter.CreateCounter<long>(
            "axon_identity_exchange_success_total",
            description: "Total number of successful JWT exchanges");

        _exchangeFailureCounter = _meter.CreateCounter<long>(
            "axon_identity_exchange_failure_total", 
            description: "Total number of failed JWT exchanges");

        _jwtValidationCounter = _meter.CreateCounter<long>(
            "axon_identity_jwt_validation_total",
            description: "Total number of JWT validation attempts");

        _jwksFetchCounter = _meter.CreateCounter<long>(
            "axon_identity_jwks_fetch_total",
            description: "Total number of JWKS fetch attempts");

        _walletOperationCounter = _meter.CreateCounter<long>(
            "axon_identity_wallet_operation_total",
            description: "Total number of wallet operations");

        _replayAttemptCounter = _meter.CreateCounter<long>(
            "axon_identity_replay_attempt_total",
            description: "Total number of detected JWT replay attempts");

        // Initialize histograms
        _exchangeProcessingTime = _meter.CreateHistogram<double>(
            "axon_identity_exchange_duration_ms",
            unit: "ms",
            description: "Duration of JWT exchange operations");

        _jwtValidationTime = _meter.CreateHistogram<double>(
            "axon_identity_jwt_validation_duration_ms", 
            unit: "ms",
            description: "Duration of JWT validation operations");

        _jwksFetchTime = _meter.CreateHistogram<double>(
            "axon_identity_jwks_fetch_duration_ms",
            unit: "ms", 
            description: "Duration of JWKS fetch operations");

        _walletOperationTime = _meter.CreateHistogram<double>(
            "axon_identity_wallet_operation_duration_ms",
            unit: "ms",
            description: "Duration of wallet operations");

        // Initialize gauges
        _activeExchanges = _meter.CreateUpDownCounter<long>(
            "axon_identity_active_exchanges",
            description: "Number of currently active JWT exchanges");
    }

    public void RecordExchangeSuccess(string userId, bool created, int walletsProcessed, int walletsLinked, int conflicts, long processingTimeMs)
    {
        _exchangeSuccessCounter.Add(1, new KeyValuePair<string, object?>("created", created.ToString().ToLowerInvariant()));
        _exchangeProcessingTime.Record(processingTimeMs, new KeyValuePair<string, object?>("status", "success"));

        _logger.LogDebug("Exchange success recorded: AxonUserId={AxonUserId}, Created={Created}, WalletsProcessed={WalletsProcessed}, WalletsLinked={WalletsLinked}, Conflicts={Conflicts}, Duration={Duration}ms",
            userId, created, walletsProcessed, walletsLinked, conflicts, processingTimeMs);
    }

    public void RecordExchangeFailure(string errorCode, string errorType, long processingTimeMs)
    {
        _exchangeFailureCounter.Add(1, 
            new KeyValuePair<string, object?>("error_code", errorCode),
            new KeyValuePair<string, object?>("error_type", errorType));
        
        _exchangeProcessingTime.Record(processingTimeMs, new KeyValuePair<string, object?>("status", "failure"));

        _logger.LogDebug("Exchange failure recorded: ErrorCode={ErrorCode}, ErrorType={ErrorType}, Duration={Duration}ms", 
            errorCode, errorType, processingTimeMs);
    }

    public void RecordJwtValidation(long validationTimeMs, bool cacheHit)
    {
        _jwtValidationCounter.Add(1, new KeyValuePair<string, object?>("cache_hit", cacheHit.ToString().ToLowerInvariant()));
        _jwtValidationTime.Record(validationTimeMs, new KeyValuePair<string, object?>("cache_hit", cacheHit.ToString().ToLowerInvariant()));

        _logger.LogDebug("JWT validation recorded: CacheHit={CacheHit}, Duration={Duration}ms", cacheHit, validationTimeMs);
    }

    public void RecordJwksFetch(long fetchTimeMs, bool cacheHit, int retryCount)
    {
        _jwksFetchCounter.Add(1, 
            new KeyValuePair<string, object?>("cache_hit", cacheHit.ToString().ToLowerInvariant()),
            new KeyValuePair<string, object?>("retry_count", retryCount));
        
        _jwksFetchTime.Record(fetchTimeMs, 
            new KeyValuePair<string, object?>("cache_hit", cacheHit.ToString().ToLowerInvariant()),
            new KeyValuePair<string, object?>("retry_count", retryCount));

        _logger.LogDebug("JWKS fetch recorded: CacheHit={CacheHit}, RetryCount={RetryCount}, Duration={Duration}ms", 
            cacheHit, retryCount, fetchTimeMs);
    }

    public void RecordWalletOperation(string chain, string operation, bool success, long processingTimeMs)
    {
        _walletOperationCounter.Add(1,
            new KeyValuePair<string, object?>("chain", chain),
            new KeyValuePair<string, object?>("operation", operation),
            new KeyValuePair<string, object?>("success", success.ToString().ToLowerInvariant()));

        _walletOperationTime.Record(processingTimeMs,
            new KeyValuePair<string, object?>("chain", chain),
            new KeyValuePair<string, object?>("operation", operation),
            new KeyValuePair<string, object?>("success", success.ToString().ToLowerInvariant()));

        _logger.LogDebug("Wallet operation recorded: Chain={Chain}, Operation={Operation}, Success={Success}, Duration={Duration}ms",
            chain, operation, success, processingTimeMs);
    }

    public void RecordReplayAttempt(string jti)
    {
        _replayAttemptCounter.Add(1);
        _logger.LogWarning("JWT replay attempt recorded: JTI={JTI}", jti);
    }

    public void Dispose()
    {
        _meter?.Dispose();
    }
}