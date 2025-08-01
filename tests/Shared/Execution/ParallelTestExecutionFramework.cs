using System.Collections.Concurrent;
using System.Diagnostics;
using NUnit.Framework;

namespace Axon.Tests.Shared.Execution;

/// <summary>
/// Parallel test execution framework with proper isolation for London School TDD tests
/// </summary>
public static class ParallelTestExecutionFramework
{
    /// <summary>
    /// Executes multiple test scenarios in parallel with proper isolation
    /// </summary>
    public static async Task ExecuteInParallel<T>(params TestScenario<T>[] scenarios) where T : class
    {
        var tasks = scenarios.Select(scenario => ExecuteScenarioSafely(scenario)).ToArray();
        var results = await Task.WhenAll(tasks);

        // Aggregate any failures
        var failures = results.Where(r => r.Exception != null).ToList();
        if (failures.Any())
        {
            var aggregateException = new AggregateException(
                "One or more parallel test scenarios failed",
                failures.Select(f => f.Exception!));
            throw aggregateException;
        }
    }

    /// <summary>
    /// Executes a single test scenario with isolation and error handling
    /// </summary>
    private static async Task<TestResult> ExecuteScenarioSafely<T>(TestScenario<T> scenario) where T : class
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            await Task.Run(async () =>
            {
                // Create isolated test context
                using var isolationContext = new TestIsolationContext();
                
                // Execute the scenario
                await scenario.ExecuteAsync(isolationContext);
            });

            stopwatch.Stop();
            return new TestResult(scenario.Name, true, null, stopwatch.Elapsed);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            return new TestResult(scenario.Name, false, ex, stopwatch.Elapsed);
        }
    }

    /// <summary>
    /// Creates a batch of test scenarios for parallel execution
    /// </summary>
    public static ParallelTestBatch CreateBatch(string batchName)
    {
        return new ParallelTestBatch(batchName);
    }

    /// <summary>
    /// Executes tests with controlled concurrency
    /// </summary>
    public static async Task ExecuteWithConcurrencyLimit<T>(
        IEnumerable<TestScenario<T>> scenarios, 
        int maxConcurrency) where T : class
    {
        using var semaphore = new SemaphoreSlim(maxConcurrency, maxConcurrency);
        var tasks = scenarios.Select(async scenario =>
        {
            await semaphore.WaitAsync();
            try
            {
                return await ExecuteScenarioSafely(scenario);
            }
            finally
            {
                semaphore.Release();
            }
        }).ToArray();

        var results = await Task.WhenAll(tasks);
        
        // Check for failures
        var failures = results.Where(r => r.Exception != null).ToList();
        if (failures.Any())
        {
            throw new AggregateException(
                "One or more concurrent test scenarios failed",
                failures.Select(f => f.Exception!));
        }
    }

    /// <summary>
    /// Executes tests with resource pooling for expensive setup operations
    /// </summary>
    public static async Task ExecuteWithResourcePool<T, TResource>(
        IEnumerable<TestScenario<T>> scenarios,
        Func<TResource> resourceFactory,
        int poolSize) where T : class where TResource : class, IDisposable
    {
        var resourcePool = new ConcurrentQueue<TResource>();
        
        // Pre-populate resource pool
        for (int i = 0; i < poolSize; i++)
        {
            resourcePool.Enqueue(resourceFactory());
        }

        var tasks = scenarios.Select(async scenario =>
        {
            TResource? resource = null;
            try
            {
                // Try to get a resource from the pool
                if (!resourcePool.TryDequeue(out resource))
                {
                    resource = resourceFactory();
                }

                scenario.SharedResource = resource;
                return await ExecuteScenarioSafely(scenario);
            }
            finally
            {
                if (resource != null)
                {
                    resourcePool.Enqueue(resource);
                }
            }
        }).ToArray();

        var results = await Task.WhenAll(tasks);
        
        // Cleanup resource pool
        while (resourcePool.TryDequeue(out var resource))
        {
            resource.Dispose();
        }

        // Check for failures
        var failures = results.Where(r => r.Exception != null).ToList();
        if (failures.Any())
        {
            throw new AggregateException(
                "One or more pooled test scenarios failed",
                failures.Select(f => f.Exception!));
        }
    }
}

