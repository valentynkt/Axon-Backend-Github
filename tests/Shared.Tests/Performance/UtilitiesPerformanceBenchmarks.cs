using NUnit.Framework;
using Shouldly;
using Axon.Shared.Common;
using Axon.Tests.Shared.Execution;
using Axon.Tests.Shared.Tests.Utilities;
using Axon.Tests.Shared.Generators;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;

namespace Axon.Tests.Shared.Tests.Performance;

/// <summary>
/// Comprehensive performance benchmarks for shared utilities including Result Error,
/// extension methods, and parallel operations. Integrates with the advanced parallel test framework.
/// </summary>
[TestFixture]
[Category("Shared")]
[Category("Performance")]
[Category("Benchmarks")]
public sealed class UtilitiesPerformanceBenchmarks
{
    #region Performance Benchmark Integration Tests

    /// <summary>
    /// Integration tests for performance benchmarking using the advanced parallel test framework
    /// </summary>
    [TestFixture]
    public class PerformanceBenchmarkIntegrationTests
    {
        [Test]
        [Category("PerformanceIntegration")]
        public async Task ResultOperations_ShouldMeetPerformanceTargets()
        {
            // Arrange
            var config = new AdvancedParallelTestFramework.ParallelTestConfiguration
            {
                MaxDegreeOfParallelism = Environment.ProcessorCount,
                EnablePerformanceMonitoring = true,
                EnableResourceMonitoring = true,
                BatchSize = 1000
            };

            var operations = GenerateResultOperations(10_000);

            // Act
            var (metrics, results) = await AdvancedParallelTestFramework.ExecuteResultOperationsAsync(operations, config);

            // Assert
            metrics.SuccessRate.ShouldBeGreaterThan(99.0, "Success rate should be above 99%");
            metrics.TestsPerSecond.ShouldBeGreaterThan(1000, "Should process at least 1000 operations per second");
            metrics.AverageTestDuration.TotalMilliseconds.ShouldBeLessThan(10, "Average operation should take less than 10ms");

            await TestContext.Out.WriteLineAsync(AdvancedParallelTestFramework.GeneratePerformanceReport(metrics, "Result Operations Benchmark"));
        }

        [Test]
        [Category("PerformanceIntegration")]
        public async Task ErrorOperations_ShouldMeetPerformanceTargets()
        {
            // Arrange
            var config = new AdvancedParallelTestFramework.ParallelTestConfiguration
            {
                MaxDegreeOfParallelism = Environment.ProcessorCount,
                EnablePerformanceMonitoring = true,
                BatchSize = 500
            };

            var operations = GenerateErrorOperations(5_000);

            // Act
            var (metrics, results) = await AdvancedParallelTestFramework.ExecuteErrorOperationsAsync(operations, config);

            // Assert
            metrics.SuccessRate.ShouldBeGreaterThan(99.0);
            metrics.TestsPerSecond.ShouldBeGreaterThan(500);

            await TestContext.Out.WriteLineAsync(AdvancedParallelTestFramework.GeneratePerformanceReport(metrics, "Error Operations Benchmark"));
        }

        [Test]
        [Category("PerformanceIntegration")]
        public async Task StressTest_ShouldHandleHighThroughput()
        {
            // Arrange
            var config = new AdvancedParallelTestFramework.ParallelTestConfiguration
            {
                MaxDegreeOfParallelism = Environment.ProcessorCount * 2,
                EnablePerformanceMonitoring = true,
                EnableResourceMonitoring = true,
                DefaultTimeout = TimeSpan.FromMinutes(2)
            };

            // Act
            var metrics = await AdvancedParallelTestFramework.ExecuteStressTestAsync(
                () => Task.FromResult(Result<string>.Success("stress-test-result")),
                100_000,
                config);

            // Assert
            metrics.SuccessRate.ShouldBeGreaterThan(99.5);
            metrics.CustomMetrics.Should().ContainKey("OperationsPerSecond");
            ((double)metrics.CustomMetrics["OperationsPerSecond"]).ShouldBeGreaterThan(5000);

            await TestContext.Out.WriteLineAsync(AdvancedParallelTestFramework.GeneratePerformanceReport(metrics, "High Throughput Stress Test"));
        }

