using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Caching.Memory;
using BuildingBlocks.Core.Diagnostics.Performance;

namespace BuildingBlocks.Core.Diagnostics.Performance.Extensions;

/// <summary>
/// Service collection extensions for registering high-performance error system components.
/// Provides fluent configuration API with sensible defaults and production-optimized settings.
/// </summary>
public static class ErrorPerformanceExtensions
{
    /// <summary>
    /// Add complete error performance system with all components
    /// </summary>
    public static IServiceCollection AddErrorPerformanceSystem(
        this IServiceCollection services,
        Action<ErrorPerformanceOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        
        var options = new ErrorPerformanceOptions();
        configure?.Invoke(options);
        
        // Register configuration options
        services.Configure<ErrorCacheOptions>(opt => ConfigureFromPerformanceOptions(opt, options));
        services.Configure<ErrorMetadataPoolOptions>(opt => ConfigureFromPerformanceOptions(opt, options));
        services.Configure<ErrorMetricsOptions>(opt => ConfigureFromPerformanceOptions(opt, options));
        services.Configure<OptimizedErrorFactoryOptions>(opt => ConfigureFromPerformanceOptions(opt, options));
        services.Configure<EfficientErrorAggregatorOptions>(opt => ConfigureFromPerformanceOptions(opt, options));
        
        // Register core components in dependency order
        services.AddErrorCache(options.ErrorCacheOptions);
        services.AddErrorMetadataPool(options.MetadataPoolOptions);
        services.AddErrorMetrics(options.MetricsOptions);
        services.AddOptimizedErrorFactory(options.ErrorFactoryOptions);
        services.AddEfficientErrorAggregator(options.AggregatorOptions);
        
        // Register composite services and helpers
        services.AddScoped<IErrorPerformanceService, ErrorPerformanceService>();
        services.AddHostedService<ErrorPerformanceBackgroundService>();
        
        return services;
    }
    
    /// <summary>
    /// Add error caching with string interning
    /// </summary>
    public static IServiceCollection AddErrorCache(
        this IServiceCollection services,
        Action<ErrorCacheOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        
        if (configure != null)
        {
            services.Configure<ErrorCacheOptions>(configure);
        }
        
        // Ensure IMemoryCache is available
        services.AddMemoryCache();
        
        services.TryAddSingleton<ErrorCache>();
        
        return services;
    }
    
    /// <summary>
    /// Add error metadata object pooling
    /// </summary>
    public static IServiceCollection AddErrorMetadataPool(
        this IServiceCollection services,
        Action<ErrorMetadataPoolOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        
        if (configure != null)
        {
            services.Configure<ErrorMetadataPoolOptions>(configure);
        }
        
        services.TryAddSingleton<ErrorMetadataPool>();
        
        return services;
    }
    
    /// <summary>
    /// Add error metrics collection
    /// </summary>
    public static IServiceCollection AddErrorMetrics(
        this IServiceCollection services,
        Action<ErrorMetricsOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        
        if (configure != null)
        {
            services.Configure<ErrorMetricsOptions>(configure);
        }
        
        services.TryAddSingleton<ErrorMetrics>();
        
        return services;
    }
    
    /// <summary>
    /// Add optimized error factory
    /// </summary>
    public static IServiceCollection AddOptimizedErrorFactory(
        this IServiceCollection services,
        Action<OptimizedErrorFactoryOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        
        if (configure != null)
        {
            services.Configure<OptimizedErrorFactoryOptions>(configure);
        }
        
        services.TryAddScoped<OptimizedErrorFactory>();
        
        return services;
    }
    
    /// <summary>
    /// Add efficient error aggregator
    /// </summary>
    public static IServiceCollection AddEfficientErrorAggregator(
        this IServiceCollection services,
        Action<EfficientErrorAggregatorOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        
        if (configure != null)
        {
            services.Configure<EfficientErrorAggregatorOptions>(configure);
        }
        
        services.TryAddSingleton<EfficientErrorAggregator>();
        
        return services;
    }
    
