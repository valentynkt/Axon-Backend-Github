using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace Axon.Modules.Chat.Infrastructure.Services;

/// <summary>
/// Provides correlation ID for request tracking and distributed tracing
/// Implements SPARC correlation tracking patterns
/// </summary>
public sealed class CorrelationIdProvider : ICorrelationIdProvider
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<CorrelationIdProvider> _logger;
    private static readonly AsyncLocal<string?> _correlationId = new();

    public const string CorrelationIdHeaderName = "X-Correlation-ID";
    public const string CorrelationIdContextKey = "CorrelationId";

    public CorrelationIdProvider(
        IHttpContextAccessor httpContextAccessor,
        ILogger<CorrelationIdProvider> logger)
    {
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
    }

    /// <summary>
    /// Gets the current correlation ID
    /// </summary>
    public string GetCorrelationId()
    {
        // Try to get from AsyncLocal first (for background operations)
        var correlationId = _correlationId.Value;
        if (!string.IsNullOrEmpty(correlationId))
        {
            return correlationId;
        }

        // Try to get from HTTP context
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext != null)
        {
            // Check if already stored in context items
            if (httpContext.Items.TryGetValue(CorrelationIdContextKey, out var contextValue) 
                && contextValue is string existingId)
            {
                return existingId;
            }

            // Try to get from request headers
            if (httpContext.Request.Headers.TryGetValue(CorrelationIdHeaderName, out var headerValue))
            {
                correlationId = headerValue.FirstOrDefault();
                if (!string.IsNullOrEmpty(correlationId))
                {
                    // Store in context for future use
                    httpContext.Items[CorrelationIdContextKey] = correlationId;
                    SetCorrelationId(correlationId);
                    return correlationId;
                }
            }
        }

        // Try to get from Activity (for distributed tracing)
        var activity = Activity.Current;
        if (activity != null)
        {
            correlationId = activity.Id ?? activity.TraceId.ToString();
            if (!string.IsNullOrEmpty(correlationId))
            {
                SetCorrelationId(correlationId);
                return correlationId;
            }
        }

        // Generate new correlation ID if none found
        correlationId = GenerateCorrelationId();
        SetCorrelationId(correlationId);

        _logger.LogDebug("Generated new correlation ID: {CorrelationId}", correlationId);
        return correlationId;
    }

    /// <summary>
    /// Sets the correlation ID for the current context
    /// </summary>
    public void SetCorrelationId(string correlationId)
    {
        if (string.IsNullOrWhiteSpace(correlationId))
        {
            throw new ArgumentException("Correlation ID cannot be null or empty", nameof(correlationId));
        }

        // Set in AsyncLocal for background operations
        _correlationId.Value = correlationId;

        // Set in HTTP context if available
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext != null)
        {
            httpContext.Items[CorrelationIdContextKey] = correlationId;
            
            // Add to response headers for client tracking
            if (!httpContext.Response.Headers.ContainsKey(CorrelationIdHeaderName))
            {
                httpContext.Response.Headers.Add(CorrelationIdHeaderName, correlationId);
            }
        }

        // Set in Activity for distributed tracing
        var activity = Activity.Current;
        if (activity != null)
        {
            activity.SetTag("correlation.id", correlationId);
        }

        _logger.LogDebug("Set correlation ID: {CorrelationId}", correlationId);
    }

    /// <summary>
    /// Clears the current correlation ID
    /// </summary>
    public void ClearCorrelationId()
    {
        _correlationId.Value = null;

        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext != null)
        {
            httpContext.Items.Remove(CorrelationIdContextKey);
        }

        _logger.LogDebug("Cleared correlation ID");
    }

    /// <summary>
    /// Executes an operation with a specific correlation ID
    /// </summary>
    public async Task<T> ExecuteWithCorrelationIdAsync<T>(
        string correlationId,
        Func<Task<T>> operation)
    {
        var previousCorrelationId = _correlationId.Value;
        
        try
        {
            SetCorrelationId(correlationId);
            return await operation();
        }
        finally
        {
            if (previousCorrelationId != null)
            {
                SetCorrelationId(previousCorrelationId);
            }
            else
            {
                ClearCorrelationId();
            }
        }
    }

    /// <summary>
    /// Executes an operation with a specific correlation ID (void return)
    /// </summary>
    public async Task ExecuteWithCorrelationIdAsync(
        string correlationId,
        Func<Task> operation)
    {
        await ExecuteWithCorrelationIdAsync(correlationId, async () =>
        {
            await operation();
            return true; // Dummy return value
        });
    }

    /// <summary>
    /// Generates a new correlation ID
    /// </summary>
    private static string GenerateCorrelationId()
    {
        // Use a combination of timestamp and random value for uniqueness
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var random = Random.Shared.Next(1000, 9999);
        return $"{timestamp:x}-{random:x}";
    }
}

/// <summary>
/// Interface for correlation ID management
/// </summary>
public interface ICorrelationIdProvider
{
    /// <summary>
    /// Gets the current correlation ID
    /// </summary>
    string GetCorrelationId();

    /// <summary>
    /// Sets the correlation ID for the current context
    /// </summary>
    void SetCorrelationId(string correlationId);

    /// <summary>
    /// Clears the current correlation ID
    /// </summary>
    void ClearCorrelationId();

    /// <summary>
    /// Executes an operation with a specific correlation ID
    /// </summary>
    Task<T> ExecuteWithCorrelationIdAsync<T>(string correlationId, Func<Task<T>> operation);

    /// <summary>
    /// Executes an operation with a specific correlation ID (void return)
    /// </summary>
    Task ExecuteWithCorrelationIdAsync(string correlationId, Func<Task> operation);
}