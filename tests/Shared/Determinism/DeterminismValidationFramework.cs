using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using NUnit.Framework;
using Axon.Tests.Shared.TestBase;

namespace Axon.Tests.Shared.Determinism;

/// <summary>
/// Framework for validating test determinism and eliminating flakiness
/// </summary>
[TestFixture]
[Category("Determinism")]
[Category("Quality")]
public abstract class DeterminismValidationFramework : LondonSchoolTestBase
{
    protected DeterminismValidator DeterminismValidator { get; private set; } = null!;
    private static readonly ConcurrentDictionary<string, TestExecutionHistory> TestHistory = new();

    [SetUp]
    public override void LondonSchoolSetUp()
    {
        base.LondonSchoolSetUp();
        DeterminismValidator = new DeterminismValidator(this);
    }

    /// <summary>
    /// Validates that a test produces deterministic results across multiple runs
    /// </summary>
    protected async Task ValidateDeterministicBehavior(
        string testName,
        Func<Task<object>> testExecution,
        int iterations = 10,
        DeterminismOptions? options = null)
    {
        options ??= DeterminismOptions.Default;
        
        await DeterminismValidator
            .ForTest(testName)
            .WithIterations(iterations)
            .WithOptions(options)
            .ValidateAsync(testExecution);
    }

    /// <summary>
    /// Validates that a test with external dependencies behaves deterministically with mocks
    /// </summary>
    protected async Task ValidateMockedDeterministicBehavior<T>(
        string testName,
        Func<Mock<T>, Task<object>> testExecution,
        int iterations = 5,
        DeterminismOptions? options = null) where T : class
    {
        options ??= DeterminismOptions.Default;

        var results = new List<object>();
        var executionTimes = new List<TimeSpan>();

        for (int i = 0; i < iterations; i++)
        {
            // Create fresh mock for each iteration to ensure isolation
            var mock = CreateStrictMock<T>();
            
            var timer = System.Diagnostics.Stopwatch.StartNew();
            var result = await testExecution(mock);
            timer.Stop();
            
            results.Add(result);
            executionTimes.Add(timer.Elapsed);
            
            TestContext.WriteLine($"Iteration {i + 1}: {timer.ElapsedMilliseconds}ms");
        }

        // Validate deterministic results
        await ValidateResultConsistency(testName, results, options);
        await ValidateExecutionTimeConsistency(testName, executionTimes, options);
    }

    private async Task ValidateResultConsistency(string testName, List<object> results, DeterminismOptions options)
    {
        var serializedResults = results.Select(SerializeResult).ToList();
        var uniqueResults = serializedResults.Distinct().ToList();

        if (uniqueResults.Count > 1)
        {
            TestContext.WriteLine($"DETERMINISM VIOLATION: {testName} produced {uniqueResults.Count} different results:");
            for (int i = 0; i < Math.Min(uniqueResults.Count, 3); i++)
            {
                TestContext.WriteLine($"Result variant {i + 1}: {uniqueResults[i]}");
            }
            
            Assert.Fail($"Test {testName} is non-deterministic: produced {uniqueResults.Count} different results");
        }

        TestContext.WriteLine($"✓ {testName} produces consistent results across all iterations");
    }

    private async Task ValidateExecutionTimeConsistency(string testName, List<TimeSpan> executionTimes, DeterminismOptions options)
    {
        var times = executionTimes.Select(t => t.TotalMilliseconds).ToList();
        var average = times.Average();
        var maxDeviation = times.Max(t => Math.Abs(t - average));
        var coefficientOfVariation = CalculateCoefficientOfVariation(times);

        TestContext.WriteLine($"Execution time analysis for {testName}:");
        TestContext.WriteLine($"  Average: {average:F2}ms");
        TestContext.WriteLine($"  Max deviation: {maxDeviation:F2}ms");
        TestContext.WriteLine($"  Coefficient of variation: {coefficientOfVariation:F3}");

        if (coefficientOfVariation > options.MaxExecutionTimeVariation)
        {
            TestContext.WriteLine($"WARNING: High execution time variation detected (CV: {coefficientOfVariation:F3} > {options.MaxExecutionTimeVariation:F3})");
            TestContext.WriteLine("This may indicate non-deterministic timing dependencies");
        }

        // Store execution history for trend analysis
        StoreExecutionHistory(testName, times, coefficientOfVariation);
    }

