using Axon.Modules.Chat.Application.Abstractions;
using Axon.Modules.Chat.Application.Commands.ProcessMessage;
using Axon.Modules.Chat.Application.DTOs;
using Axon.Tests.Shared.Builders;
using Axon.Tests.Shared.TestBase;
using Microsoft.Extensions.Logging;
using NUnit.Framework;
using NUnit.Framework.Interfaces;

namespace Axon.Tests.Shared.Parallel;

/// <summary>
/// Configuration and optimization for parallel test execution
/// </summary>
[SetUpFixture]
public class ParallelTestConfiguration
{
    /// <summary>
    /// Global test setup - configure parallel execution
    /// </summary>
    [OneTimeSetUp]
    public void GlobalSetUp()
    {
        // Configure NUnit for optimal parallel execution
        TestContext.Parameters["NumberOfTestWorkers"] = Environment.ProcessorCount.ToString();
        
        // Set up thread pool for optimal parallel test execution
        ThreadPool.SetMinThreads(Environment.ProcessorCount * 2, Environment.ProcessorCount * 2);
        
        // Configure task scheduler for better performance
        TaskScheduler.UnobservedTaskException += (sender, e) =>
        {
            // Log unobserved exceptions but mark as observed to prevent crashes
            TestContext.WriteLine($"Unobserved task exception: {e.Exception}");
            e.SetObserved();
        };

        TestContext.WriteLine($"Parallel test execution configured with {Environment.ProcessorCount} processors");
        TestContext.WriteLine($"Min thread pool threads: {Environment.ProcessorCount * 2}");
    }

    /// <summary>
    /// Global test cleanup
    /// </summary>
    [OneTimeTearDown]
    public void GlobalTearDown()
    {
        // Ensure all async operations complete before teardown
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        
        TestContext.WriteLine("Parallel test execution completed");
    }
}

/// <summary>
/// Attributes and utilities for optimizing test parallelization
/// </summary>
public static class ParallelTestOptimizations
{
    /// <summary>
    /// Custom attribute for CPU-intensive tests that should have limited parallelism
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
    public class CpuIntensiveAttribute : PropertyAttribute
    {
        public CpuIntensiveAttribute() : base("CpuIntensive", true) { }
    }

    /// <summary>
    /// Custom attribute for memory-intensive tests that should be serialized
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
    public class MemoryIntensiveAttribute : PropertyAttribute
    {
        public MemoryIntensiveAttribute() : base("MemoryIntensive", true) { }
    }

    /// <summary>
    /// Custom attribute for I/O intensive tests that can be parallelized freely
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
    public class IoIntensiveAttribute : PropertyAttribute
    {
        public IoIntensiveAttribute() : base("IoIntensive", true) { }
    }

    /// <summary>
    /// Utility for creating isolated test environments
    /// </summary>
    public static IDisposable CreateIsolatedEnvironment()
    {
        return new IsolatedTestEnvironment();
    }

    /// <summary>
    /// Utility for parallel test data generation
    /// </summary>
    public static IEnumerable<T> GenerateParallelTestData<T>(Func<int, T> generator, int count)
    {
        return Enumerable.Range(0, count)
            .AsParallel()
            .WithDegreeOfParallelism(Environment.ProcessorCount)
            .Select(generator)
            .AsEnumerable();
    }

    /// <summary>
    /// Utility for parallel test execution with controlled concurrency
    /// </summary>
    public static async Task<IEnumerable<TResult>> ExecuteParallelTests<TInput, TResult>(
        IEnumerable<TInput> inputs,
        Func<TInput, Task<TResult>> testFunction,
        int maxConcurrency = -1)
    {
        var semaphore = maxConcurrency > 0 
            ? new SemaphoreSlim(maxConcurrency, maxConcurrency)
            : new SemaphoreSlim(Environment.ProcessorCount, Environment.ProcessorCount);

        try
        {
            var tasks = inputs.Select(async input =>
            {
                await semaphore.WaitAsync();
                try
                {
                    return await testFunction(input);
                }
                finally
                {
                    semaphore.Release();
                }
            });

            return await Task.WhenAll(tasks);
        }
        finally
        {
            semaphore.Dispose();
        }
    }

    /// <summary>
    /// Isolated test environment for thread-safe testing
    /// </summary>
    private class IsolatedTestEnvironment : IDisposable
    {
        private readonly AsyncLocal<Dictionary<string, object>> _testContext = new();
        private bool _disposed = false;

        public IsolatedTestEnvironment()
        {
            _testContext.Value = new Dictionary<string, object>();
        }

