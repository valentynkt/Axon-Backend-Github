using Axon.Shared.Common;
using Axon.Tests.Shared.Builders;
using Axon.Tests.Shared.Generators;
using System.Collections.Concurrent;

namespace Axon.Tests.Shared.Fixtures;

/// <summary>
/// Reusable test fixtures for shared utilities testing
/// </summary>
public static class UtilitiesTestFixtures
{
    /// <summary>
    /// Thread-safe cache for test data to improve performance
    /// </summary>
    private static readonly ConcurrentDictionary<string, object> TestDataCache = new();

    /// <summary>
    /// Fixture for Result testing scenarios
    /// </summary>
    public static class ResultFixtures
    {
        /// <summary>
        /// Get common successful Result test cases
        /// </summary>
        public static IEnumerable<TestCaseData> SuccessfulResultTestCases()
        {
            yield return new TestCaseData(Result<string>.Success("test")).SetName("String Success");
            yield return new TestCaseData(Result<int>.Success(42)).SetName("Int Success");
            yield return new TestCaseData(Result<bool>.Success(true)).SetName("Bool Success");
            yield return new TestCaseData(Result<DateTime>.Success(DateTime.UtcNow)).SetName("DateTime Success");
            yield return new TestCaseData(Result<Guid>.Success(Guid.NewGuid())).SetName("Guid Success");
            yield return new TestCaseData(Result<decimal>.Success(3.14m)).SetName("Decimal Success");
            yield return new TestCaseData(Result<string?>.Success(null)).SetName("Nullable String Success");
            yield return new TestCaseData(Result<object>.Success(new { Name = "Test" })).SetName("Object Success");
        }

        /// <summary>
        /// Get common failed Result test cases
        /// </summary>
        public static IEnumerable<TestCaseData> FailedResultTestCases()
        {
            yield return new TestCaseData(Result<string>.Failure(Error.Validation("Validation error")))
                .SetName("String Validation Failure");
            yield return new TestCaseData(Result<int>.Failure(Error.NotFound("Not found")))
                .SetName("Int NotFound Failure");
            yield return new TestCaseData(Result<bool>.Failure(Error.Conflict("Conflict")))
                .SetName("Bool Conflict Failure");
            yield return new TestCaseData(Result<DateTime>.Failure(Error.InternalError("Internal error")))
                .SetName("DateTime Internal Failure");
            yield return new TestCaseData(Result<Guid>.Failure(Error.ExternalService("External service error")))
                .SetName("Guid External Service Failure");
            yield return new TestCaseData(Result<decimal>.Failure(Error.Unauthorized("Unauthorized")))
                .SetName("Decimal Unauthorized Failure");
            yield return new TestCaseData(Result<object>.Failure(Error.Forbidden("Forbidden")))
                .SetName("Object Forbidden Failure");
        }

        /// <summary>
        /// Get mixed Result test cases (both successful and failed)
        /// </summary>
        public static IEnumerable<TestCaseData> MixedResultTestCases()
        {
            var successful = SuccessfulResultTestCases();
            var failed = FailedResultTestCases();
            return successful.Concat(failed);
        }

        /// <summary>
        /// Get edge case Result test scenarios
        /// </summary>
        public static IEnumerable<TestCaseData> EdgeCaseResultTestCases()
        {
            yield return new TestCaseData(Result<string>.Success(string.Empty)).SetName("Empty String Success");
            yield return new TestCaseData(Result<string>.Success(new string('A', 10000))).SetName("Large String Success");
            yield return new TestCaseData(Result<int>.Success(int.MaxValue)).SetName("Max Int Success");
            yield return new TestCaseData(Result<int>.Success(int.MinValue)).SetName("Min Int Success");
            yield return new TestCaseData(Result<DateTime>.Success(DateTime.MinValue)).SetName("Min DateTime Success");
            yield return new TestCaseData(Result<DateTime>.Success(DateTime.MaxValue)).SetName("Max DateTime Success");
        }