        [Test]
        [Category("PerformanceIntegration")]
        public async Task ConcurrencySafetyTest_ShouldValidateThreadSafety()
        {
            // Arrange
            var concurrentBag = new ConcurrentBag<string>();

            // Act
            var (isThreadSafe, metrics, issues) = await AdvancedParallelTestFramework.ExecuteConcurrencySafetyTestAsync(
                () => Task.FromResult(Guid.NewGuid().ToString()),
                results => 
                {
                    // Validation: all results should be unique
                    return results.Distinct().Count() == results.Count;
                },
                50_000,
                Environment.ProcessorCount * 4);

            // Assert
            isThreadSafe.ShouldBeTrue("Operations should be thread-safe");
            issues.ShouldBeEmpty("No concurrency issues should be detected");
            metrics.CustomMetrics.Should().ContainKey("ThreadSafetyScore");
            ((double)metrics.CustomMetrics["ThreadSafetyScore"]).ShouldBe(100.0);

            await TestContext.Out.WriteLineAsync($"Thread Safety Test: {metrics.SuccessfulTests} operations, Thread Safety Score: {metrics.CustomMetrics["ThreadSafetyScore"]}%");
        }

        [Test]
        [Category("PerformanceIntegration")]
        public async Task BenchmarkScenarios_ShouldComparePerformance()
        {
            // Arrange
            var scenarios = AdvancedParallelTestFramework.TestScenarios.CreatePerformanceScenarios();

            // Act
            var benchmarkResults = await AdvancedParallelTestFramework.BenchmarkOperationAsync(
                () => CreateComplexResult(),
                scenarios);

            // Assert
            benchmarkResults.Should().HaveCount(scenarios.Count);
            
            foreach (var (scenarioName, result) in benchmarkResults)
            {
                result.SuccessRate.ShouldBeGreaterThan(99.0, $"Scenario {scenarioName} should have high success rate");
                await TestContext.Out.WriteLineAsync($"Scenario {scenarioName}: {result.TestsPerSecond:F0} ops/sec, {result.AverageTestDuration.TotalMilliseconds:F2}ms avg");
            }

            // Compare performance across scenarios
            var baselineResult = benchmarkResults["Baseline"];
            var largeLoadResult = benchmarkResults["Large Load"];
            
            // Large load should have reasonable degradation (not more than 50% slower per operation)
            var performanceDegradation = (baselineResult.AverageTestDuration.TotalMilliseconds / largeLoadResult.AverageTestDuration.TotalMilliseconds);
            performanceDegradation.ShouldBeGreaterThan(0.5, "Performance degradation should be acceptable");
        }

        #region Helper Methods

        private static IEnumerable<Func<Task<Result<string>>>> GenerateResultOperations(int count)
        {
            return Enumerable.Range(0, count).Select(i => new Func<Task<Result<string>>>(() =>
            {
                return i % 100 == 0 
                    ? Task.FromResult(Result<string>.Failure(Error.Validation($"Test error {i}", $"TEST_ERROR_{i}")))
                    : Task.FromResult(Result<string>.Success($"Success result {i}"));
            }));
        }

        private static IEnumerable<Func<Task<Error>>> GenerateErrorOperations(int count)
        {
            return Enumerable.Range(0, count).Select(i => new Func<Task<Error>>(() =>
            {
                var errorType = (ErrorType)(i % Enum.GetValues<ErrorType>().Length);
                var error = errorType switch
                {
                    ErrorType.Validation => Error.Validation($"Validation error {i}"),
                    ErrorType.NotFound => Error.NotFound($"Not found {i}"),
                    ErrorType.Conflict => Error.Conflict($"Conflict {i}"),
                    ErrorType.Unauthorized => Error.Unauthorized($"Unauthorized {i}"),
                    ErrorType.Forbidden => Error.Forbidden($"Forbidden {i}"),
                    ErrorType.InternalError => Error.InternalError($"Internal error {i}"),
                    ErrorType.ExternalService => Error.ExternalService($"External service error {i}"),
                    _ => Error.InternalError($"Unknown error {i}")
                };
                return Task.FromResult(error);
            }));
        }

