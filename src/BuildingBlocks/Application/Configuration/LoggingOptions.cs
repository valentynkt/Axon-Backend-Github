namespace BuildingBlocks.Application.Configuration;

/// <summary>
/// Configuration options for pipeline behavior logging.
/// Controls when and how requests are logged for performance monitoring.
/// </summary>
public sealed class LoggingOptions
{
    /// <summary>
    /// Threshold in milliseconds above which requests are considered slow and logged as warnings.
    /// </summary>
    public int SlowRequestThresholdMs { get; set; } = 1000;

    /// <summary>
    /// Whether to enable structured logging with request/response data.
    /// </summary>
    public bool EnableStructuredLogging { get; set; } = true;

    /// <summary>
    /// Whether to log request parameters (be careful with sensitive data).
    /// </summary>
    public bool LogRequestParameters { get; set; }

    /// <summary>
    /// Whether to log response data (can be verbose).
    /// </summary>
    public bool LogResponseData { get; set; }

    /// <summary>
    /// Maximum length of logged request/response data to prevent log spam.
    /// </summary>
    public int MaxLoggedDataLength { get; set; } = 1000;
}