        /// <summary>
        /// Get performance test data for Result
        /// </summary>
        public static IEnumerable<TestCaseData> PerformanceTestCases()
        {
            yield return new TestCaseData(1_000, typeof(string)).SetName("1K String Results");
            yield return new TestCaseData(10_000, typeof(int)).SetName("10K Int Results");
            yield return new TestCaseData(100_000, typeof(bool)).SetName("100K Bool Results");
        }
    }

    /// <summary>
    /// Fixture for Error testing scenarios
    /// </summary>
    public static class ErrorFixtures
    {
        /// <summary>
        /// Get all error type test cases
        /// </summary>
        public static IEnumerable<TestCaseData> AllErrorTypeTestCases()
        {
            var errorTypes = Enum.GetValues<ErrorType>();
            
            foreach (var errorType in errorTypes)
            {
                var error = errorType switch
                {
                    ErrorType.Validation => Error.Validation($"{errorType} message"),
                    ErrorType.NotFound => Error.NotFound($"{errorType} message"),
                    ErrorType.Conflict => Error.Conflict($"{errorType} message"),
                    ErrorType.InternalError => Error.InternalError($"{errorType} message"),
                    ErrorType.ExternalService => Error.ExternalService($"{errorType} message"),
                    ErrorType.Unauthorized => Error.Unauthorized($"{errorType} message"),
                    ErrorType.Forbidden => Error.Forbidden($"{errorType} message"),
                    _ => throw new ArgumentOutOfRangeException(nameof(errorType))
                };

                yield return new TestCaseData(error).SetName($"{errorType} Error");
            }
        }

        /// <summary>
        /// Get error test cases with exceptions
        /// </summary>
        public static IEnumerable<TestCaseData> ErrorsWithExceptionTestCases()
        {
            yield return new TestCaseData(
                Error.InternalError("Database error", "DB_ERROR", new InvalidOperationException("Connection failed")))
                .SetName("Internal Error with Exception");
            
            yield return new TestCaseData(
                Error.ExternalService("API error", "API_ERROR", new HttpRequestException("Network error")))
                .SetName("External Service Error with HTTP Exception");
            
            yield return new TestCaseData(
                Error.InternalError("Timeout error", "TIMEOUT_ERROR", new TimeoutException("Operation timed out")))
                .SetName("Internal Error with Timeout Exception");
        }

        /// <summary>
        /// Get edge case error test scenarios
        /// </summary>
        public static IEnumerable<TestCaseData> EdgeCaseErrorTestCases()
        {
            yield return new TestCaseData(Error.Validation("", "EMPTY_MESSAGE")).SetName("Empty Message Error");
            yield return new TestCaseData(Error.Validation(new string('A', 10000), "LARGE_MESSAGE")).SetName("Large Message Error");
            yield return new TestCaseData(Error.Validation("Unicode: 🚨 测试", "UNICODE_MESSAGE")).SetName("Unicode Message Error");
            yield return new TestCaseData(Error.Validation("Special: !@#$%^&*()", "SPECIAL_CHARS")).SetName("Special Characters Error");
        }

        /// <summary>
        /// Get business domain error scenarios
        /// </summary>
        public static IEnumerable<TestCaseData> BusinessErrorTestCases()
        {
            yield return new TestCaseData(ErrorBuilders.RequiredField("Email")).SetName("Required Field Error");
            yield return new TestCaseData(ErrorBuilders.InvalidFormat("Phone", "+1-XXX-XXX-XXXX")).SetName("Invalid Format Error");
            yield return new TestCaseData(ErrorBuilders.EntityNotFound("User", "123")).SetName("Entity Not Found Error");
            yield return new TestCaseData(ErrorBuilders.DuplicateEntity("Product", "SKU123")).SetName("Duplicate Entity Error");
            yield return new TestCaseData(ErrorBuilders.AuthenticationRequired()).SetName("Authentication Required Error");
            yield return new TestCaseData(ErrorBuilders.InsufficientPermissions("AdminPanel")).SetName("Insufficient Permissions Error");
        }
    }