        private static async Task<Result<ComplexObject>> CreateComplexResult()
        {
            await Task.Delay(1); // Simulate some work
            
            var obj = new ComplexObject
            {
                Id = Guid.NewGuid(),
                Name = $"Complex-{Random.Shared.Next(1000)}",
                Data = Enumerable.Range(0, 100).Select(i => $"Item-{i}").ToList(),
                Metadata = new Dictionary<string, object>
                {
                    ["CreatedAt"] = DateTime.UtcNow,
                    ["Version"] = "1.0",
                    ["Tags"] = new[] { "performance", "test", "benchmark" }
                }
            };

            return Result<ComplexObject>.Success(obj);
        }

        private record ComplexObject
        {
            public Guid Id { get; init; }
            public string Name { get; init; } = string.Empty;
            public List<string> Data { get; init; } = new();
            public Dictionary<string, object> Metadata { get; init; } = new();
        }

        #endregion
    }

    #endregion

    #region BenchmarkDotNet Integration

    /// <summary>
    /// BenchmarkDotNet benchmarks for precise performance measurements
    /// Note: These would normally be run separately using BenchmarkDotNet runner
    /// </summary>
    [TestFixture]
    public class BenchmarkDotNetIntegrationTests
    {
        [Test]
        [Category("BenchmarkIntegration")]
        [Explicit("Run manually for detailed benchmarking")]
        public void RunBenchmarks_ShouldExecuteBenchmarkSuite()
        {
            // This test demonstrates how to integrate BenchmarkDotNet
            // In practice, benchmarks would be run separately
            
            var summary = BenchmarkRunner.Run<UtilitiesBenchmarks>();
            summary.ShouldNotBeNull();
            
            TestContext.Out.WriteLine("BenchmarkDotNet summary:");
            TestContext.Out.WriteLine(summary.ToString());
        }
    }

    /// <summary>
    /// BenchmarkDotNet benchmark class for shared utilities
    /// </summary>
    [MemoryDiagnoser]
    [SimpleJob]
    public class UtilitiesBenchmarks
    {
        private Result<string> _successResult = null!;
        private Result<string> _failureResult = null!;
        private List<Result<int>> _resultList = null!;
        private string _testString = null!;
        private DateTime _testDateTime;

        [GlobalSetup]
        public void Setup()
        {
            _successResult = Result<string>.Success("benchmark test");
            _failureResult = Result<string>.Failure(Error.Validation("benchmark error"));
            _resultList = Enumerable.Range(0, 1000)
                .Select(i => i % 10 == 0 
                    ? Result<int>.Failure(Error.NotFound($"Item {i}")) 
                    : Result<int>.Success(i))
                .ToList();
            _testString = "This is a test string for benchmarking";
            _testDateTime = DateTime.UtcNow;
        }

        [Benchmark]
        public bool ResultSuccessCheck() => _successResult.IsSuccess;

        [Benchmark]
        public string ResultSuccessValue() => _successResult.Value;

        [Benchmark]
        public Error ResultFailureError() => _failureResult.Error;

        [Benchmark]
        public Result<string> CreateSuccessResult() => Result<string>.Success("new result");

        [Benchmark]
        public Result<string> CreateFailureResult() => Result<string>.Failure(Error.Validation("new error"));

        [Benchmark]
        public Error CreateValidationError() => Error.Validation("benchmark validation error");

        [Benchmark]
        public Error CreateNotFoundError() => Error.NotFound("benchmark not found error");

        [Benchmark]
        public int FilterSuccessfulResults() => _resultList.Count(r => r.IsSuccess);

        [Benchmark]
        public List<int> ExtractSuccessfulValues() => _resultList.Where(r => r.IsSuccess).Select(r => r.Value).ToList();

        [Benchmark]
        public string StringTruncation() => _testString.Length > 20 ? _testString[..20] : _testString;