    /// <summary>
    /// Configure error performance system for development environment
    /// </summary>
    public static IServiceCollection AddErrorPerformanceForDevelopment(
        this IServiceCollection services)
    {
        return services.AddErrorPerformanceSystem(options =>
        {
            // Development-optimized settings
            options.ErrorCacheOptions.MaxCachedErrors = 100;
            options.ErrorCacheOptions.SlidingExpirationMinutes = 10;
            options.ErrorCacheOptions.CleanupIntervalMinutes = 2;
            
            options.MetadataPoolOptions.MaxPoolSize = 50;
            options.MetadataPoolOptions.MaintenanceIntervalMinutes = 2;
            
            options.MetricsOptions.AggregationIntervalSeconds = 10;
            options.MetricsOptions.EnablePeriodicLogging = true;
            
            options.ErrorFactoryOptions.MaxCacheSize = 100;
            options.ErrorFactoryOptions.EnableBuilderCaching = true;
            
            options.AggregatorOptions.AggregationIntervalMs = 500;
            options.AggregatorOptions.CleanupIntervalMinutes = 2;
            options.AggregatorOptions.MaxRecentErrors = 200;
            options.AggregatorOptions.EnableAutoGrouping = true;
        });
    }
    
    /// <summary>
    /// Configure error performance system for production environment
    /// </summary>
    public static IServiceCollection AddErrorPerformanceForProduction(
        this IServiceCollection services)
    {
        return services.AddErrorPerformanceSystem(options =>
        {
            // Production-optimized settings
            options.ErrorCacheOptions.MaxCachedErrors = 5000;
            options.ErrorCacheOptions.SlidingExpirationMinutes = 60;
            options.ErrorCacheOptions.AbsoluteExpirationMinutes = 120;
            options.ErrorCacheOptions.CleanupIntervalMinutes = 10;
            options.ErrorCacheOptions.MemoryThresholdBytes = 100 * 1024 * 1024; // 100MB
            
            options.MetadataPoolOptions.MaxPoolSize = 500;
            options.MetadataPoolOptions.MaintenanceIntervalMinutes = 5;
            options.MetadataPoolOptions.MemoryThresholdBytes = 50 * 1024 * 1024; // 50MB
            
            options.MetricsOptions.AggregationIntervalSeconds = 30;
            options.MetricsOptions.EnablePeriodicLogging = false; // Disable in production
            options.MetricsOptions.MaxTimeSeriesEntries = 50000;
            
            options.ErrorFactoryOptions.MaxCacheSize = 2000;
            options.ErrorFactoryOptions.MaxInternedStrings = 5000;
            options.ErrorFactoryOptions.EnableServiceMetadata = true;
            
            options.AggregatorOptions.AggregationIntervalMs = 2000;
            options.AggregatorOptions.CleanupIntervalMinutes = 10;
            options.AggregatorOptions.MaxRecentErrors = 5000;
            options.AggregatorOptions.MaxPendingErrors = 20000;
            options.AggregatorOptions.AggregateRetentionMinutes = 120;
            options.AggregatorOptions.EnableAutoGrouping = true;
        });
    }
    
    /// <summary>
    /// Configure error performance system with high-throughput settings
    /// </summary>
    public static IServiceCollection AddErrorPerformanceForHighThroughput(
        this IServiceCollection services)
    {
        return services.AddErrorPerformanceSystem(options =>
        {
            // High-throughput optimized settings
            options.ErrorCacheOptions.MaxCachedErrors = 10000;
            options.ErrorCacheOptions.SlidingExpirationMinutes = 30;
            options.ErrorCacheOptions.CleanupIntervalMinutes = 5;
            options.ErrorCacheOptions.MemoryThresholdBytes = 200 * 1024 * 1024; // 200MB
            
            options.MetadataPoolOptions.MaxPoolSize = 1000;
            options.MetadataPoolOptions.InitialDictionaryCapacity = 16;
            options.MetadataPoolOptions.MaxDictionaryCapacity = 64;
            options.MetadataPoolOptions.MaintenanceIntervalMinutes = 2;
            
            options.MetricsOptions.AggregationIntervalSeconds = 15;
            options.MetricsOptions.MaxTimeSeriesEntries = 100000;
            options.MetricsOptions.EnablePatternTracking = true;
            
            options.ErrorFactoryOptions.MaxCacheSize = 5000;
            options.ErrorFactoryOptions.MaxInternedStrings = 10000;
            options.ErrorFactoryOptions.MaxInternLength = 500;
            
            options.AggregatorOptions.AggregationIntervalMs = 500;
            options.AggregatorOptions.MaxBatchSize = 500;
            options.AggregatorOptions.MaxPendingErrors = 50000;
            options.AggregatorOptions.MaxRecentErrors = 10000;
        });
    }
    