    /// <summary>
    /// Fixture for integration testing scenarios
    /// </summary>
    public static class IntegrationFixtures
    {
        /// <summary>
        /// Get CRUD operation test scenarios
        /// </summary>
        public static IEnumerable<TestCaseData> CrudOperationTestCases()
        {
            var scenarios = TestDataGenerators.Scenarios.CrudScenarios();
            
            foreach (var (operation, expected) in scenarios)
            {
                yield return new TestCaseData(operation, expected).SetName($"CRUD {operation}");
            }
        }

        /// <summary>
        /// Get validation scenario test cases
        /// </summary>
        public static IEnumerable<TestCaseData> ValidationScenarioTestCases()
        {
            var scenarios = TestDataGenerators.Scenarios.ValidationScenarios();
            
            foreach (var (input, expected) in scenarios)
            {
                yield return new TestCaseData(input, expected).SetName($"Validation {input}");
            }
        }

        /// <summary>
        /// Get authentication scenario test cases
        /// </summary>
        public static IEnumerable<TestCaseData> AuthenticationScenarioTestCases()
        {
            var scenarios = TestDataGenerators.Scenarios.AuthenticationScenarios();
            
            foreach (var (username, password, expected) in scenarios)
            {
                yield return new TestCaseData(username, password, expected)
                    .SetName($"Auth {username}:{password}");
            }
        }

        /// <summary>
        /// Get performance scenario test cases
        /// </summary>
        public static IEnumerable<TestCaseData> PerformanceScenarioTestCases()
        {
            var scenarios = TestDataGenerators.Scenarios.PerformanceScenarios();
            
            foreach (var (itemCount, expectedMaxDuration) in scenarios)
            {
                yield return new TestCaseData(itemCount, expectedMaxDuration)
                    .SetName($"Performance {itemCount} items");
            }
        }

        /// <summary>
        /// Get concurrency scenario test cases
        /// </summary>
        public static IEnumerable<TestCaseData> ConcurrencyScenarioTestCases()
        {
            var scenarios = TestDataGenerators.Scenarios.ConcurrencyScenarios();
            
            foreach (var (threadCount, operationsPerThread, expectedResultType) in scenarios)
            {
                yield return new TestCaseData(threadCount, operationsPerThread, expectedResultType)
                    .SetName($"Concurrency {threadCount}x{operationsPerThread}");
            }
        }
    }

    /// <summary>
    /// Fixture for builders testing
    /// </summary>
    public static class BuilderFixtures
    {
        /// <summary>
        /// Get ResultBuilder test scenarios
        /// </summary>
        public static IEnumerable<TestCaseData> ResultBuilderTestCases()
        {
            yield return new TestCaseData(
                ResultBuilder<string>.Success().WithValue("test"))
                .SetName("String ResultBuilder Success");
            
            yield return new TestCaseData(
                ResultBuilder<int>.Success().WithValue(42))
                .SetName("Int ResultBuilder Success");
            
            yield return new TestCaseData(
                ResultBuilder<string>.Failure().WithValidationError("Required"))
                .SetName("String ResultBuilder Validation Failure");
            
            yield return new TestCaseData(
                ResultBuilder<int>.Failure().WithNotFoundError("Not found"))
                .SetName("Int ResultBuilder NotFound Failure");
        }

        /// <summary>
        /// Get ErrorBuilder test scenarios
        /// </summary>
        public static IEnumerable<TestCaseData> ErrorBuilderTestCases()
        {
            yield return new TestCaseData(
                ErrorBuilder.Validation().WithMessage("Test validation"))
                .SetName("Validation ErrorBuilder");
            
            yield return new TestCaseData(
                ErrorBuilder.NotFound().WithMessage("Not found").WithCode("NOT_FOUND"))
                .SetName("NotFound ErrorBuilder");
            
            yield return new TestCaseData(
                ErrorBuilder.InternalError().WithMessage("Internal").WithInnerException(new Exception("Inner")))
                .SetName("Internal ErrorBuilder with Exception");
        }
    }

