using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using BuildingBlocks.Core.Diagnostics.Errors;

namespace BuildingBlocks.Core.Diagnostics.Performance;

/// <summary>
/// High-performance error factory with zero-allocation paths for common error patterns.
/// Utilizes caching, string interning, and pre-allocated instances to minimize
/// memory allocations in performance-critical error handling scenarios.
/// </summary>
public sealed class OptimizedErrorFactory : IDisposable
{
    private readonly ErrorCache _errorCache;
    private readonly ErrorMetadataPool _metadataPool;
    private readonly ErrorMetrics _errorMetrics;
    private readonly OptimizedErrorFactoryOptions _options;
    private readonly ILogger<OptimizedErrorFactory> _logger;
    
    // Pre-allocated common errors (zero-allocation access)
    private readonly Dictionary<string, Error> _preAllocatedErrors;
    
    // Fast lookup tables for common patterns
    private readonly ConcurrentDictionary<ErrorCreationKey, Error> _creationCache;
    
    // String interning pools for performance
    private readonly ConcurrentDictionary<string, string> _internedCodes;
    private readonly ConcurrentDictionary<string, string> _internedMessages;
    private readonly ConcurrentDictionary<string, string> _internedSources;
    
    // Performance counters
    private long _zeroAllocationHits;
    private long _cachedCreations;
    private long _freshCreations;
    private long _metadataPoolUsage;
    
    private readonly object _lockObject = new();
    private bool _disposed;
    
    public OptimizedErrorFactory(
        ErrorCache errorCache,
        ErrorMetadataPool metadataPool,
        ErrorMetrics errorMetrics,
        IOptions<OptimizedErrorFactoryOptions> options,
        ILogger<OptimizedErrorFactory> logger)
    {
        _errorCache = errorCache ?? throw new ArgumentNullException(nameof(errorCache));
        _metadataPool = metadataPool ?? throw new ArgumentNullException(nameof(metadataPool));
        _errorMetrics = errorMetrics ?? throw new ArgumentNullException(nameof(errorMetrics));
        _options = options?.Value ?? new OptimizedErrorFactoryOptions();
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        
        _preAllocatedErrors = new Dictionary<string, Error>();
        _creationCache = new ConcurrentDictionary<ErrorCreationKey, Error>();
        _internedCodes = new ConcurrentDictionary<string, string>();
        _internedMessages = new ConcurrentDictionary<string, string>();
        _internedSources = new ConcurrentDictionary<string, string>();
        
        InitializePreAllocatedErrors();
    }
    
    #region Zero-Allocation Error Creation
    
    /// <summary>
    /// Create validation error with zero allocations for common patterns
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Error CreateValidationError(string code, string? message = null)
    {
        // Try pre-allocated first (zero allocation)
        if (TryGetPreAllocatedError($"validation:{code}", out var preAllocated))
        {
            RecordZeroAllocationHit();
            return preAllocated;
        }
        
        // Use cache with interned strings
        var internedCode = InternCode(code);
        var internedMessage = message != null ? InternMessage(message) : $"Validation failed: {code}";
        
        return _errorCache.GetOrCreateError(
            internedCode,
            internedMessage,
            ErrorType.Validation,
            ErrorSeverity.Warning
        );
    }
    
    /// <summary>
    /// Create not found error with zero allocations for common patterns
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Error CreateNotFoundError(string entity, string? identifier = null)
    {
        var code = $"ENTITY_NOT_FOUND";
        var key = $"not_found:{entity}";
        
        // Try pre-allocated first
        if (TryGetPreAllocatedError(key, out var preAllocated))
        {
            RecordZeroAllocationHit();
            return preAllocated;
        }
        
        var message = identifier != null 
            ? $"{entity} with identifier '{identifier}' was not found"
            : $"{entity} was not found";
        
        return _errorCache.GetOrCreateError(
            InternCode(code),
            InternMessage(message),
            ErrorType.NotFound,
            ErrorSeverity.Info
        );
    }
    