        [Benchmark]
        public bool StringValidation() => !string.IsNullOrWhiteSpace(_testString);

        [Benchmark]
        public bool DateTimeWeekendCheck() => _testDateTime.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;

        [Benchmark]
        public DateTime DateTimeStartOfDay() => _testDateTime.Date;

        [Benchmark]
        public string JsonSerialization() => JsonSerializer.Serialize(new { Value = _testString, Date = _testDateTime });

        [Benchmark]
        public Dictionary<string, object> ObjectToDictionary() => new()
        {
            ["String"] = _testString,
            ["DateTime"] = _testDateTime,
            ["Success"] = _successResult.IsSuccess
        };
    }

    #endregion

    #region Memory Performance Testing

    /// <summary>
    /// Memory-focused performance testing for shared utilities
    /// </summary>
    [TestFixture]
    public class MemoryPerformanceTests
    {
        [Test]
        [Category("MemoryPerformance")]
        public void ResultOperations_ShouldNotLeakMemory()
        {
            // Arrange
            const int iterations = 100_000;
            
            GC.Collect();
            GC.WaitForPendingFinalizers();
            var initialMemory = GC.GetTotalMemory(false);

            // Act
            for (int i = 0; i < iterations; i++)
            {
                var result = Result<string>.Success($"test-{i}");
                var mapped = result.IsSuccess ? result.Value.ToUpper() : "default";
                
                if (i % 10000 == 0)
                {
                    GC.Collect();
                    GC.WaitForPendingFinalizers();
                }
            }

            GC.Collect();
            GC.WaitForPendingFinalizers();
            var finalMemory = GC.GetTotalMemory(false);

            // Assert
            var memoryIncrease = finalMemory - initialMemory;
            var memoryIncreaseKB = memoryIncrease / 1024.0;

            memoryIncreaseKB.ShouldBeLessThan(iterations / 1000.0, // Less than 1KB per 1000 operations
                $"Memory increase was {memoryIncreaseKB:F2} KB for {iterations} operations");

            TestContext.Out.WriteLine($"Memory performance: {iterations} operations increased memory by {memoryIncreaseKB:F2} KB");
        }

        [Test]
        [Category("MemoryPerformance")]
        public void ErrorCreation_ShouldEfficientlyManageMemory()
        {
            // Arrange
            const int iterations = 50_000;
            var errorTypes = Enum.GetValues<ErrorType>();
            
            GC.Collect();
            GC.WaitForPendingFinalizers();
            var initialMemory = GC.GetTotalMemory(false);

            // Act
            var errors = new List<Error>(iterations);
            for (int i = 0; i < iterations; i++)
            {
                var errorType = errorTypes[i % errorTypes.Length];
                var error = errorType switch
                {
                    ErrorType.Validation => Error.Validation($"Validation error {i}"),
                    ErrorType.NotFound => Error.NotFound($"Not found {i}"),
                    ErrorType.Conflict => Error.Conflict($"Conflict {i}"),
                    ErrorType.Unauthorized => Error.Unauthorized($"Unauthorized {i}"),
                    ErrorType.Forbidden => Error.Forbidden($"Forbidden {i}"),
                    ErrorType.InternalError => Error.InternalError($"Internal error {i}"),
                    ErrorType.ExternalService => Error.ExternalService($"External service error {i}"),
                    _ => Error.InternalError($"Unknown error {i}")
                };
                errors.Add(error);

                if (i % 10000 == 0)
                {
                    GC.Collect();
                    GC.WaitForPendingFinalizers();
                }
            }

            var finalMemory = GC.GetTotalMemory(false);

            // Assert
            errors.Count.ShouldBe(iterations);
            
            var memoryIncrease = finalMemory - initialMemory;
            var memoryPerError = memoryIncrease / (double)iterations;

            memoryPerError.ShouldBeLessThan(500, // Less than 500 bytes per error
                $"Memory per error was {memoryPerError:F2} bytes");

            TestContext.Out.WriteLine($"Error memory efficiency: {iterations} errors used {memoryIncrease / 1024.0:F2} KB ({memoryPerError:F0} bytes per error)");
        }