/// <summary>
/// Represents a single test scenario that can be executed in parallel
/// </summary>
public class TestScenario<T> where T : class
{
    public string Name { get; }
    public Func<TestIsolationContext, Task> TestAction { get; }
    public object? SharedResource { get; set; }

    public TestScenario(string name, Func<TestIsolationContext, Task> testAction)
    {
        Name = name;
        TestAction = testAction;
    }

    /// <summary>
    /// Executes the test scenario with the provided isolation context
    /// </summary>
    public async Task ExecuteAsync(TestIsolationContext context)
    {
        await TestAction(context);
    }

    /// <summary>
    /// Creates a test scenario with synchronous action
    /// </summary>
    public static TestScenario<T> Create(string name, Action<TestIsolationContext> testAction)
    {
        return new TestScenario<T>(name, context =>
        {
            testAction(context);
            return Task.CompletedTask;
        });
    }

    /// <summary>
    /// Creates a test scenario with asynchronous action
    /// </summary>
    public static TestScenario<T> CreateAsync(string name, Func<TestIsolationContext, Task> testAction)
    {
        return new TestScenario<T>(name, testAction);
    }
}

/// <summary>
/// Provides isolation context for parallel test execution
/// </summary>
public class TestIsolationContext : IDisposable
{
    private readonly Dictionary<Type, object> _services = new();
    private readonly List<IDisposable> _disposables = new();
    private bool _disposed = false;

    /// <summary>
    /// Registers a service in the isolation context
    /// </summary>
    public void RegisterService<T>(T service) where T : class
    {
        _services[typeof(T)] = service;
        
        if (service is IDisposable disposable)
        {
            _disposables.Add(disposable);
        }
    }

    /// <summary>
    /// Gets a service from the isolation context
    /// </summary>
    public T GetService<T>() where T : class
    {
        if (_services.TryGetValue(typeof(T), out var service))
        {
            return (T)service;
        }
        
        throw new InvalidOperationException($"Service of type {typeof(T).Name} not registered in isolation context");
    }

    /// <summary>
    /// Tries to get a service from the isolation context
    /// </summary>
    public bool TryGetService<T>(out T? service) where T : class
    {
        if (_services.TryGetValue(typeof(T), out var serviceObj))
        {
            service = (T)serviceObj;
            return true;
        }
        
        service = null;
        return false;
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            foreach (var disposable in _disposables)
            {
                try
                {
                    disposable.Dispose();
                }
                catch (Exception ex)
                {
                    TestContext.WriteLine($"Error disposing service: {ex.Message}");
                }
            }
            
            _services.Clear();
            _disposables.Clear();
            _disposed = true;
        }
    }
}

/// <summary>
/// Represents the result of a test scenario execution
/// </summary>
public record TestResult(string ScenarioName, bool Success, Exception? Exception, TimeSpan Duration);

/// <summary>
/// Builder for creating batches of parallel tests
/// </summary>
public class ParallelTestBatch
{
    private readonly string _batchName;
    private readonly List<object> _scenarios = new();

    public ParallelTestBatch(string batchName)
    {
        _batchName = batchName;
    }

    /// <summary>
    /// Adds a test scenario to the batch
    /// </summary>
    public ParallelTestBatch AddScenario<T>(TestScenario<T> scenario) where T : class
    {
        _scenarios.Add(scenario);
        return this;
    }

    /// <summary>
    /// Adds multiple test scenarios to the batch
    /// </summary>
    public ParallelTestBatch AddScenarios<T>(params TestScenario<T>[] scenarios) where T : class
    {
        _scenarios.AddRange(scenarios);
        return this;
    }