    #region Private Helper Methods
    
    private static void ConfigureFromPerformanceOptions(ErrorCacheOptions target, ErrorPerformanceOptions source)
    {
        if (source.ErrorCacheOptions == null) return;
        
        target.MaxCachedErrors = source.ErrorCacheOptions.MaxCachedErrors;
        target.MaxCachedMessageLength = source.ErrorCacheOptions.MaxCachedMessageLength;
        target.SlidingExpirationMinutes = source.ErrorCacheOptions.SlidingExpirationMinutes;
        target.AbsoluteExpirationMinutes = source.ErrorCacheOptions.AbsoluteExpirationMinutes;
        target.CleanupIntervalMinutes = source.ErrorCacheOptions.CleanupIntervalMinutes;
        target.MemoryThresholdBytes = source.ErrorCacheOptions.MemoryThresholdBytes;
        target.CompactionPercentage = source.ErrorCacheOptions.CompactionPercentage;
    }
    
    private static void ConfigureFromPerformanceOptions(ErrorMetadataPoolOptions target, ErrorPerformanceOptions source)
    {
        if (source.MetadataPoolOptions == null) return;
        
        target.MaxPoolSize = source.MetadataPoolOptions.MaxPoolSize;
        target.InitialDictionaryCapacity = source.MetadataPoolOptions.InitialDictionaryCapacity;
        target.MaxDictionaryCapacity = source.MetadataPoolOptions.MaxDictionaryCapacity;
        target.InitialListCapacity = source.MetadataPoolOptions.InitialListCapacity;
        target.MaxListCapacity = source.MetadataPoolOptions.MaxListCapacity;
        target.MaxCommonPatterns = source.MetadataPoolOptions.MaxCommonPatterns;
        target.MaintenanceIntervalMinutes = source.MetadataPoolOptions.MaintenanceIntervalMinutes;
        target.MemoryThresholdBytes = source.MetadataPoolOptions.MemoryThresholdBytes;
    }
    
    private static void ConfigureFromPerformanceOptions(ErrorMetricsOptions target, ErrorPerformanceOptions source)
    {
        if (source.MetricsOptions == null) return;
        
        target.AggregationIntervalSeconds = source.MetricsOptions.AggregationIntervalSeconds;
        target.EnablePatternTracking = source.MetricsOptions.EnablePatternTracking;
        target.EnableTimeSeriesTracking = source.MetricsOptions.EnableTimeSeriesTracking;
        target.MaxTimeSeriesEntries = source.MetricsOptions.MaxTimeSeriesEntries;
        target.EnablePeriodicLogging = source.MetricsOptions.EnablePeriodicLogging;
    }
    
    private static void ConfigureFromPerformanceOptions(OptimizedErrorFactoryOptions target, ErrorPerformanceOptions source)
    {
        if (source.ErrorFactoryOptions == null) return;
        
        target.MaxCacheSize = source.ErrorFactoryOptions.MaxCacheSize;
        target.MaxInternedStrings = source.ErrorFactoryOptions.MaxInternedStrings;
        target.MaxInternLength = source.ErrorFactoryOptions.MaxInternLength;
        target.EnableServiceMetadata = source.ErrorFactoryOptions.EnableServiceMetadata;
        target.EnableBuilderCaching = source.ErrorFactoryOptions.EnableBuilderCaching;
    }
    