        [Test]
        [Category("MemoryPerformance")]
        public async Task ParallelOperations_ShouldManageMemoryEfficiently()
        {
            // Arrange
            const int operationCount = 20_000;
            const int batchSize = 1000;
            
            GC.Collect();
            GC.WaitForPendingFinalizers();
            var initialMemory = GC.GetTotalMemory(false);

            // Act
            var batches = Enumerable.Range(0, operationCount)
                .Select(i => new Func<Task<Result<string>>>(() => 
                    Task.FromResult(Result<string>.Success($"parallel-result-{i}"))))
                .Chunk(batchSize);

            foreach (var batch in batches)
            {
                var tasks = batch.Select(op => op());
                var results = await Task.WhenAll(tasks);
                
                results.ShouldAllBe(r => r.IsSuccess);
                
                // Force cleanup between batches
                GC.Collect();
                GC.WaitForPendingFinalizers();
            }

            var finalMemory = GC.GetTotalMemory(false);

            // Assert
            var memoryIncrease = finalMemory - initialMemory;
            var memoryIncreaseKB = memoryIncrease / 1024.0;

            memoryIncreaseKB.ShouldBeLessThan(operationCount / 500.0, // Less than 2KB per 1000 operations
                $"Parallel operations memory increase was {memoryIncreaseKB:F2} KB for {operationCount} operations");

            TestContext.Out.WriteLine($"Parallel memory efficiency: {operationCount} operations used {memoryIncreaseKB:F2} KB additional memory");
        }
    }

    #endregion

    #region Performance Regression Testing

    /// <summary>
    /// Performance regression testing to ensure performance doesn't degrade over time
    /// </summary>
    [TestFixture]
    public class PerformanceRegressionTests
    {
        private static readonly Dictionary<string, double> BaselineMetrics = new()
        {
            ["ResultCreationPerSecond"] = 1_000_000,
            ["ErrorCreationPerSecond"] = 500_000,
            ["ResultMappingPerSecond"] = 800_000,
            ["StringExtensionsPerSecond"] = 2_000_000,
            ["DateTimeExtensionsPerSecond"] = 1_500_000
        };

        [Test]
        [Category("PerformanceRegression")]
        public void ResultCreation_ShouldNotRegress()
        {
            // Arrange
            const int iterations = 1_000_000;
            var stopwatch = Stopwatch.StartNew();

            // Act
            for (int i = 0; i < iterations; i++)
            {
                var result = Result<int>.Success(i);
                var isSuccess = result.IsSuccess;
            }

            stopwatch.Stop();

            // Assert
            var operationsPerSecond = iterations / stopwatch.Elapsed.TotalSeconds;
            var baseline = BaselineMetrics["ResultCreationPerSecond"];
            
            operationsPerSecond.ShouldBeGreaterThan(baseline * 0.8, // Allow 20% degradation
                $"Result creation performance regressed: {operationsPerSecond:F0} ops/sec vs baseline {baseline:F0} ops/sec");

            TestContext.Out.WriteLine($"Result creation: {operationsPerSecond:F0} ops/sec (baseline: {baseline:F0} ops/sec)");
        }

        [Test]
        [Category("PerformanceRegression")]
        public void ErrorCreation_ShouldNotRegress()
        {
            // Arrange
            const int iterations = 500_000;
            var stopwatch = Stopwatch.StartNew();

            // Act
            for (int i = 0; i < iterations; i++)
            {
                var error = Error.Validation($"Error {i}");
                var type = error.Type;
            }

            stopwatch.Stop();

            // Assert
            var operationsPerSecond = iterations / stopwatch.Elapsed.TotalSeconds;
            var baseline = BaselineMetrics["ErrorCreationPerSecond"];
            
            operationsPerSecond.ShouldBeGreaterThan(baseline * 0.8,
                $"Error creation performance regressed: {operationsPerSecond:F0} ops/sec vs baseline {baseline:F0} ops/sec");

            TestContext.Out.WriteLine($"Error creation: {operationsPerSecond:F0} ops/sec (baseline: {baseline:F0} ops/sec)");
        }