    /// <summary>
    /// Utility methods for test fixtures
    /// </summary>
    public static class FixtureUtilities
    {
        /// <summary>
        /// Get cached test data to improve performance
        /// </summary>
        public static T GetCachedTestData<T>(string key, Func<T> factory) where T : class
        {
            return (T)TestDataCache.GetOrAdd(key, _ => factory()!);
        }

        /// <summary>
        /// Generate parameterized test cases from a collection
        /// </summary>
        public static IEnumerable<TestCaseData> GenerateTestCases<T>(
            IEnumerable<T> items, 
            Func<T, string> nameSelector)
        {
            return items.Select(item => new TestCaseData(item).SetName(nameSelector(item)));
        }

        /// <summary>
        /// Generate parameterized test cases with multiple parameters
        /// </summary>
        public static IEnumerable<TestCaseData> GenerateTestCases<T1, T2>(
            IEnumerable<(T1, T2)> items, 
            Func<T1, T2, string> nameSelector)
        {
            return items.Select(item => new TestCaseData(item.Item1, item.Item2)
                .SetName(nameSelector(item.Item1, item.Item2)));
        }

        /// <summary>
        /// Generate parameterized test cases with three parameters
        /// </summary>
        public static IEnumerable<TestCaseData> GenerateTestCases<T1, T2, T3>(
            IEnumerable<(T1, T2, T3)> items, 
            Func<T1, T2, T3, string> nameSelector)
        {
            return items.Select(item => new TestCaseData(item.Item1, item.Item2, item.Item3)
                .SetName(nameSelector(item.Item1, item.Item2, item.Item3)));
        }

        /// <summary>
        /// Clear test data cache
        /// </summary>
        public static void ClearCache()
        {
            TestDataCache.Clear();
        }

        /// <summary>
        /// Get cache statistics for performance monitoring
        /// </summary>
        public static (int Count, long MemoryEstimate) GetCacheStatistics()
        {
            var count = TestDataCache.Count;
            var memoryEstimate = count * 1024L; // Rough estimate
            return (count, memoryEstimate);
        }
    }

    /// <summary>
    /// Fixture for stress testing scenarios
    /// </summary>
    public static class StressTestFixtures
    {
        /// <summary>
        /// Get large dataset test cases for stress testing
        /// </summary>
        public static IEnumerable<TestCaseData> LargeDatasetTestCases()
        {
            yield return new TestCaseData(1_000).SetName("1K Dataset");
            yield return new TestCaseData(10_000).SetName("10K Dataset");
            yield return new TestCaseData(100_000).SetName("100K Dataset");
            yield return new TestCaseData(1_000_000).SetName("1M Dataset");
        }

        /// <summary>
        /// Get memory pressure test cases
        /// </summary>
        public static IEnumerable<TestCaseData> MemoryPressureTestCases()
        {
            yield return new TestCaseData(10_000, 1024).SetName("10K items, 1KB each");
            yield return new TestCaseData(1_000, 10_240).SetName("1K items, 10KB each");
            yield return new TestCaseData(100, 102_400).SetName("100 items, 100KB each");
        }

        /// <summary>
        /// Get concurrency stress test cases
        /// </summary>
        public static IEnumerable<TestCaseData> ConcurrencyStressTestCases()
        {
            yield return new TestCaseData(10, 1_000).SetName("10 threads, 1K ops each");
            yield return new TestCaseData(50, 200).SetName("50 threads, 200 ops each");
            yield return new TestCaseData(100, 100).SetName("100 threads, 100 ops each");
            yield return new TestCaseData(Environment.ProcessorCount * 2, 500).SetName("2x CPU threads, 500 ops each");
        }
    }
}