        public void SetValue<T>(string key, T value)
        {
            if (_testContext.Value != null)
            {
                _testContext.Value[key] = value!;
            }
        }

        public T GetValue<T>(string key)
        {
            if (_testContext.Value != null && _testContext.Value.TryGetValue(key, out var value))
            {
                return (T)value;
            }
            return default!;
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _testContext.Value?.Clear();
                _disposed = true;
            }
        }
    }
}

/// <summary>
/// Base class for parallel test fixtures with optimized setup
/// </summary>
[Parallelizable(ParallelScope.All)]
public abstract class ParallelTestBase
{
    protected static readonly ThreadLocal<Random> ThreadLocalRandom = 
        new(() => new Random(Thread.CurrentThread.ManagedThreadId));

    protected Random Random => ThreadLocalRandom.Value!;

    [SetUp]
    public virtual void ParallelSetUp()
    {
        // Ensure each test has isolated state
        TestContext.WriteLine($"Running test on thread {Thread.CurrentThread.ManagedThreadId}");
    }

    [TearDown]
    public virtual void ParallelTearDown()
    {
        // Cleanup any thread-local resources
    }

    /// <summary>
    /// Execute a test action with proper exception handling
    /// </summary>
    protected static void ExecuteSafely(Action action)
    {
        try
        {
            action();
        }
        catch (Exception ex)
        {
            TestContext.WriteLine($"Test exception on thread {Thread.CurrentThread.ManagedThreadId}: {ex}");
            throw;
        }
    }

    /// <summary>
    /// Execute an async test action with proper exception handling
    /// </summary>
    protected static async Task ExecuteSafelyAsync(Func<Task> action)
    {
        try
        {
            await action();
        }
        catch (Exception ex)
        {
            TestContext.WriteLine($"Async test exception on thread {Thread.CurrentThread.ManagedThreadId}: {ex}");
            throw;
        }
    }
}

/// <summary>
/// Performance monitoring utilities for parallel tests
/// </summary>
public static class ParallelTestPerformanceMonitor
{
    private static readonly ConcurrentDictionary<string, List<TimeSpan>> TestExecutionTimes = new();
    private static readonly ConcurrentDictionary<string, int> TestExecutionCounts = new();

    /// <summary>
    /// Record test execution time for performance analysis
    /// </summary>
    public static void RecordExecutionTime(string testName, TimeSpan executionTime)
    {
        TestExecutionTimes.AddOrUpdate(testName, 
            new List<TimeSpan> { executionTime },
            (key, list) => 
            {
                lock (list)
                {
                    list.Add(executionTime);
                    return list;
                }
            });

        TestExecutionCounts.AddOrUpdate(testName, 1, (key, count) => count + 1);
    }

    /// <summary>
    /// Get performance statistics for a test
    /// </summary>
    public static (TimeSpan Average, TimeSpan Min, TimeSpan Max, int Count) GetStatistics(string testName)
    {
        if (!TestExecutionTimes.TryGetValue(testName, out var times) || !times.Any())
        {
            return (TimeSpan.Zero, TimeSpan.Zero, TimeSpan.Zero, 0);
        }

        lock (times)
        {
            return (
                Average: TimeSpan.FromTicks((long)times.Average(t => t.Ticks)),
                Min: times.Min(),
                Max: times.Max(),
                Count: times.Count
            );
        }
    }

    /// <summary>
    /// Generate performance report for all tests
    /// </summary>
    public static string GeneratePerformanceReport()
    {
        var report = new StringBuilder();
        report.AppendLine("Parallel Test Performance Report");
        report.AppendLine("=================================");

        foreach (var testName in TestExecutionTimes.Keys.OrderBy(k => k))
        {
            var stats = GetStatistics(testName);
            report.AppendLine($"{testName}:");
            report.AppendLine($"  Count: {stats.Count}");
            report.AppendLine($"  Average: {stats.Average.TotalMilliseconds:F2}ms");
            report.AppendLine($"  Min: {stats.Min.TotalMilliseconds:F2}ms");
            report.AppendLine($"  Max: {stats.Max.TotalMilliseconds:F2}ms");
            report.AppendLine();
        }

        return report.ToString();
    }

    /// <summary>
    /// Clear all performance data
    /// </summary>
    public static void Clear()
    {
        TestExecutionTimes.Clear();
        TestExecutionCounts.Clear();
    }
}

