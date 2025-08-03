using Axon.Shared.Common;
// using Axon.Tests.Shared.Tests.Extensions; // Removed to fix circular dependency
using System.Collections.Concurrent;
using System.Diagnostics;

namespace Axon.Tests.Shared.Execution;

/// <summary>
/// Advanced parallel test execution framework optimized for shared utilities testing
/// Provides sophisticated concurrency control, load balancing, and performance monitoring
/// </summary>
public static class AdvancedParallelTestFramework
{
    /// <summary>
    /// Configuration for parallel test execution
    /// </summary>
    public class ParallelTestConfiguration
    {
        public int MaxDegreeOfParallelism { get; set; } = Environment.ProcessorCount;
        public TimeSpan DefaultTimeout { get; set; } = TimeSpan.FromMinutes(1);
        public bool EnableLoadBalancing { get; set; } = true;
        public bool EnablePerformanceMonitoring { get; set; } = true;
        public bool EnableResourceMonitoring { get; set; } = true;
        public int BatchSize { get; set; } = 100;
        public ThreadPriority TestThreadPriority { get; set; } = ThreadPriority.Normal;
    }

    /// <summary>
    /// Metrics collected during parallel test execution
    /// </summary>
    public class ExecutionMetrics
    {
        public TimeSpan TotalExecutionTime { get; set; }
        public int TotalTests { get; set; }
        public int SuccessfulTests { get; set; }
        public int FailedTests { get; set; }
        public int ThreadsUsed { get; set; }
        public double TestsPerSecond => TotalTests / TotalExecutionTime.TotalSeconds;
        public double SuccessRate => (double)SuccessfulTests / TotalTests * 100;
        public long MemoryUsedBytes { get; set; }
        public TimeSpan AverageTestDuration { get; set; }
        public TimeSpan MaxTestDuration { get; set; }
        public TimeSpan MinTestDuration { get; set; }
        public Dictionary<string, object> CustomMetrics { get; } = new();
    }

    /// <summary>
    /// Result of a parallel test execution
    /// </summary>
    public class ParallelTestResult<T>
    {
        public T Result { get; set; } = default!;
        public bool IsSuccess { get; set; }
        public Exception? Exception { get; set; }
        public TimeSpan Duration { get; set; }
        public int ThreadId { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public long MemoryBefore { get; set; }
        public long MemoryAfter { get; set; }
        public string TestName { get; set; } = string.Empty;
    }