        [Test]
        [Category("PerformanceRegression")]
        public void ExtensionMethods_ShouldNotRegress()
        {
            // Arrange
            const int iterations = 2_000_000;
            var testString = "This is a test string for performance testing";
            var stopwatch = Stopwatch.StartNew();

            // Act
            for (int i = 0; i < iterations; i++)
            {
                var hasValue = !string.IsNullOrWhiteSpace(testString);
                var truncated = testString.Length > 20 ? testString[..20] : testString;
            }

            stopwatch.Stop();

            // Assert
            var operationsPerSecond = iterations / stopwatch.Elapsed.TotalSeconds;
            var baseline = BaselineMetrics["StringExtensionsPerSecond"];
            
            operationsPerSecond.ShouldBeGreaterThan(baseline * 0.8,
                $"String extensions performance regressed: {operationsPerSecond:F0} ops/sec vs baseline {baseline:F0} ops/sec");

            TestContext.Out.WriteLine($"String extensions: {operationsPerSecond:F0} ops/sec (baseline: {baseline:F0} ops/sec)");
        }

        [Test]
        [Category("PerformanceRegression")]
        public async Task AsyncOperations_ShouldNotRegress()
        {
            // Arrange
            const int iterations = 10_000;
            var stopwatch = Stopwatch.StartNew();

            // Act
            var tasks = Enumerable.Range(0, iterations).Select(async i =>
            {
                var result = Result<string>.Success($"async-{i}");
                return result.IsSuccess ? result.Value : "default";
            });

            var results = await Task.WhenAll(tasks);
            stopwatch.Stop();

            // Assert
            results.Length.ShouldBe(iterations);
            var operationsPerSecond = iterations / stopwatch.Elapsed.TotalSeconds;
            
            operationsPerSecond.ShouldBeGreaterThan(5_000, // Minimum threshold for async operations
                $"Async operations performance: {operationsPerSecond:F0} ops/sec");

            TestContext.Out.WriteLine($"Async operations: {operationsPerSecond:F0} ops/sec");
        }
    }

    #endregion

    #region Cross-Platform Performance Testing

    /// <summary>
    /// Performance testing that considers cross-platform differences
    /// </summary>
    [TestFixture]
    public class CrossPlatformPerformanceTests
    {
        [Test]
        [Category("CrossPlatform")]
        public void PerformanceMetrics_ShouldBeConsistentAcrossPlatforms()
        {
            // Arrange
            var platformInfo = Environment.OSVersion.Platform;
            var processorCount = Environment.ProcessorCount;
            var is64Bit = Environment.Is64BitProcess;

            TestContext.Out.WriteLine($"Platform: {platformInfo}");
            TestContext.Out.WriteLine($"Processor Count: {processorCount}");
            TestContext.Out.WriteLine($"64-bit Process: {is64Bit}");

            // Act & Assert - Basic performance validation
            const int iterations = 100_000;
            var stopwatch = Stopwatch.StartNew();

            for (int i = 0; i < iterations; i++)
            {
                var result = Result<int>.Success(i);
                var value = result.IsSuccess ? result.Value : 0;
            }

            stopwatch.Stop();

            var operationsPerSecond = iterations / stopwatch.Elapsed.TotalSeconds;
            
            // Platform-specific minimum thresholds
            var minimumOpsPerSecond = platformInfo switch
            {
                PlatformID.Win32NT => 500_000,  // Windows
                PlatformID.Unix => 400_000,     // Linux/macOS
                PlatformID.MacOSX => 400_000,   // macOS
                _ => 300_000                     // Other platforms
            };

            operationsPerSecond.ShouldBeGreaterThan(minimumOpsPerSecond,
                $"Platform {platformInfo} performance below threshold: {operationsPerSecond:F0} ops/sec");

            TestContext.Out.WriteLine($"Cross-platform performance: {operationsPerSecond:F0} ops/sec on {platformInfo}");
        }
    }

    #endregion
}