    private static double CalculateCoefficientOfVariation(List<double> values)
    {
        if (values.Count < 2) return 0;
        
        var mean = values.Average();
        var variance = values.Sum(v => Math.Pow(v - mean, 2)) / (values.Count - 1);
        var standardDeviation = Math.Sqrt(variance);
        
        return mean > 0 ? standardDeviation / mean : 0;
    }

    private static string SerializeResult(object result)
    {
        try
        {
            var json = JsonSerializer.Serialize(result, new JsonSerializerOptions 
            { 
                WriteIndented = false,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });
            
            // Create hash for large results to avoid memory issues
            if (json.Length > 1000)
            {
                using var sha256 = SHA256.Create();
                var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(json));
                return Convert.ToHexString(hashBytes);
            }
            
            return json;
        }
        catch
        {
            // Fallback to string representation
            return result?.ToString() ?? "null";
        }
    }

    private static void StoreExecutionHistory(string testName, List<double> executionTimes, double coefficientOfVariation)
    {
        TestHistory.AddOrUpdate(testName, 
            new TestExecutionHistory
            {
                TestName = testName,
                ExecutionTimes = new List<double>(executionTimes),
                CoefficientOfVariation = coefficientOfVariation,
                LastRun = DateTime.UtcNow,
                TotalRuns = executionTimes.Count
            },
            (key, existing) =>
            {
                existing.ExecutionTimes.AddRange(executionTimes);
                existing.CoefficientOfVariation = coefficientOfVariation;
                existing.LastRun = DateTime.UtcNow;
                existing.TotalRuns += executionTimes.Count;
                return existing;
            });
    }

    /// <summary>
    /// Generates a determinism report for all tested components
    /// </summary>
    [Test]
    [Order(int.MaxValue)]
    public async Task GenerateDeterminismReport()
    {
        if (!TestHistory.Any())
        {
            TestContext.WriteLine("No determinism data collected in this test run");
            return;
        }

        TestContext.WriteLine("=== DETERMINISM VALIDATION REPORT ===");
        TestContext.WriteLine($"Total tests analyzed: {TestHistory.Count}");
        
        var highVariationTests = TestHistory.Values
            .Where(h => h.CoefficientOfVariation > 0.3)
            .OrderByDescending(h => h.CoefficientOfVariation)
            .ToList();

        if (highVariationTests.Any())
        {
            TestContext.WriteLine("\nTests with high execution time variation (potential flakiness):");
            foreach (var test in highVariationTests.Take(5))
            {
                TestContext.WriteLine($"  {test.TestName}: CV = {test.CoefficientOfVariation:F3} ({test.TotalRuns} runs)");
            }
        }
        else
        {
            TestContext.WriteLine("\n✓ All tests show consistent execution patterns");
        }

        var reportPath = Path.Combine(TestContext.CurrentContext.WorkDirectory, "determinism-report.json");
        var report = new
        {
            GeneratedAt = DateTime.UtcNow,
            TotalTests = TestHistory.Count,
            HighVariationTests = highVariationTests.Count,
            TestDetails = TestHistory.Values.Select(h => new
            {
                h.TestName,
                h.CoefficientOfVariation,
                h.TotalRuns,
                h.LastRun,
                AverageExecutionTime = h.ExecutionTimes.Average()
            }).ToList()
        };

        await File.WriteAllTextAsync(reportPath, JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
        TestContext.WriteLine($"\nDetailed report exported to: {reportPath}");
        TestContext.WriteLine("=====================================");
    }
}

/// <summary>
/// Validator for test determinism
/// </summary>
public class DeterminismValidator
{
    private readonly LondonSchoolTestBase _testBase;
    private string _testName = string.Empty;
    private int _iterations = 10;
    private DeterminismOptions _options = DeterminismOptions.Default;

    public DeterminismValidator(LondonSchoolTestBase testBase)
    {
        _testBase = testBase;
    }

    public DeterminismValidator ForTest(string testName)
    {
        _testName = testName;
        return this;
    }