    /// <summary>
    /// Executes Result operations in parallel with comprehensive monitoring
    /// </summary>
    public static async Task<(ExecutionMetrics Metrics, List<ParallelTestResult<Result<T>>> Results)> 
        ExecuteResultOperationsAsync<T>(
            IEnumerable<Func<Task<Result<T>>>> operations,
            ParallelTestConfiguration? config = null)
    {
        config ??= new ParallelTestConfiguration();
        var operationsList = operations.ToList();
        var results = new ConcurrentBag<ParallelTestResult<Result<T>>>();
        var metrics = new ExecutionMetrics { TotalTests = operationsList.Count };

        var stopwatch = Stopwatch.StartNew();
        var initialMemory = GC.GetTotalMemory(false);

        var parallelOptions = new ParallelOptions
        {
            MaxDegreeOfParallelism = config.MaxDegreeOfParallelism,
            CancellationToken = new CancellationTokenSource(config.DefaultTimeout).Token
        };

        // Execute operations in parallel with batching for better performance
        await Parallel.ForEachAsync(
            operationsList.Select((op, index) => new { Operation = op, Index = index }),
            parallelOptions,
            async (item, cancellationToken) =>
            {
                var testResult = new ParallelTestResult<Result<T>>
                {
                    ThreadId = Thread.CurrentThread.ManagedThreadId,
                    StartTime = DateTime.UtcNow,
                    TestName = $"Test_{item.Index}",
                    MemoryBefore = GC.GetTotalMemory(false)
                };

                var testStopwatch = Stopwatch.StartNew();

                try
                {
                    // Set thread priority if configured
                    if (config.TestThreadPriority != ThreadPriority.Normal)
                    {
                        Thread.CurrentThread.Priority = config.TestThreadPriority;
                    }

                    testResult.Result = await item.Operation();
                    testResult.IsSuccess = true;
                }
                catch (Exception ex)
                {
                    testResult.Exception = ex;
                    testResult.IsSuccess = false;
                    testResult.Result = Result<T>.Failure(Error.InternalError($"Test execution failed: {ex.Message}", "TEST_EXECUTION_ERROR", ex));
                }
                finally
                {
                    testStopwatch.Stop();
                    testResult.Duration = testStopwatch.Elapsed;
                    testResult.EndTime = DateTime.UtcNow;
                    testResult.MemoryAfter = GC.GetTotalMemory(false);
                    results.Add(testResult);
                }
            });

        stopwatch.Stop();

        var finalMemory = GC.GetTotalMemory(false);
        var resultsList = results.ToList();

        // Calculate comprehensive metrics
        metrics.TotalExecutionTime = stopwatch.Elapsed;
        metrics.SuccessfulTests = resultsList.Count(r => r.IsSuccess);
        metrics.FailedTests = resultsList.Count(r => !r.IsSuccess);
        metrics.ThreadsUsed = resultsList.Select(r => r.ThreadId).Distinct().Count();
        metrics.MemoryUsedBytes = finalMemory - initialMemory;

        if (resultsList.Any())
        {
            metrics.AverageTestDuration = TimeSpan.FromTicks((long)resultsList.Average(r => r.Duration.Ticks));
            metrics.MaxTestDuration = resultsList.Max(r => r.Duration);
            metrics.MinTestDuration = resultsList.Min(r => r.Duration);
        }

        // Add custom metrics
        metrics.CustomMetrics["ThreadDistribution"] = resultsList
            .GroupBy(r => r.ThreadId)
            .ToDictionary(g => $"Thread_{g.Key}", g => g.Count());

        metrics.CustomMetrics["MemoryPerTest"] = resultsList.Any() 
            ? resultsList.Average(r => r.MemoryAfter - r.MemoryBefore) 
            : 0;

        return (metrics, resultsList);
    }

    /// <summary>
    /// Executes Error creation operations in parallel with performance analysis
    /// </summary>
    public static async Task<(ExecutionMetrics Metrics, List<ParallelTestResult<Error>> Results)>
        ExecuteErrorOperationsAsync(
            IEnumerable<Func<Task<Error>>> operations,
            ParallelTestConfiguration? config = null)
    {
        config ??= new ParallelTestConfiguration();
        var operationsList = operations.ToList();
        var results = new ConcurrentBag<ParallelTestResult<Error>>();
        var metrics = new ExecutionMetrics { TotalTests = operationsList.Count };

        var stopwatch = Stopwatch.StartNew();
        var initialMemory = GC.GetTotalMemory(false);

        var parallelOptions = new ParallelOptions
        {
            MaxDegreeOfParallelism = config.MaxDegreeOfParallelism,
            CancellationToken = new CancellationTokenSource(config.DefaultTimeout).Token
        };

        await Parallel.ForEachAsync(
            operationsList.Select((op, index) => new { Operation = op, Index = index }),
            parallelOptions,
            async (item, cancellationToken) =>
            {
                var testResult = new ParallelTestResult<Error>
                {
                    ThreadId = Thread.CurrentThread.ManagedThreadId,
                    StartTime = DateTime.UtcNow,
                    TestName = $"ErrorTest_{item.Index}",
                    MemoryBefore = GC.GetTotalMemory(false)
                };

                var testStopwatch = Stopwatch.StartNew();

                try
                {
                    testResult.Result = await item.Operation();
                    testResult.IsSuccess = true;
                }
                catch (Exception ex)
                {
                    testResult.Exception = ex;
                    testResult.IsSuccess = false;
                    testResult.Result = Error.InternalError($"Error test failed: {ex.Message}", "ERROR_TEST_FAILED", ex);
                }
                finally
                {
                    testStopwatch.Stop();
                    testResult.Duration = testStopwatch.Elapsed;
                    testResult.EndTime = DateTime.UtcNow;
                    testResult.MemoryAfter = GC.GetTotalMemory(false);
                    results.Add(testResult);
                }
            });

        stopwatch.Stop();

        var finalMemory = GC.GetTotalMemory(false);
        var resultsList = results.ToList();

        // Calculate metrics
        metrics.TotalExecutionTime = stopwatch.Elapsed;
        metrics.SuccessfulTests = resultsList.Count(r => r.IsSuccess);
        metrics.FailedTests = resultsList.Count(r => !r.IsSuccess);
        metrics.ThreadsUsed = resultsList.Select(r => r.ThreadId).Distinct().Count();
        metrics.MemoryUsedBytes = finalMemory - initialMemory;

        if (resultsList.Any())
        {
            metrics.AverageTestDuration = TimeSpan.FromTicks((long)resultsList.Average(r => r.Duration.Ticks));
            metrics.MaxTestDuration = resultsList.Max(r => r.Duration);
            metrics.MinTestDuration = resultsList.Min(r => r.Duration);
        }

        return (metrics, resultsList);
    }