    /// <summary>
    /// Executes all scenarios in the batch with specified concurrency
    /// </summary>
    public async Task ExecuteAsync(int maxConcurrency = 0)
    {
        if (maxConcurrency <= 0) maxConcurrency = Environment.ProcessorCount;
        TestContext.WriteLine($"Executing batch '{_batchName}' with {_scenarios.Count} scenarios and max concurrency {maxConcurrency}");
        
        var stopwatch = Stopwatch.StartNew();
        
        using var semaphore = new SemaphoreSlim(maxConcurrency, maxConcurrency);
        var tasks = _scenarios.Select(async scenario =>
        {
            await semaphore.WaitAsync();
            try
            {
                // Execute scenario based on its type
                if (scenario is TestScenario<object> objectScenario)
                {
                    using var context = new TestIsolationContext();
                    await objectScenario.ExecuteAsync(context);
                    return new TestResult(objectScenario.Name, true, null, TimeSpan.Zero);
                }
                else
                {
                    throw new InvalidOperationException($"Unsupported scenario type: {scenario.GetType()}");
                }
            }
            catch (Exception ex)
            {
                return new TestResult("Unknown", false, ex, TimeSpan.Zero);
            }
            finally
            {
                semaphore.Release();
            }
        }).ToArray();

        var results = await Task.WhenAll(tasks);
        stopwatch.Stop();
        
        var successful = results.Count(r => r.Success);
        var failed = results.Length - successful;
        
        TestContext.WriteLine($"Batch '{_batchName}' completed in {stopwatch.Elapsed.TotalMilliseconds:F2}ms: {successful} successful, {failed} failed");

        // Throw if any failures
        var failures = results.Where(r => r.Exception != null).ToList();
        if (failures.Any())
        {
            throw new AggregateException(
                $"Batch '{_batchName}' had {failures.Count} failures",
                failures.Select(f => f.Exception!));
        }
    }
}

/// <summary>
/// Attributes for marking parallel test execution behavior
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public class ParallelExecutionAttribute : Attribute
{
    public int MaxConcurrency { get; set; } = Environment.ProcessorCount;
    public bool IsolateResources { get; set; } = true;
}

/// <summary>
/// Helper methods for creating parallel test scenarios
/// </summary>
public static class ParallelTestHelpers
{
    /// <summary>
    /// Creates a London School behavior test scenario
    /// </summary>
    public static TestScenario<T> CreateBehaviorScenario<T>(
        string scenarioName,
        Func<TestIsolationContext, T> systemUnderTestFactory,
        Func<T, Task> testAction) where T : class
    {
        return TestScenario<T>.CreateAsync(scenarioName, async context =>
        {
            var systemUnderTest = systemUnderTestFactory(context);
            await testAction(systemUnderTest);
        });
    }

    /// <summary>
    /// Creates an interaction test scenario
    /// </summary>
    public static TestScenario<T> CreateInteractionScenario<T>(
        string scenarioName,
        Func<TestIsolationContext, T> systemUnderTestFactory,
        Action<T> testAction,
        params Action[] verifications) where T : class
    {
        return TestScenario<T>.Create(scenarioName, context =>
        {
            var systemUnderTest = systemUnderTestFactory(context);
            testAction(systemUnderTest);
            
            // Execute all verifications
            foreach (var verification in verifications)
            {
                verification();
            }
        });
    }

    /// <summary>
    /// Creates a contract test scenario
    /// </summary>
    public static TestScenario<T> CreateContractScenario<T>(
        string scenarioName,
        Func<TestIsolationContext, T> systemUnderTestFactory,
        Func<T, Task> contractTest) where T : class
    {
        return TestScenario<T>.CreateAsync(scenarioName, async context =>
        {
            var systemUnderTest = systemUnderTestFactory(context);
            await contractTest(systemUnderTest);
        });
    }
}