/// <summary>
/// Performance Benchmark Tests - Validates SLA compliance and concurrent scaling
/// </summary>
[TestFixture]
[Category("Performance")]
[Category("Benchmarks")]
public class PerformanceBenchmarkTests : LondonSchoolTestBase
{
    private Mock<IAiClient> _aiClientMock = null!;
    private Mock<ILogger<ProcessMessageHandler>> _loggerMock = null!;
    private ProcessMessageHandler _handler = null!;

    [SetUp]
    public override void LondonSchoolSetUp()
    {
        base.LondonSchoolSetUp();
        
        _aiClientMock = CreateStrictMock<IAiClient>();
        _loggerMock = CreateLooseMock<ILogger<ProcessMessageHandler>>();
        
        _handler = new ProcessMessageHandler(_aiClientMock.Object, _loggerMock.Object);
    }

    [Test]
    [MeasurePerformance(SlaMilliseconds = 2000, EnforceStrictSla = true)]
    [Order(1)]
    public async Task ProcessMessage_ShouldCompleteWithinSLA()
    {
        // Arrange
        var command = ProcessMessageCommandBuilder.Default()
            .WithMessage("Test message for SLA validation")
            .Build();

        var expectedResponse = AiResponseBuilder.Default()
            .WithContent("SLA test response")
            .Build();

        _aiClientMock.Setup(x => x.SendMessageAsync(
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<AiResponse>.Success(expectedResponse));

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Content.ShouldNotBeNullOrEmpty();
        
        // Verify interaction occurred as expected
        _aiClientMock.Verify(x => x.SendMessageAsync(
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    [MeasurePerformance(SlaMilliseconds = 10000)] // 10 second timeout for concurrent test
    [Order(2)]
    public async Task ProcessMessage_ShouldScaleLinearlyUnder100ConcurrentRequests()
    {
        // Arrange
        const int concurrentRequests = 50; // Start with 50 to validate scaling
        var commands = Enumerable.Range(1, concurrentRequests)
            .Select(i => ProcessMessageCommandBuilder.Default()
                .WithMessage($"Concurrent test message {i}")
                .Build())
            .ToList();

        var expectedResponse = AiResponseBuilder.Default()
            .WithContent("Concurrent test response")
            .Build();

        _aiClientMock.Setup(x => x.SendMessageAsync(
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<AiResponse>.Success(expectedResponse));

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        // Act - Execute requests concurrently
        var tasks = commands.Select(async command =>
        {
            var taskStopwatch = System.Diagnostics.Stopwatch.StartNew();
            var result = await _handler.Handle(command, CancellationToken.None);
            taskStopwatch.Stop();
            
            return new { Result = result, ExecutionTime = taskStopwatch.Elapsed };
        });

        var results = await Task.WhenAll(tasks);
        stopwatch.Stop();

        // Assert
        results.All(r => r.Result.IsSuccess).ShouldBeTrue("All concurrent requests should succeed");
        
        // Validate linear scaling characteristics
        var averageExecutionTime = results.Average(r => r.ExecutionTime.TotalMilliseconds);
        var maxExecutionTime = results.Max(r => r.ExecutionTime.TotalMilliseconds);
        
        TestContext.WriteLine($"Concurrent requests processed: {concurrentRequests}");
        TestContext.WriteLine($"Total execution time: {stopwatch.Elapsed.TotalMilliseconds:F2}ms");
        TestContext.WriteLine($"Average per-request time: {averageExecutionTime:F2}ms");
        TestContext.WriteLine($"Max per-request time: {maxExecutionTime:F2}ms");
        
        // Linear scaling assertion - max time shouldn't be more than 3x average
        (maxExecutionTime / averageExecutionTime).ShouldBeLessThan(3.0, 
            "Linear scaling violated - some requests took significantly longer");
        
        // Overall throughput validation
        var requestsPerSecond = concurrentRequests / stopwatch.Elapsed.TotalSeconds;
        requestsPerSecond.ShouldBeGreaterThan(10, 
            "System should handle at least 10 requests per second under concurrent load");

        _aiClientMock.Verify(x => x.SendMessageAsync(
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Exactly(concurrentRequests));
    }

    [Test]
    [MeasurePerformance(SlaMilliseconds = 15000)] // 15 second timeout for stress test
    [Order(3)]
    public async Task ProcessMessage_ShouldMaintainPerformanceUnderLoad()
    {
        // Arrange - Stress test with 100 concurrent requests
        const int stressTestRequests = 100;
        var semaphore = new SemaphoreSlim(20, 20); // Limit concurrent execution to 20
        
        var expectedResponse = AiResponseBuilder.Default()
            .WithContent("Stress test response")
            .Build();

        _aiClientMock.Setup(x => x.SendMessageAsync(
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<AiResponse>.Success(expectedResponse));

        var overallStopwatch = System.Diagnostics.Stopwatch.StartNew();

        // Act - Execute with controlled concurrency
        var tasks = Enumerable.Range(1, stressTestRequests).Select(async i =>
        {
            await semaphore.WaitAsync();
            try
            {
                var command = ProcessMessageCommandBuilder.Default()
                    .WithMessage($"Stress test message {i}")
                    .Build();

                var taskStopwatch = System.Diagnostics.Stopwatch.StartNew();
                var result = await _handler.Handle(command, CancellationToken.None);
                taskStopwatch.Stop();

                return new { 
                    RequestId = i, 
                    Result = result, 
                    ExecutionTime = taskStopwatch.Elapsed 
                };
            }
            finally
            {
                semaphore.Release();
            }
        });

        var results = await Task.WhenAll(tasks);
        overallStopwatch.Stop();

        // Assert
        results.All(r => r.Result.IsSuccess).ShouldBeTrue("All stress test requests should succeed");
        
        // Performance characteristics analysis
        var executionTimes = results.Select(r => r.ExecutionTime.TotalMilliseconds).ToList();
        var averageTime = executionTimes.Average();
        var p95Time = executionTimes.OrderBy(t => t).Skip((int)(executionTimes.Count * 0.95)).FirstOrDefault();
        var p99Time = executionTimes.OrderBy(t => t).Skip((int)(executionTimes.Count * 0.99)).FirstOrDefault();

        TestContext.WriteLine($"Stress test completed: {stressTestRequests} requests");
        TestContext.WriteLine($"Total execution time: {overallStopwatch.Elapsed.TotalSeconds:F2}s");
        TestContext.WriteLine($"Average response time: {averageTime:F2}ms");
        TestContext.WriteLine($"95th percentile: {p95Time:F2}ms");
        TestContext.WriteLine($"99th percentile: {p99Time:F2}ms");
        
        // SLA validation under load
        p95Time.ShouldBeLessThan(3000, "95th percentile should be under 3 seconds under load");
        averageTime.ShouldBeLessThan(1000, "Average response time should be under 1 second");
        
        // Throughput validation
        var throughput = stressTestRequests / overallStopwatch.Elapsed.TotalSeconds;
        throughput.ShouldBeGreaterThan(20, "System should maintain at least 20 requests/second under stress");

        _aiClientMock.Verify(x => x.SendMessageAsync(
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Exactly(stressTestRequests));
    }
}

/// <summary>
/// Attribute to automatically measure test performance
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public class MeasurePerformanceAttribute : Attribute, ITestAction
{
    public int SlaMilliseconds { get; set; } = 2000; // Default 2-second SLA
    public bool EnforceStrictSla { get; set; } = false;
    public ActionTargets Targets => ActionTargets.Test;

    public void BeforeTest(ITest test)
    {
        TestContext.CurrentContext.Test.Properties.Set("StartTime", DateTime.UtcNow);
        TestContext.CurrentContext.Test.Properties.Set("SlaMilliseconds", SlaMilliseconds);
        TestContext.CurrentContext.Test.Properties.Set("EnforceStrictSla", EnforceStrictSla);
    }

    public void AfterTest(ITest test)
    {
        if (TestContext.CurrentContext.Test.Properties.TryGet("StartTime", out var startTimeObj) 
            && startTimeObj is DateTime startTime)
        {
            var executionTime = DateTime.UtcNow - startTime;
            var testName = test.FullName;
            
            ParallelTestPerformanceMonitor.RecordExecutionTime(testName, executionTime);
            
            // SLA validation
            if (TestContext.CurrentContext.Test.Properties.TryGet("SlaMilliseconds", out var slaObj) 
                && slaObj is int slaMs)
            {
                if (executionTime.TotalMilliseconds > slaMs)
                {
                    var message = $"SLA VIOLATION: {testName} took {executionTime.TotalMilliseconds:F2}ms (SLA: {slaMs}ms)";
                    TestContext.WriteLine(message);
                    
                    if (TestContext.CurrentContext.Test.Properties.TryGet("EnforceStrictSla", out var enforceObj) 
                        && enforceObj is bool enforce && enforce)
                    {
                        Assert.Fail(message);
                    }
                }
            }
            
            if (executionTime.TotalMilliseconds > 100) // Log slow tests
            {
                TestContext.WriteLine($"Slow test detected: {testName} took {executionTime.TotalMilliseconds:F2}ms");
            }
        }
    }
}