    /// <summary>
    /// Executes stress test scenarios with load balancing and resource monitoring
    /// </summary>
    public static async Task<ExecutionMetrics> ExecuteStressTestAsync<T>(
        Func<Task<T>> operation,
        int iterations,
        ParallelTestConfiguration? config = null)
    {
        config ??= new ParallelTestConfiguration();
        var results = new ConcurrentBag<ParallelTestResult<T>>();
        var metrics = new ExecutionMetrics { TotalTests = iterations };

        var stopwatch = Stopwatch.StartNew();
        var initialMemory = GC.GetTotalMemory(true); // Force GC before measuring

        var parallelOptions = new ParallelOptions
        {
            MaxDegreeOfParallelism = config.MaxDegreeOfParallelism,
            CancellationToken = new CancellationTokenSource(config.DefaultTimeout).Token
        };

        // Use batching for better performance with large iteration counts
        var batches = Enumerable.Range(0, iterations)
            .Chunk(config.BatchSize)
            .ToList();

        await Parallel.ForEachAsync(
            batches,
            parallelOptions,
            async (batch, cancellationToken) =>
            {
                foreach (var index in batch)
                {
                    var testResult = new ParallelTestResult<T>
                    {
                        ThreadId = Thread.CurrentThread.ManagedThreadId,
                        StartTime = DateTime.UtcNow,
                        TestName = $"StressTest_{index}",
                        MemoryBefore = GC.GetTotalMemory(false)
                    };

                    var testStopwatch = Stopwatch.StartNew();

                    try
                    {
                        testResult.Result = await operation();
                        testResult.IsSuccess = true;
                    }
                    catch (Exception ex)
                    {
                        testResult.Exception = ex;
                        testResult.IsSuccess = false;
                    }
                    finally
                    {
                        testStopwatch.Stop();
                        testResult.Duration = testStopwatch.Elapsed;
                        testResult.EndTime = DateTime.UtcNow;
                        testResult.MemoryAfter = GC.GetTotalMemory(false);
                        results.Add(testResult);
                    }
                }
            });

        stopwatch.Stop();

        var finalMemory = GC.GetTotalMemory(false);
        var resultsList = results.ToList();

        // Calculate comprehensive stress test metrics
        metrics.TotalExecutionTime = stopwatch.Elapsed;
        metrics.SuccessfulTests = resultsList.Count(r => r.IsSuccess);
        metrics.FailedTests = resultsList.Count(r => !r.IsSuccess);
        metrics.ThreadsUsed = resultsList.Select(r => r.ThreadId).Distinct().Count();
        metrics.MemoryUsedBytes = finalMemory - initialMemory;

        if (resultsList.Any())
        {
            metrics.AverageTestDuration = TimeSpan.FromTicks((long)resultsList.Average(r => r.Duration.Ticks));
            metrics.MaxTestDuration = resultsList.Max(r => r.Duration);
            metrics.MinTestDuration = resultsList.Min(r => r.Duration);

            // Stress test specific metrics
            metrics.CustomMetrics["OperationsPerSecond"] = iterations / stopwatch.Elapsed.TotalSeconds;
            metrics.CustomMetrics["AverageMemoryPerOperation"] = (double)(finalMemory - initialMemory) / iterations;
            metrics.CustomMetrics["ThreadEfficiency"] = (double)iterations / (metrics.ThreadsUsed * stopwatch.Elapsed.TotalSeconds);
            
            // Performance percentiles
            var sortedDurations = resultsList.Select(r => r.Duration.TotalMilliseconds).OrderBy(d => d).ToList();
            metrics.CustomMetrics["P50Duration"] = GetPercentile(sortedDurations, 0.5);
            metrics.CustomMetrics["P90Duration"] = GetPercentile(sortedDurations, 0.9);
            metrics.CustomMetrics["P95Duration"] = GetPercentile(sortedDurations, 0.95);
            metrics.CustomMetrics["P99Duration"] = GetPercentile(sortedDurations, 0.99);
        }

        return metrics;
    }

