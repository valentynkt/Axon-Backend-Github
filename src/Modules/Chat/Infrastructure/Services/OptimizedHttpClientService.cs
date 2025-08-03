using System.Net;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly;
using Polly.CircuitBreaker;
using Polly.Extensions.Http;

namespace Axon.Modules.Chat.Infrastructure.Services;

/// <summary>
/// Configuration options for optimized HTTP client
/// </summary>
public sealed class OptimizedHttpClientOptions
{
    public const string SectionName = "Chat:HttpClient";
    
    /// <summary>
    /// Connection timeout in seconds (default: 30)
    /// </summary>
    public int TimeoutSeconds { get; init; } = 30;
    
    /// <summary>
    /// Maximum number of connections per endpoint (default: 10)
    /// </summary>
    public int MaxConnectionsPerEndpoint { get; init; } = 10;
    
    /// <summary>
    /// Connection pool timeout in seconds (default: 60)
    /// </summary>
    public int PooledConnectionLifetimeSeconds { get; init; } = 60;
    
    /// <summary>
    /// Retry policy configuration
    /// </summary>
    public RetryPolicyOptions Retry { get; init; } = new();
    
    /// <summary>
    /// Circuit breaker configuration
    /// </summary>
    public CircuitBreakerOptions CircuitBreaker { get; init; } = new();
}

/// <summary>
/// Retry policy configuration
/// </summary>
public sealed class RetryPolicyOptions
{
    /// <summary>
    /// Number of retry attempts (default: 3)
    /// </summary>
    public int MaxRetries { get; init; } = 3;
    
    /// <summary>
    /// Base delay between retries in milliseconds (default: 1000)
    /// </summary>
    public int BaseDelayMs { get; init; } = 1000;
    
    /// <summary>
    /// Maximum delay between retries in milliseconds (default: 10000)
    /// </summary>
    public int MaxDelayMs { get; init; } = 10000;
}

/// <summary>
/// Circuit breaker configuration
/// </summary>
public sealed class CircuitBreakerOptions
{
    /// <summary>
    /// Number of failures before opening circuit (default: 5)
    /// </summary>
    public int FailureThreshold { get; init; } = 5;
    
    /// <summary>
    /// Time window for sampling failures in seconds (default: 30)
    /// </summary>
    public int SamplingDurationSeconds { get; init; } = 30;
    
    /// <summary>
    /// Minimum throughput before circuit breaker activates (default: 3)
    /// </summary>
    public int MinimumThroughput { get; init; } = 3;
    
    /// <summary>
    /// Duration to keep circuit open in seconds (default: 30)
    /// </summary>
    public int DurationOfBreakSeconds { get; init; } = 30;
}