    public DeterminismValidator WithIterations(int iterations)
    {
        _iterations = iterations;
        return this;
    }

    public DeterminismValidator WithOptions(DeterminismOptions options)
    {
        _options = options;
        return this;
    }

    public async Task ValidateAsync(Func<Task<object>> testExecution)
    {
        TestContext.WriteLine($"=== DETERMINISM VALIDATION: {_testName} ===");
        TestContext.WriteLine($"Running {_iterations} iterations to validate deterministic behavior...");

        var results = new List<object>();
        var executionTimes = new List<TimeSpan>();
        var exceptions = new List<Exception>();

        for (int i = 0; i < _iterations; i++)
        {
            try
            {
                // Introduce small delay to avoid timing-related issues
                if (_options.IterationDelay > TimeSpan.Zero)
                {
                    await Task.Delay(_options.IterationDelay);
                }

                var timer = System.Diagnostics.Stopwatch.StartNew();
                var result = await testExecution();
                timer.Stop();

                results.Add(result);
                executionTimes.Add(timer.Elapsed);

                if (i % Math.Max(1, _iterations / 4) == 0)
                {
                    TestContext.WriteLine($"Progress: {i + 1}/{_iterations} iterations completed");
                }
            }
            catch (Exception ex)
            {
                exceptions.Add(ex);
                TestContext.WriteLine($"Iteration {i + 1} failed: {ex.Message}");
            }
        }

        // Validate results
        await ValidateDeterministicResults(_testName, results, executionTimes, exceptions);
        
        TestContext.WriteLine($"=== DETERMINISM VALIDATION COMPLETED ===");
    }

    private async Task ValidateDeterministicResults(
        string testName, 
        List<object> results, 
        List<TimeSpan> executionTimes,
        List<Exception> exceptions)
    {
        // Check for consistent success/failure
        if (exceptions.Any() && exceptions.Count < results.Count)
        {
            Assert.Fail($"Test {testName} shows inconsistent exception behavior: {exceptions.Count} failures out of {results.Count + exceptions.Count} runs");
        }

        if (results.Any())
        {
            // All iterations succeeded - validate result consistency
            var serializedResults = results.Select(SerializeResult).ToList();
            var uniqueResults = serializedResults.Distinct().ToList();

            if (uniqueResults.Count > 1)
            {
                Assert.Fail($"Test {testName} produces non-deterministic results: {uniqueResults.Count} different outcomes");
            }

            TestContext.WriteLine($"✓ All {results.Count} iterations produced identical results");
        }
        
        if (exceptions.Any() && exceptions.Count == _iterations)
        {
            // All iterations failed - check for consistent exception types
            var exceptionTypes = exceptions.Select(e => e.GetType()).Distinct().ToList();
            if (exceptionTypes.Count > 1)
            {
                Assert.Fail($"Test {testName} produces non-deterministic exception types: {string.Join(", ", exceptionTypes.Select(t => t.Name))}");
            }

            TestContext.WriteLine($"✓ All {exceptions.Count} iterations failed consistently with {exceptionTypes.First().Name}");
        }
    }

    private static string SerializeResult(object result)
    {
        try
        {
            return JsonSerializer.Serialize(result, new JsonSerializerOptions 
            { 
                WriteIndented = false,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });
        }
        catch
        {
            return result?.ToString() ?? "null";
        }
    }
}

/// <summary>
/// Options for determinism validation
/// </summary>
public class DeterminismOptions
{
    public static DeterminismOptions Default => new();
    
    public double MaxExecutionTimeVariation { get; set; } = 0.5; // 50% coefficient of variation
    public TimeSpan IterationDelay { get; set; } = TimeSpan.FromMilliseconds(10);
    public bool EnableTimingAnalysis { get; set; } = true;
    public bool EnableResultHashing { get; set; } = true;
}

/// <summary>
/// Execution history for a test
/// </summary>
public class TestExecutionHistory
{
    public required string TestName { get; set; }
    public List<double> ExecutionTimes { get; set; } = new();
    public double CoefficientOfVariation { get; set; }
    public DateTime LastRun { get; set; }
    public int TotalRuns { get; set; }
}