    /// <summary>
    /// Executes concurrent safety tests to verify thread-safe behavior
    /// </summary>
    public static async Task<(bool IsThreadSafe, ExecutionMetrics Metrics, List<string> Issues)>
        ExecuteConcurrencySafetyTestAsync<T>(
            Func<Task<T>> operation,
            Func<List<T>, bool> validationFunc,
            int iterations = 10000,
            int threadCount = 0)
    {
        threadCount = threadCount > 0 ? threadCount : Environment.ProcessorCount * 2;
        var results = new ConcurrentBag<T>();
        var issues = new ConcurrentBag<string>();
        var metrics = new ExecutionMetrics { TotalTests = iterations };

        var stopwatch = Stopwatch.StartNew();
        var initialMemory = GC.GetTotalMemory(true);

        var parallelOptions = new ParallelOptions
        {
            MaxDegreeOfParallelism = threadCount
        };

        await Parallel.ForEachAsync(
            Enumerable.Range(0, iterations),
            parallelOptions,
            async (index, cancellationToken) =>
            {
                try
                {
                    var result = await operation();
                    results.Add(result);
                }
                catch (Exception ex)
                {
                    issues.Add($"Operation {index} failed: {ex.Message}");
                }
            });

        stopwatch.Stop();

        var resultsList = results.ToList();
        var issuesList = issues.ToList();

        // Validate results for thread safety
        bool isThreadSafe = true;
        try
        {
            isThreadSafe = validationFunc(resultsList) && issuesList.Count == 0;
        }
        catch (Exception ex)
        {
            isThreadSafe = false;
            issuesList.Add($"Validation failed: {ex.Message}");
        }

        // Check for race conditions
        if (resultsList.Count != iterations)
        {
            isThreadSafe = false;
            issuesList.Add($"Expected {iterations} results but got {resultsList.Count}");
        }

        var finalMemory = GC.GetTotalMemory(false);

        metrics.TotalExecutionTime = stopwatch.Elapsed;
        metrics.SuccessfulTests = resultsList.Count;
        metrics.FailedTests = issuesList.Count;
        metrics.ThreadsUsed = threadCount;
        metrics.MemoryUsedBytes = finalMemory - initialMemory;

        metrics.CustomMetrics["ThreadSafetyScore"] = isThreadSafe ? 100.0 : (double)metrics.SuccessfulTests / iterations * 100;
        metrics.CustomMetrics["ConcurrencyIssues"] = issuesList.Count;
        metrics.CustomMetrics["DataIntegrity"] = resultsList.Count == iterations;

        return (isThreadSafe, metrics, issuesList);
    }

    /// <summary>
    /// Benchmarks operation performance across different load scenarios
    /// </summary>
    public static async Task<Dictionary<string, ExecutionMetrics>> BenchmarkOperationAsync<T>(
        Func<Task<T>> operation,
        Dictionary<string, int> scenarios)
    {
        var results = new Dictionary<string, ExecutionMetrics>();

        foreach (var (scenarioName, iterations) in scenarios)
        {
            var config = new ParallelTestConfiguration
            {
                MaxDegreeOfParallelism = Math.Max(1, Environment.ProcessorCount),
                EnablePerformanceMonitoring = true,
                EnableResourceMonitoring = true
            };

            var metrics = await ExecuteStressTestAsync(operation, iterations, config);
            metrics.CustomMetrics["ScenarioName"] = scenarioName;
            metrics.CustomMetrics["Iterations"] = iterations;

            results[scenarioName] = metrics;
        }

        return results;
    }