    private static void ConfigureFromPerformanceOptions(EfficientErrorAggregatorOptions target, ErrorPerformanceOptions source)
    {
        if (source.AggregatorOptions == null) return;
        
        target.AggregationIntervalMs = source.AggregatorOptions.AggregationIntervalMs;
        target.CleanupIntervalMinutes = source.AggregatorOptions.CleanupIntervalMinutes;
        target.MaxErrorSamples = source.AggregatorOptions.MaxErrorSamples;
        target.MaxRecentErrors = source.AggregatorOptions.MaxRecentErrors;
        target.MaxPendingErrors = source.AggregatorOptions.MaxPendingErrors;
        target.MaxBatchSize = source.AggregatorOptions.MaxBatchSize;
        target.MaxGroupSize = source.AggregatorOptions.MaxGroupSize;
        target.AggregateRetentionMinutes = source.AggregatorOptions.AggregateRetentionMinutes;
        target.GroupRetentionMinutes = source.AggregatorOptions.GroupRetentionMinutes;
        target.MinCorrelationStrength = source.AggregatorOptions.MinCorrelationStrength;
        target.EnableAutoGrouping = source.AggregatorOptions.EnableAutoGrouping;
    }
    
    #endregion
}

/// <summary>
/// Composite configuration options for the entire error performance system
/// </summary>
public sealed class ErrorPerformanceOptions
{
    /// <summary>
    /// Error cache configuration options
    /// </summary>
    public ErrorCacheOptions ErrorCacheOptions { get; set; } = new();
    
    /// <summary>
    /// Metadata pool configuration options
    /// </summary>
    public ErrorMetadataPoolOptions MetadataPoolOptions { get; set; } = new();
    
    /// <summary>
    /// Metrics configuration options
    /// </summary>
    public ErrorMetricsOptions MetricsOptions { get; set; } = new();
    
    /// <summary>
    /// Error factory configuration options
    /// </summary>
    public OptimizedErrorFactoryOptions ErrorFactoryOptions { get; set; } = new();
    
    /// <summary>
    /// Aggregator configuration options
    /// </summary>
    public EfficientErrorAggregatorOptions AggregatorOptions { get; set; } = new();
    
    /// <summary>
    /// Enable comprehensive monitoring and diagnostics (default: true)
    /// </summary>
    public bool EnableDiagnostics { get; set; } = true;
    
    /// <summary>
    /// Enable background optimization services (default: true)
    /// </summary>
    public bool EnableBackgroundServices { get; set; } = true;
}

/// <summary>
/// Service interface for unified error performance operations
/// </summary>
public interface IErrorPerformanceService
{
    /// <summary>
    /// Get comprehensive system statistics
    /// </summary>
    Task<ErrorPerformanceStatistics> GetStatisticsAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Optimize system performance (cleanup, compaction, etc.)
    /// </summary>
    Task OptimizeAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Reset all performance counters and caches
    /// </summary>
    Task ResetAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Pre-warm caches with common patterns
    /// </summary>
    Task PreWarmAsync(IEnumerable<string> commonPatterns, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Export performance data for analysis
    /// </summary>
    Task<string> ExportDataAsync(ExportFormat format, CancellationToken cancellationToken = default);
}

/// <summary>
/// Implementation of unified error performance service
/// </summary>
internal sealed class ErrorPerformanceService : IErrorPerformanceService
{
    private readonly ErrorCache _errorCache;
    private readonly ErrorMetadataPool _metadataPool;
    private readonly ErrorMetrics _errorMetrics;
    private readonly OptimizedErrorFactory _errorFactory;
    private readonly EfficientErrorAggregator _errorAggregator;
    private readonly ILogger<ErrorPerformanceService> _logger;
    
    public ErrorPerformanceService(
        ErrorCache errorCache,
        ErrorMetadataPool metadataPool,
        ErrorMetrics errorMetrics,
        OptimizedErrorFactory errorFactory,
        EfficientErrorAggregator errorAggregator,
        ILogger<ErrorPerformanceService> logger)
    {
        _errorCache = errorCache;
        _metadataPool = metadataPool;
        _errorMetrics = errorMetrics;
        _errorFactory = errorFactory;
        _errorAggregator = errorAggregator;
        _logger = logger;
    }
    
    public async Task<ErrorPerformanceStatistics> GetStatisticsAsync(CancellationToken cancellationToken = default)
    {
        return await Task.Run(() =>
        {
            var cacheStats = _errorCache.GetStatistics();
            var poolStats = _metadataPool.GetStatistics();
            var metricsStats = _errorMetrics.GetStatistics();
            var factoryStats = _errorFactory.GetStatistics();
            var aggregatorSummary = _errorAggregator.GetSummary();
            
            return new ErrorPerformanceStatistics
            {
                CacheStatistics = cacheStats,
                PoolStatistics = poolStats,
                MetricsStatistics = metricsStats,
                FactoryStatistics = factoryStats,
                AggregatorSummary = aggregatorSummary,
                SystemMemoryPressure = GC.GetTotalMemory(false),
                Timestamp = DateTimeOffset.UtcNow
            };
        }, cancellationToken);
    }
    