    /// <summary>
    /// Create business rule error with optimized metadata handling
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Error CreateBusinessRuleError(string rule, string entity, object? context = null)
    {
        var code = $"BUSINESS_RULE_VIOLATED";
        var message = $"Business rule '{rule}' violated for {entity}";
        
        // Use cached creation for common patterns
        var cacheKey = new ErrorCreationKey(code, message, ErrorType.BusinessRule, ErrorSeverity.Warning, context != null);
        
        if (_creationCache.TryGetValue(cacheKey, out var cached) && context == null)
        {
            RecordCachedCreation();
            return cached;
        }
        
        IReadOnlyDictionary<string, object>? metadata = null;
        if (context != null)
        {
            metadata = _metadataPool.GetBusinessRuleMetadata(rule, entity);
            RecordMetadataPoolUsage();
        }
        
        var error = _errorCache.GetOrCreateError(
            InternCode(code),
            InternMessage(message),
            ErrorType.BusinessRule,
            ErrorSeverity.Warning,
            metadata: metadata
        );
        
        // Cache simple patterns without context
        if (context == null && _creationCache.Count < _options.MaxCacheSize)
        {
            _creationCache.TryAdd(cacheKey, error);
        }
        
        return error;
    }
    
    /// <summary>
    /// Create internal error with optimized exception handling
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Error CreateInternalError(string message, Exception? exception = null, string? source = null)
    {
        var code = "INTERNAL_SERVER_ERROR";
        
        // For exceptions, always create fresh (can't cache due to dynamic content)
        if (exception != null)
        {
            RecordFreshCreation();
            return _errorCache.GetOrCreateError(
                InternCode(code),
                InternMessage(message),
                ErrorType.Internal,
                ErrorSeverity.Critical,
                exception,
                source: source != null ? InternSource(source) : null
            );
        }
        
        // Try cached creation for simple internal errors
        var cacheKey = new ErrorCreationKey(code, message, ErrorType.Internal, ErrorSeverity.Critical, false);
        
        if (_creationCache.TryGetValue(cacheKey, out var cached))
        {
            RecordCachedCreation();
            return cached;
        }
        
        var error = _errorCache.GetOrCreateError(
            InternCode(code),
            InternMessage(message),
            ErrorType.Internal,
            ErrorSeverity.Critical,
            source: source != null ? InternSource(source) : null
        );
        
        if (_creationCache.Count < _options.MaxCacheSize)
        {
            _creationCache.TryAdd(cacheKey, error);
        }
        
        return error;
    }
    
    /// <summary>
    /// Create external service error with common service patterns
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Error CreateExternalServiceError(string service, string operation, int? statusCode = null, Exception? exception = null)
    {
        var code = "EXTERNAL_SERVICE_ERROR";
        var message = $"External service '{service}' operation '{operation}' failed";
        
        IReadOnlyDictionary<string, object>? metadata = null;
        if (_options.EnableServiceMetadata)
        {
            metadata = _metadataPool.GetExternalServiceMetadata(service, operation, statusCode);
            RecordMetadataPoolUsage();
        }
        
        return _errorCache.GetOrCreateError(
            InternCode(code),
            InternMessage(message),
            ErrorType.External,
            ErrorSeverity.Error,
            exception,
            metadata
        );
    }
    
    #endregion
    
    #region Batch Error Creation
    