/// <summary>
/// High-performance HTTP client service with connection pooling, retry policies, and circuit breaker
/// Provides optimal HTTP performance for MCP server communications
/// </summary>
public interface IOptimizedHttpClientService
{
    /// <summary>
    /// Send HTTP request with full optimization (connection pooling, retries, circuit breaker)
    /// </summary>
    Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Send GET request with optimizations
    /// </summary>
    Task<string> GetAsync(string requestUri, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Send POST request with optimizations
    /// </summary>
    Task<string> PostAsync(string requestUri, HttpContent content, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get HTTP client performance metrics
    /// </summary>
    (long TotalRequests, long SuccessfulRequests, long FailedRequests, double SuccessRate) GetMetrics();
}

/// <summary>
/// Implementation of optimized HTTP client service
/// </summary>
public sealed class OptimizedHttpClientService : IOptimizedHttpClientService, IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly Polly.Retry.AsyncRetryPolicy<HttpResponseMessage> _retryPolicy;
    private readonly Polly.CircuitBreaker.AsyncCircuitBreakerPolicy<HttpResponseMessage> _circuitBreakerPolicy;
    private readonly Polly.Wrap.AsyncPolicyWrap<HttpResponseMessage> _combinedPolicy;
    private readonly ILogger<OptimizedHttpClientService> _logger;
    
    // LoggerMessage delegates for CA1848 compliance
    private static readonly Action<ILogger, int, int, int, Exception?> LogServiceInitializedAction =
        LoggerMessage.Define<int, int, int>(
            LogLevel.Information,
            new EventId(7001, "LogServiceInitialized"),
            "OptimizedHttpClientService initialized with timeout: {Timeout}s, max connections: {MaxConnections}, pool lifetime: {PoolLifetime}s");
            
    private static readonly Action<ILogger, HttpStatusCode, HttpMethod, Uri?, Exception?> LogRequestFailedAction =
        LoggerMessage.Define<HttpStatusCode, HttpMethod, Uri?>(
            LogLevel.Warning,
            new EventId(7002, "LogRequestFailed"),
            "HTTP request failed with status {StatusCode}: {Method} {Uri}");
            
    private static readonly Action<ILogger, HttpMethod, Uri?, Exception?> LogRequestExceptionAction =
        LoggerMessage.Define<HttpMethod, Uri?>(
            LogLevel.Error,
            new EventId(7003, "LogRequestException"),
            "HTTP request failed with exception: {Method} {Uri}");
            
    private static readonly Action<ILogger, Exception?> LogCircuitBreakerResetAction =
        LoggerMessage.Define(
            LogLevel.Information,
            new EventId(7004, "LogCircuitBreakerReset"),
            "Circuit breaker reset - requests will be allowed again");
            
    private static readonly Action<ILogger, Exception?> LogCircuitBreakerHalfOpenAction =
        LoggerMessage.Define(
            LogLevel.Information,
            new EventId(7005, "LogCircuitBreakerHalfOpen"),
            "Circuit breaker half-open - testing if service has recovered");
            
    private static readonly Action<ILogger, long, long, long, double, Exception?> LogServiceDisposedAction =
        LoggerMessage.Define<long, long, long, double>(
            LogLevel.Information,
            new EventId(7006, "LogServiceDisposed"),
            "OptimizedHttpClientService disposed. Final metrics - Total: {Total}, Successful: {Successful}, Failed: {Failed}, Success Rate: {SuccessRate:P1}");
            
    private static readonly Action<ILogger, int, int, double, string?, Exception?> LogHttpRetryAction =
        LoggerMessage.Define<int, int, double, string?>(
            LogLevel.Warning,
            new EventId(7007, "LogHttpRetry"),
            "HTTP request retry {RetryCount}/{MaxRetries} after {Delay}ms delay. Reason: {Reason}");
            
    private static readonly Action<ILogger, double, int, string?, Exception?> LogCircuitBreakerOpenedAction =
        LoggerMessage.Define<double, int, string?>(
            LogLevel.Error,
            new EventId(7008, "LogCircuitBreakerOpened"),
            "Circuit breaker opened for {Duration}s due to {FailureThreshold} failures. Last exception: {Exception}");
    
    // Performance tracking
    private long _totalRequests;
    private long _successfulRequests;
    private long _failedRequests;
    private readonly object _metricsLock = new();

    public OptimizedHttpClientService(
        HttpClient httpClient,
        IOptions<OptimizedHttpClientOptions> options,
        ILogger<OptimizedHttpClientService> logger)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        
        var opts = options?.Value ?? new OptimizedHttpClientOptions();
        
        // Configure HTTP client for optimal performance
        ConfigureHttpClient(opts);
        
        // Build retry policy with exponential backoff
        _retryPolicy = BuildRetryPolicy(opts.Retry);
        
        // Build circuit breaker policy
        _circuitBreakerPolicy = BuildCircuitBreakerPolicy(opts.CircuitBreaker);
        
        // Combine policies: retry first, then circuit breaker
        _combinedPolicy = Policy.WrapAsync(_retryPolicy, _circuitBreakerPolicy);
        
        LogServiceInitializedAction(_logger, opts.TimeoutSeconds, opts.MaxConnectionsPerEndpoint, opts.PooledConnectionLifetimeSeconds, null);
    }