    public async Task OptimizeAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Starting error performance system optimization");
            
            // Run optimizations in parallel where safe
            var tasks = new[]
            {
                Task.Run(() => _errorCache.Compact(), cancellationToken),
                Task.Run(() => _errorFactory.CompactCaches(), cancellationToken),
                Task.Run(() => _metadataPool.PerformMaintenance(), cancellationToken)
            };
            
            await Task.WhenAll(tasks);
            
            // Force garbage collection
            GC.Collect(2, GCCollectionMode.Optimized, blocking: false);
            
            _logger.LogInformation("Error performance system optimization completed");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during performance optimization");
            throw;
        }
    }
    
    public async Task ResetAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogWarning("Resetting error performance system - all caches and metrics will be cleared");
            
            await Task.Run(() =>
            {
                _errorCache.Clear();
                _errorMetrics.Reset();
                _errorFactory.CompactCaches();
            }, cancellationToken);
            
            _logger.LogInformation("Error performance system reset completed");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during performance reset");
            throw;
        }
    }
    
    public async Task PreWarmAsync(IEnumerable<string> commonPatterns, CancellationToken cancellationToken = default)
    {
        try
        {
            var patterns = commonPatterns.ToList();
            _logger.LogInformation("Pre-warming caches with {Count} patterns", patterns.Count);
            
            await Task.Run(() =>
            {
                _errorFactory.PreWarmCache(patterns);
                _errorCache.PreCacheCommonErrors();
                
                // Pre-warm with common string patterns
                _errorFactory.OptimizeStringAllocations(patterns, StringType.ErrorCode);
            }, cancellationToken);
            
            _logger.LogInformation("Cache pre-warming completed");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during cache pre-warming");
            throw;
        }
    }
    
    public async Task<string> ExportDataAsync(ExportFormat format, CancellationToken cancellationToken = default)
    {
        try
        {
            var statistics = await GetStatisticsAsync(cancellationToken);
            
            return format switch
            {
                ExportFormat.Json => System.Text.Json.JsonSerializer.Serialize(statistics, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }),
                ExportFormat.Csv => ConvertToCsv(statistics),
                ExportFormat.Text => ConvertToText(statistics),
                _ => throw new ArgumentException($"Unsupported export format: {format}")
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during data export");
            throw;
        }
    }
    
    private static string ConvertToCsv(ErrorPerformanceStatistics stats)
    {
        var csv = new StringBuilder();
        csv.AppendLine("Component,Metric,Value");
        csv.AppendLine($"Cache,HitCount,{stats.CacheStatistics.HitCount}");
        csv.AppendLine($"Cache,MissCount,{stats.CacheStatistics.MissCount}");
        csv.AppendLine($"Cache,HitRatio,{stats.CacheStatistics.HitRatio:F2}");
        csv.AppendLine($"Pool,DictionaryPoolHits,{stats.PoolStatistics.DictionaryPoolHits}");
        csv.AppendLine($"Pool,PatternPoolHits,{stats.PoolStatistics.PatternPoolHits}");
        csv.AppendLine($"Factory,ZeroAllocationHits,{stats.FactoryStatistics.ZeroAllocationHits}");
        csv.AppendLine($"Factory,CachedCreations,{stats.FactoryStatistics.CachedCreations}");
        csv.AppendLine($"Aggregator,TotalErrors,{stats.AggregatorSummary.TotalErrors}");
        csv.AppendLine($"Aggregator,TotalAggregates,{stats.AggregatorSummary.TotalAggregates}");
        csv.AppendLine($"System,MemoryPressure,{stats.SystemMemoryPressure}");
        
        return csv.ToString();
    }
    
    private static string ConvertToText(ErrorPerformanceStatistics stats)
    {
        var text = new StringBuilder();
        text.AppendLine("=== Error Performance System Statistics ===");
        text.AppendLine($"Generated: {stats.Timestamp}");
        text.AppendLine();
        
        text.AppendLine("Cache Statistics:");
        text.AppendLine($"  Hits: {stats.CacheStatistics.HitCount:N0}");
        text.AppendLine($"  Misses: {stats.CacheStatistics.MissCount:N0}");
        text.AppendLine($"  Hit Ratio: {stats.CacheStatistics.HitRatio:P2}");
        text.AppendLine();
        
        text.AppendLine("Pool Statistics:");
        text.AppendLine($"  Dictionary Pool Hits: {stats.PoolStatistics.DictionaryPoolHits:N0}");
        text.AppendLine($"  Pattern Pool Hits: {stats.PoolStatistics.PatternPoolHits:N0}");
        text.AppendLine($"  Total Allocations: {stats.PoolStatistics.TotalAllocations:N0}");
        text.AppendLine();
        
        text.AppendLine("Factory Statistics:");
        text.AppendLine($"  Zero-Allocation Hits: {stats.FactoryStatistics.ZeroAllocationHits:N0}");
        text.AppendLine($"  Cached Creations: {stats.FactoryStatistics.CachedCreations:N0}");
        text.AppendLine($"  Fresh Creations: {stats.FactoryStatistics.FreshCreations:N0}");
        text.AppendLine();
        
        text.AppendLine("Aggregator Summary:");
        text.AppendLine($"  Total Errors: {stats.AggregatorSummary.TotalErrors:N0}");
        text.AppendLine($"  Total Aggregates: {stats.AggregatorSummary.TotalAggregates:N0}");
        text.AppendLine($"  Total Groups: {stats.AggregatorSummary.TotalGroups:N0}");
        text.AppendLine();
        
        text.AppendLine($"System Memory Pressure: {stats.SystemMemoryPressure:N0} bytes");
        
        return text.ToString();
    }
}

