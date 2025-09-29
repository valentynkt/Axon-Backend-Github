using System.Diagnostics;
using Axon.Modules.Chat.Infrastructure.Persistence.DbContexts;
using BuildingBlocks.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NUnit.Framework;
using Shouldly;

namespace Axon.Modules.Chat.Infrastructure.Persistence.TestInfrastructure;

/// <summary>
/// Base class for Chat performance tests.
/// Provides timing assertions, query counting, and memory tracking capabilities.
/// </summary>
public abstract class ChatPerformanceTestBase : ChatPersistenceTestBase
{
    private readonly List<QueryExecutionInfo> _executedQueries = new();
    private readonly Stopwatch _operationTimer = new();
    private long _initialMemory;

    /// <summary>
    /// Performance thresholds that can be customized per test class.
    /// </summary>
    protected virtual TimeSpan SingleQueryThreshold => TimeSpan.FromMilliseconds(100);
    protected virtual TimeSpan BulkOperationThreshold => TimeSpan.FromSeconds(1);
    protected virtual int MaxAcceptableQueries => 10;
    protected virtual long MaxMemoryIncreaseKb => 10240; // 10MB

    [SetUp]
    public async Task PerformanceSetUp()
    {
        await Task.CompletedTask;

        // Capture initial memory
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        _initialMemory = GC.GetTotalMemory(false);

        // Hook into EF query logging
        EnableQueryLogging();
    }

    [TearDown]
    public async Task PerformanceTearDown()
    {
        await Task.CompletedTask;

        // Report performance metrics
        if (_executedQueries.Any())
        {
            TestContext.Out.WriteLine($"Executed {_executedQueries.Count} queries:");
            foreach (var query in _executedQueries)
            {
                TestContext.Out.WriteLine($"  - {query.Duration.TotalMilliseconds:F2}ms: {query.QueryType}");
            }
        }

        _executedQueries.Clear();
    }

    #region Performance Assertions

    /// <summary>
    /// Asserts that an operation completes within the specified time.
    /// </summary>
    protected async Task AssertOperationTime(Func<Task> operation, TimeSpan maxDuration, string operationName)
    {
        _operationTimer.Restart();
        await operation();
        _operationTimer.Stop();

        var elapsed = _operationTimer.Elapsed;
        TestContext.Out.WriteLine($"{operationName} took {elapsed.TotalMilliseconds:F2}ms");

        elapsed.ShouldBeLessThanOrEqualTo(maxDuration,
            $"{operationName} took {elapsed.TotalMilliseconds:F2}ms, exceeding threshold of {maxDuration.TotalMilliseconds:F2}ms");
    }

    /// <summary>
    /// Asserts that an operation executes no more than the specified number of queries.
    /// </summary>
    protected async Task AssertQueryCount(Func<Task> operation, int maxQueries, string operationName)
    {
        _executedQueries.Clear();

        await operation();

        var queryCount = _executedQueries.Count;
        TestContext.Out.WriteLine($"{operationName} executed {queryCount} queries");

        queryCount.ShouldBeLessThanOrEqualTo(maxQueries,
            $"{operationName} executed {queryCount} queries, exceeding threshold of {maxQueries}");
    }

    /// <summary>
    /// Detects N+1 query problems by analyzing query patterns.
    /// </summary>
    protected async Task AssertNoNPlusOneQueries(Func<Task> operation, string operationName)
    {
        _executedQueries.Clear();

        await operation();

        // Group queries by pattern (simplified - looks for similar query starts)
        var queryGroups = _executedQueries
            .GroupBy(q => GetQueryPattern(q.QueryText))
            .Where(g => g.Count() > 2) // More than 2 similar queries might indicate N+1
            .ToList();

        if (queryGroups.Any())
        {
            var suspiciousGroup = queryGroups.First();
            TestContext.Out.WriteLine($"Potential N+1 detected in {operationName}:");
            TestContext.Out.WriteLine($"  Pattern: {suspiciousGroup.Key}");
            TestContext.Out.WriteLine($"  Count: {suspiciousGroup.Count()}");

            suspiciousGroup.Count().ShouldBeLessThanOrEqualTo(2,
                $"Potential N+1 query problem detected in {operationName}. " +
                $"Found {suspiciousGroup.Count()} similar queries with pattern: {suspiciousGroup.Key}");
        }
    }