    /// <summary>
    /// Generates a comprehensive performance report from execution metrics
    /// </summary>
    public static string GeneratePerformanceReport(ExecutionMetrics metrics, string testName = "Test")
    {
        var report = new System.Text.StringBuilder();
        
        report.AppendLine($"=== {testName} Performance Report ===");
        report.AppendLine($"Total Execution Time: {metrics.TotalExecutionTime.TotalMilliseconds:F2} ms");
        report.AppendLine($"Total Tests: {metrics.TotalTests:N0}");
        report.AppendLine($"Successful Tests: {metrics.SuccessfulTests:N0}");
        report.AppendLine($"Failed Tests: {metrics.FailedTests:N0}");
        report.AppendLine($"Success Rate: {metrics.SuccessRate:F2}%");
        report.AppendLine($"Tests per Second: {metrics.TestsPerSecond:F2}");
        report.AppendLine($"Threads Used: {metrics.ThreadsUsed}");
        report.AppendLine($"Memory Used: {metrics.MemoryUsedBytes / 1024.0 / 1024.0:F2} MB");

        if (metrics.TotalTests > 0)
        {
            report.AppendLine($"Average Test Duration: {metrics.AverageTestDuration.TotalMilliseconds:F2} ms");
            report.AppendLine($"Min Test Duration: {metrics.MinTestDuration.TotalMilliseconds:F2} ms");
            report.AppendLine($"Max Test Duration: {metrics.MaxTestDuration.TotalMilliseconds:F2} ms");
        }

        if (metrics.CustomMetrics.Any())
        {
            report.AppendLine("\nCustom Metrics:");
            foreach (var (key, value) in metrics.CustomMetrics)
            {
                report.AppendLine($"  {key}: {value}");
            }
        }

        return report.ToString();
    }

    /// <summary>
    /// Helper method to calculate percentiles
    /// </summary>
    private static double GetPercentile(IList<double> sortedData, double percentile)
    {
        if (!sortedData.Any()) return 0;
        
        var index = percentile * (sortedData.Count - 1);
        var lowerIndex = (int)Math.Floor(index);
        var upperIndex = (int)Math.Ceiling(index);
        
        if (lowerIndex == upperIndex)
            return sortedData[lowerIndex];
            
        return sortedData[lowerIndex] + (index - lowerIndex) * (sortedData[upperIndex] - sortedData[lowerIndex]);
    }

    /// <summary>
    /// Utility class for creating common test scenarios
    /// </summary>
    public static class TestScenarios
    {
        /// <summary>
        /// Creates performance benchmark scenarios
        /// </summary>
        public static Dictionary<string, int> CreatePerformanceScenarios()
        {
            return new Dictionary<string, int>
            {
                ["Baseline"] = 1_000,
                ["Small Load"] = 10_000,
                ["Medium Load"] = 100_000,
                ["Large Load"] = 1_000_000
            };
        }

        /// <summary>
        /// Creates memory pressure scenarios
        /// </summary>
        public static Dictionary<string, int> CreateMemoryScenarios()
        {
            return new Dictionary<string, int>
            {
                ["Low Memory"] = 10_000,
                ["Medium Memory"] = 100_000,
                ["High Memory"] = 500_000
            };
        }

        /// <summary>
        /// Creates concurrency scenarios
        /// </summary>
        public static Dictionary<string, (int threads, int operations)> CreateConcurrencyScenarios()
        {
            var coreCount = Environment.ProcessorCount;
            return new Dictionary<string, (int, int)>
            {
                ["Single Thread"] = (1, 10_000),
                ["CPU Optimal"] = (coreCount, 10_000),
                ["Oversubscribed"] = (coreCount * 2, 10_000),
                ["High Contention"] = (coreCount * 4, 5_000)
            };
        }
    }
}