/// <summary>
/// Background service for error performance maintenance
/// </summary>
internal sealed class ErrorPerformanceBackgroundService : BackgroundService
{
    private readonly IErrorPerformanceService _performanceService;
    private readonly ILogger<ErrorPerformanceBackgroundService> _logger;
    private readonly IHostApplicationLifetime _appLifetime;
    
    public ErrorPerformanceBackgroundService(
        IErrorPerformanceService performanceService,
        ILogger<ErrorPerformanceBackgroundService> logger,
        IHostApplicationLifetime appLifetime)
    {
        _performanceService = performanceService;
        _logger = logger;
        _appLifetime = appLifetime;
    }
    
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Error performance background service started");
        
        // Wait for application to fully start
        await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
        
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // Perform periodic optimization every 10 minutes
                await _performanceService.OptimizeAsync(stoppingToken);
                
                // Log statistics every hour
                var stats = await _performanceService.GetStatisticsAsync(stoppingToken);
                _logger.LogInformation("Performance stats - Cache hits: {CacheHits}, Factory zero-alloc: {ZeroAlloc}, Total errors: {TotalErrors}",
                    stats.CacheStatistics.HitCount, stats.FactoryStatistics.ZeroAllocationHits, stats.AggregatorSummary.TotalErrors);
                
                // Wait for next cycle
                await Task.Delay(TimeSpan.FromMinutes(10), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in performance background service");
                
                // Wait before retrying to avoid tight loops
                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
            }
        }
        
        _logger.LogInformation("Error performance background service stopped");
    }
}

/// <summary>
/// Comprehensive statistics for the entire error performance system
/// </summary>
public sealed record ErrorPerformanceStatistics
{
    public ErrorCacheStatistics CacheStatistics { get; init; } = new();
    public ErrorMetadataPoolStatistics PoolStatistics { get; init; } = new();
    public ErrorMetricsStatistics MetricsStatistics { get; init; } = new();
    public OptimizedErrorFactoryStatistics FactoryStatistics { get; init; } = new();
    public ErrorAggregationSummary AggregatorSummary { get; init; } = new();
    public long SystemMemoryPressure { get; init; }
    public DateTimeOffset Timestamp { get; init; }
}

/// <summary>
/// Export format options for performance data
/// </summary>
public enum ExportFormat
{
    Json,
    Csv,
    Text
}