    /// <summary>
    /// Asserts memory usage stays within acceptable bounds.
    /// </summary>
    protected void AssertMemoryUsage(string operationName)
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        var currentMemory = GC.GetTotalMemory(false);
        var memoryIncreaseBytes = currentMemory - _initialMemory;
        var memoryIncreaseKb = memoryIncreaseBytes / 1024;

        TestContext.Out.WriteLine($"{operationName} memory increase: {memoryIncreaseKb:N0} KB");

        memoryIncreaseKb.ShouldBeLessThanOrEqualTo(MaxMemoryIncreaseKb,
            $"{operationName} increased memory by {memoryIncreaseKb:N0} KB, exceeding threshold of {MaxMemoryIncreaseKb:N0} KB");
    }

    #endregion

    #region Query Tracking

    private void EnableQueryLogging()
    {
        // Configure logging to capture EF queries
        var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.AddProvider(new QueryLoggerProvider(_executedQueries));
            builder.SetMinimumLevel(LogLevel.Information);
        });

        DbContext.ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.TrackAll;
    }

    private static string GetQueryPattern(string query)
    {
        // Simplified pattern extraction - gets first 50 chars or until WHERE
        var whereIndex = query.IndexOf("WHERE", StringComparison.OrdinalIgnoreCase);
        if (whereIndex > 0 && whereIndex < 50)
            return query.Substring(0, whereIndex).Trim();

        return query.Length > 50 ? query.Substring(0, 50) : query;
    }

    #endregion

    #region Helper Classes

    protected class QueryExecutionInfo
    {
        public required string QueryText { get; init; }
        public required string QueryType { get; init; }
        public required TimeSpan Duration { get; init; }
        public DateTime ExecutedAt { get; init; } = DateTime.UtcNow;
    }

    private class QueryLoggerProvider : ILoggerProvider
    {
        private readonly List<QueryExecutionInfo> _queries;

        public QueryLoggerProvider(List<QueryExecutionInfo> queries)
        {
            _queries = queries;
        }

        public ILogger CreateLogger(string categoryName)
        {
            if (categoryName.Contains("Microsoft.EntityFrameworkCore.Database.Command"))
            {
                return new QueryLogger(_queries);
            }
            return new NullLogger();
        }

        public void Dispose() { }
    }

    private class QueryLogger : ILogger
    {
        private readonly List<QueryExecutionInfo> _queries;
        private readonly Stopwatch _timer = new();

        public QueryLogger(List<QueryExecutionInfo> queries)
        {
            _queries = queries;
        }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull
        {
            _timer.Restart();
            return null;
        }

        public bool IsEnabled(LogLevel logLevel) => logLevel == LogLevel.Information;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state,
            Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (eventId.Name == "CommandExecuted")
            {
                var message = formatter(state, exception);
                if (message.Contains("SELECT") || message.Contains("INSERT") ||
                    message.Contains("UPDATE") || message.Contains("DELETE"))
                {
                    _queries.Add(new QueryExecutionInfo
                    {
                        QueryText = message,
                        QueryType = GetQueryType(message),
                        Duration = _timer.Elapsed
                    });
                }
            }
        }

        private static string GetQueryType(string query)
        {
            if (query.Contains("SELECT")) return "SELECT";
            if (query.Contains("INSERT")) return "INSERT";
            if (query.Contains("UPDATE")) return "UPDATE";
            if (query.Contains("DELETE")) return "DELETE";
            return "OTHER";
        }
    }

    private class NullLogger : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => false;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state,
            Exception? exception, Func<TState, Exception?, string> formatter) { }
    }

    #endregion
}