    /// <inheritdoc />
    public async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, 
        CancellationToken cancellationToken = default)
    {
        IncrementTotalRequests();
        
        try
        {
            var response = await _combinedPolicy.ExecuteAsync(async (ct) =>
            {
                // Clone request for retry attempts
                var clonedRequest = await CloneRequestAsync(request, ct);
                return await _httpClient.SendAsync(clonedRequest, ct);
            }, cancellationToken);
            
            if (response.IsSuccessStatusCode)
            {
                IncrementSuccessfulRequests();
            }
            else
            {
                IncrementFailedRequests();
                LogRequestFailedAction(_logger, response.StatusCode, request.Method, request.RequestUri, null);
            }
            
            return response;
        }
        catch (Exception ex)
        {
            IncrementFailedRequests();
            LogRequestExceptionAction(_logger, request.Method, request.RequestUri, ex);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<string> GetAsync(string requestUri, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, requestUri);
        using var response = await SendAsync(request, cancellationToken);
        
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<string> PostAsync(
        string requestUri, 
        HttpContent content, 
        CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, requestUri)
        {
            Content = content
        };
        
        using var response = await SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync(cancellationToken);
    }

    /// <inheritdoc />
    public (long TotalRequests, long SuccessfulRequests, long FailedRequests, double SuccessRate) GetMetrics()
    {
        lock (_metricsLock)
        {
            var successRate = _totalRequests > 0 ? (double)_successfulRequests / _totalRequests : 0.0;
            return (_totalRequests, _successfulRequests, _failedRequests, successRate);
        }
    }

    private void ConfigureHttpClient(OptimizedHttpClientOptions options)
    {
        // Set optimal timeout
        _httpClient.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
        
        // Configure default headers for performance
        _httpClient.DefaultRequestHeaders.Add("Connection", "keep-alive");
        _httpClient.DefaultRequestHeaders.Add("Keep-Alive", "timeout=60, max=100");
    }

    private Polly.Retry.AsyncRetryPolicy<HttpResponseMessage> BuildRetryPolicy(RetryPolicyOptions retryOptions)
    {
        return HttpPolicyExtensions
            .HandleTransientHttpError() // Handles HttpRequestException and 5XX, 408 status codes
            .Or<TaskCanceledException>() // Handle timeout
            .WaitAndRetryAsync(
                retryOptions.MaxRetries,
                retryAttempt => TimeSpan.FromMilliseconds(Math.Min(
                    retryOptions.BaseDelayMs * Math.Pow(2, retryAttempt - 1), // Exponential backoff
                    retryOptions.MaxDelayMs)),
                onRetry: (outcome, timespan, retryCount, context) =>
                {
                    LogHttpRetryAction(_logger, retryCount, retryOptions.MaxRetries, timespan.TotalMilliseconds,
                        outcome.Exception?.Message ?? outcome.Result?.StatusCode.ToString(), outcome.Exception);
                });
    }

    private Polly.CircuitBreaker.AsyncCircuitBreakerPolicy<HttpResponseMessage> BuildCircuitBreakerPolicy(CircuitBreakerOptions cbOptions)
    {
        return HttpPolicyExtensions
            .HandleTransientHttpError()
            .CircuitBreakerAsync(
                cbOptions.FailureThreshold,
                TimeSpan.FromSeconds(cbOptions.DurationOfBreakSeconds),
                onBreak: (exception, duration) =>
                {
                    LogCircuitBreakerOpenedAction(_logger, duration.TotalSeconds, cbOptions.FailureThreshold,
                        exception.Exception?.Message ?? exception.Result?.StatusCode.ToString(), exception.Exception);
                },
                onReset: () =>
                {
                    LogCircuitBreakerResetAction(_logger, null);
                },
                onHalfOpen: () =>
                {
                    LogCircuitBreakerHalfOpenAction(_logger, null);
                });
    }

    private static async Task<HttpRequestMessage> CloneRequestAsync(
        HttpRequestMessage originalRequest, 
        CancellationToken cancellationToken)
    {
        var clonedRequest = new HttpRequestMessage(originalRequest.Method, originalRequest.RequestUri)
        {
            Version = originalRequest.Version
        };

        // Copy headers
        foreach (var header in originalRequest.Headers)
        {
            clonedRequest.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        // Copy content if present
        if (originalRequest.Content != null)
        {
            var contentBytes = await originalRequest.Content.ReadAsByteArrayAsync(cancellationToken);
            clonedRequest.Content = new ByteArrayContent(contentBytes);
            
            // Copy content headers
            foreach (var header in originalRequest.Content.Headers)
            {
                clonedRequest.Content.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }
        }

        return clonedRequest;
    }

    private void IncrementTotalRequests()
    {
        lock (_metricsLock)
        {
            _totalRequests++;
        }
    }

    private void IncrementSuccessfulRequests()
    {
        lock (_metricsLock)
        {
            _successfulRequests++;
        }
    }

    private void IncrementFailedRequests()
    {
        lock (_metricsLock)
        {
            _failedRequests++;
        }
    }

    public void Dispose()
    {
        var (total, successful, failed, successRate) = GetMetrics();
        LogServiceDisposedAction(_logger, total, successful, failed, successRate, null);
        
        _httpClient?.Dispose();
    }
}