    /// <summary>
    /// Create multiple errors efficiently with batch optimizations
    /// </summary>
    public IList<Error> CreateBatchErrors(IEnumerable<ErrorCreationRequest> requests)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(OptimizedErrorFactory));
        
        var requestList = requests.ToList();
        var results = new List<Error>(requestList.Count);
        
        // Group by pattern for batch processing
        var groupedRequests = requestList.GroupBy(r => new { r.Type, r.Severity, HasMetadata = r.MetadataBuilder != null });
        
        foreach (var group in groupedRequests)
        {
            foreach (var request in group)
            {
                var error = CreateErrorFromRequest(request);
                results.Add(error);
            }
        }
        
        return results;
    }
    
    /// <summary>
    /// Create validation errors for multiple fields efficiently
    /// </summary>
    public IList<Error> CreateValidationErrors(IEnumerable<(string Field, string Message)> validationFailures)
    {
        var failures = validationFailures.ToList();
        var results = new List<Error>(failures.Count);
        
        foreach (var (field, message) in failures)
        {
            var error = CreateValidationError($"VALIDATION_{field.ToUpperInvariant()}", message);
            results.Add(error);
        }
        
        return results;
    }
    
    #endregion
    
    #region Factory Pattern Builders
    
    /// <summary>
    /// Create error builder for complex error construction
    /// </summary>
    public OptimizedErrorBuilder CreateBuilder()
    {
        return new OptimizedErrorBuilder(this, _metadataPool, _options.EnableBuilderCaching);
    }
    
    /// <summary>
    /// Create error from builder pattern with optimizations
    /// </summary>
    internal Error CreateFromBuilder(
        string code,
        string message,
        ErrorType type,
        ErrorSeverity severity,
        Exception? exception,
        IReadOnlyDictionary<string, object>? metadata,
        string? correlationId,
        string? source)
    {
        return _errorCache.GetOrCreateError(
            InternCode(code),
            InternMessage(message),
            type,
            severity,
            exception,
            metadata,
            correlationId != null ? InternString(correlationId, _internedCodes) : null,
            source != null ? InternSource(source) : null
        );
    }
    
    #endregion
    
    #region Performance Optimizations
    
    /// <summary>
    /// Pre-warm cache with common error patterns
    /// </summary>
    public void PreWarmCache(IEnumerable<string> commonErrorCodes)
    {
        if (_disposed) return;
        
        foreach (var code in commonErrorCodes)
        {
            // Pre-create common patterns
            var validationError = CreateValidationError(code, $"Validation failed for {code}");
            var businessError = CreateBusinessRuleError(code, "entity");
            var notFoundError = CreateNotFoundError("Entity", code);
            
            _logger.LogDebug("Pre-warmed cache for error code: {Code}", code);
        }
        
        // Also warm the underlying cache
        _errorCache.PreCacheCommonErrors();
    }
    
    /// <summary>
    /// Optimize string allocations by pre-interning common strings
    /// </summary>
    public void OptimizeStringAllocations(IEnumerable<string> commonStrings, StringType stringType)
    {
        if (_disposed) return;
        
        var cache = stringType switch
        {
            StringType.ErrorCode => _internedCodes,
            StringType.ErrorMessage => _internedMessages,
            StringType.ErrorSource => _internedSources,
            _ => _internedCodes
        };
        
        foreach (var str in commonStrings)
        {
            if (!string.IsNullOrEmpty(str))
            {
                InternString(str, cache);
            }
        }
    }
    
    /// <summary>
    /// Compact internal caches to reduce memory usage
    /// </summary>
    public void CompactCaches()
    {
        if (_disposed) return;
        
        lock (_lockObject)
        {
            // Compact creation cache if it's too large
            if (_creationCache.Count > _options.MaxCacheSize * 1.5)
            {
                var itemsToRemove = _creationCache.Keys.Take((int)(_creationCache.Count * 0.3)).ToList();
                foreach (var key in itemsToRemove)
                {
                    _creationCache.TryRemove(key, out _);
                }
            }
            
            // Limit string interning caches
            CompactStringCache(_internedCodes, _options.MaxInternedStrings);
            CompactStringCache(_internedMessages, _options.MaxInternedStrings);
            CompactStringCache(_internedSources, _options.MaxInternedStrings);
            
            _logger.LogDebug("Compacted OptimizedErrorFactory caches");
        }
    }
    
    #endregion
    
    #region Statistics and Monitoring
    
    /// <summary>
    /// Get factory performance statistics
    /// </summary>
    public OptimizedErrorFactoryStatistics GetStatistics()
    {
        if (_disposed) return new OptimizedErrorFactoryStatistics();
        
        var totalOperations = _zeroAllocationHits + _cachedCreations + _freshCreations;
        var cacheHitRatio = totalOperations > 0 ? (double)(_zeroAllocationHits + _cachedCreations) / totalOperations : 0.0;
        
        return new OptimizedErrorFactoryStatistics
        {
            ZeroAllocationHits = _zeroAllocationHits,
            CachedCreations = _cachedCreations,
            FreshCreations = _freshCreations,
            MetadataPoolUsage = _metadataPoolUsage,
            CacheHitRatio = cacheHitRatio,
            PreAllocatedErrorsCount = _preAllocatedErrors.Count,
            CreationCacheSize = _creationCache.Count,
            InternedCodesCount = _internedCodes.Count,
            InternedMessagesCount = _internedMessages.Count,
            InternedSourcesCount = _internedSources.Count,
            TotalOperations = totalOperations
        };
    }
    
    #endregion
    
    #region Private Helper Methods
    
    private void InitializePreAllocatedErrors()
    {
        // Pre-allocate most common validation errors
        _preAllocatedErrors["validation:required"] = Error.Validation("Value is required", "VALIDATION_REQUIRED");
        _preAllocatedErrors["validation:invalid_format"] = Error.Validation("Invalid format", "VALIDATION_INVALID_FORMAT");
        _preAllocatedErrors["validation:out_of_range"] = Error.Validation("Value is out of range", "VALIDATION_OUT_OF_RANGE");
        
        // Pre-allocate common not found errors
        _preAllocatedErrors["not_found:entity"] = Error.NotFound("Entity not found", "ENTITY_NOT_FOUND");
        _preAllocatedErrors["not_found:resource"] = Error.NotFound("Resource not found", "RESOURCE_NOT_FOUND");
        _preAllocatedErrors["not_found:user"] = Error.NotFound("User not found", "USER_NOT_FOUND");
        
        // Pre-allocate common system errors
        _preAllocatedErrors["system:internal"] = Error.Internal("Internal server error", "INTERNAL_SERVER_ERROR");
        _preAllocatedErrors["system:unavailable"] = Error.External("Service unavailable", "SERVICE_UNAVAILABLE");
        
        _logger.LogInformation("Pre-allocated {Count} common error patterns", _preAllocatedErrors.Count);
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool TryGetPreAllocatedError(string key, out Error error)
    {
        return _preAllocatedErrors.TryGetValue(key, out error!);
    }
    
    private Error CreateErrorFromRequest(ErrorCreationRequest request)
    {
        IReadOnlyDictionary<string, object>? metadata = null;
        if (request.MetadataBuilder != null)
        {
            metadata = _metadataPool.CreateReadOnlyMetadata(request.MetadataBuilder);
            RecordMetadataPoolUsage();
        }
        
        return _errorCache.GetOrCreateError(
            InternCode(request.Code),
            InternMessage(request.Message),
            request.Type,
            request.Severity,
            request.Exception,
            metadata,
            request.CorrelationId,
            request.Source != null ? InternSource(request.Source) : null
        );
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private string InternCode(string code) => InternString(code, _internedCodes);
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private string InternMessage(string message) => InternString(message, _internedMessages);
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private string InternSource(string source) => InternString(source, _internedSources);
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private string InternString(string value, ConcurrentDictionary<string, string> cache)
    {
        if (string.IsNullOrEmpty(value) || value.Length > _options.MaxInternLength)
            return value;
        
        return cache.GetOrAdd(value, v => string.Intern(v));
    }
    
    private void CompactStringCache(ConcurrentDictionary<string, string> cache, int maxSize)
    {
        if (cache.Count <= maxSize) return;
        
        var itemsToRemove = cache.Keys.Take(cache.Count - maxSize).ToList();
        foreach (var key in itemsToRemove)
        {
            cache.TryRemove(key, out _);
        }
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void RecordZeroAllocationHit()
    {
        Interlocked.Increment(ref _zeroAllocationHits);
        _errorMetrics.TrackCacheHit("zero_allocation", "pre_allocated");
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void RecordCachedCreation()
    {
        Interlocked.Increment(ref _cachedCreations);
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void RecordFreshCreation()
    {
        Interlocked.Increment(ref _freshCreations);
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void RecordMetadataPoolUsage()
    {
        Interlocked.Increment(ref _metadataPoolUsage);
    }
    
    #endregion
    
    #region IDisposable
    
    public void Dispose()
    {
        if (_disposed) return;
        
        _disposed = true;
        
        try
        {
            var stats = GetStatistics();
            _logger.LogInformation("OptimizedErrorFactory disposed - Final stats: Zero-alloc hits: {ZeroAllocHits}, Cached: {Cached}, Fresh: {Fresh}", 
                stats.ZeroAllocationHits, stats.CachedCreations, stats.FreshCreations);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during OptimizedErrorFactory disposal");
        }
        
        GC.SuppressFinalize(this);
    }
    
    #endregion
}

/// <summary>
/// Builder pattern for optimized error creation with method chaining
/// </summary>
public sealed class OptimizedErrorBuilder
{
    private readonly OptimizedErrorFactory _factory;
    private readonly ErrorMetadataPool _metadataPool;
    private readonly bool _enableCaching;
    
    private string? _code;
    private string? _message;
    private ErrorType _type = ErrorType.Internal;
    private ErrorSeverity? _severity;
    private Exception? _exception;
    private Action<Dictionary<string, object>>? _metadataBuilder;
    private string? _correlationId;
    private string? _source;
    
    internal OptimizedErrorBuilder(OptimizedErrorFactory factory, ErrorMetadataPool metadataPool, bool enableCaching)
    {
        _factory = factory;
        _metadataPool = metadataPool;
        _enableCaching = enableCaching;
    }
    
    public OptimizedErrorBuilder WithCode(string code)
    {
        _code = code;
        return this;
    }
    
    public OptimizedErrorBuilder WithMessage(string message)
    {
        _message = message;
        return this;
    }
    
    public OptimizedErrorBuilder WithType(ErrorType type)
    {
        _type = type;
        return this;
    }
    
    public OptimizedErrorBuilder WithSeverity(ErrorSeverity severity)
    {
        _severity = severity;
        return this;
    }
    
    public OptimizedErrorBuilder WithException(Exception exception)
    {
        _exception = exception;
        return this;
    }
    
    public OptimizedErrorBuilder WithMetadata(Action<Dictionary<string, object>> builder)
    {
        _metadataBuilder = builder;
        return this;
    }
    
    public OptimizedErrorBuilder WithCorrelationId(string correlationId)
    {
        _correlationId = correlationId;
        return this;
    }
    
    public OptimizedErrorBuilder WithSource(string source)
    {
        _source = source;
        return this;
    }
    
    public Error Build()
    {
        if (string.IsNullOrEmpty(_code))
            throw new InvalidOperationException("Error code is required");
        
        if (string.IsNullOrEmpty(_message))
            throw new InvalidOperationException("Error message is required");
        
        var effectiveSeverity = _severity ?? GetDefaultSeverity(_type);
        
        IReadOnlyDictionary<string, object>? metadata = null;
        if (_metadataBuilder != null)
        {
            metadata = _metadataPool.CreateReadOnlyMetadata(_metadataBuilder);
        }
        
        return _factory.CreateFromBuilder(_code, _message, _type, effectiveSeverity, _exception, metadata, _correlationId, _source);
    }
    
    private static ErrorSeverity GetDefaultSeverity(ErrorType type) => type switch
    {
        ErrorType.Validation => ErrorSeverity.Warning,
        ErrorType.NotFound => ErrorSeverity.Info,
        ErrorType.Conflict => ErrorSeverity.Warning,
        ErrorType.BusinessRule => ErrorSeverity.Warning,
        ErrorType.Unauthorized => ErrorSeverity.Warning,
        ErrorType.Forbidden => ErrorSeverity.Warning,
        ErrorType.Internal => ErrorSeverity.Critical,
        ErrorType.External => ErrorSeverity.Error,
        ErrorType.Timeout => ErrorSeverity.Warning,
        ErrorType.Cancelled => ErrorSeverity.Info,
        ErrorType.Aggregate => ErrorSeverity.Error,
        _ => ErrorSeverity.Error
    };
}

/// <summary>
/// Configuration options for OptimizedErrorFactory
/// </summary>
public sealed class OptimizedErrorFactoryOptions
{
    /// <summary>
    /// Maximum size of creation cache (default: 500)
    /// </summary>
    public int MaxCacheSize { get; set; } = 500;
    
    /// <summary>
    /// Maximum number of interned strings per category (default: 1000)
    /// </summary>
    public int MaxInternedStrings { get; set; } = 1000;
    
    /// <summary>
    /// Maximum length of strings to intern (default: 200)
    /// </summary>
    public int MaxInternLength { get; set; } = 200;
    
    /// <summary>
    /// Enable service metadata for external errors (default: true)
    /// </summary>
    public bool EnableServiceMetadata { get; set; } = true;
    
    /// <summary>
    /// Enable caching in builder pattern (default: true)
    /// </summary>
    public bool EnableBuilderCaching { get; set; } = true;
}

/// <summary>
/// Error creation request for batch operations
/// </summary>
public sealed record ErrorCreationRequest
{
    public required string Code { get; init; }
    public required string Message { get; init; }
    public ErrorType Type { get; init; } = ErrorType.Internal;
    public ErrorSeverity Severity { get; init; } = ErrorSeverity.Error;
    public Exception? Exception { get; init; }
    public Action<Dictionary<string, object>>? MetadataBuilder { get; init; }
    public string? CorrelationId { get; init; }
    public string? Source { get; init; }
}

/// <summary>
/// String type enumeration for optimization
/// </summary>
public enum StringType
{
    ErrorCode,
    ErrorMessage,
    ErrorSource
}

/// <summary>
/// Cache key for error creation optimization
/// </summary>
public readonly record struct ErrorCreationKey(
    string Code,
    string Message,
    ErrorType Type,
    ErrorSeverity Severity,
    bool HasComplexContent);

/// <summary>
/// Statistics for OptimizedErrorFactory performance monitoring
/// </summary>
public sealed record OptimizedErrorFactoryStatistics
{
    public long ZeroAllocationHits { get; init; }
    public long CachedCreations { get; init; }
    public long FreshCreations { get; init; }
    public long MetadataPoolUsage { get; init; }
    public double CacheHitRatio { get; init; }
    public int PreAllocatedErrorsCount { get; init; }
    public int CreationCacheSize { get; init; }
    public int InternedCodesCount { get; init; }
    public int InternedMessagesCount { get; init; }
    public int InternedSourcesCount { get; init; }
    public long TotalOperations { get; init; }
}