using Axon.Shared.Common;
using System.Diagnostics;

namespace Axon.Tests.Shared.Performance;

/// <summary>
/// Performance benchmarks and stress tests for shared utilities
/// </summary>
[TestFixture]
[Category("Performance")]
public sealed class UtilitiesPerformanceTests
{
    private const int SmallIterations = 1_000;
    private const int MediumIterations = 10_000;
    private const int LargeIterations = 100_000;
    private const int StressIterations = 1_000_000;

    [TestFixture]
    public class ResultPerformanceTests
    {
        [Test]
        [Category("Performance")]
        public void Result_Creation_ShouldBeEfficient()
        {
            // Arrange
            var stopwatch = Stopwatch.StartNew();

            // Act
            for (int i = 0; i < LargeIterations; i++)
            {
                var result = Result<int>.Success(i);
            }

            stopwatch.Stop();

            // Assert
            stopwatch.ElapsedMilliseconds.ShouldBeLessThan(100); // Should be very fast for struct creation
            TestContext.WriteLine($"Created {LargeIterations:N0} Result<int> instances in {stopwatch.ElapsedMilliseconds}ms");
        }

        [Test]
        [Category("Performance")]
        public void Result_ImplicitConversion_ShouldBeEfficient()
        {
            // Arrange
            var stopwatch = Stopwatch.StartNew();

            // Act
            for (int i = 0; i < LargeIterations; i++)
            {
                Result<int> result = i; // Implicit conversion
            }

            stopwatch.Stop();

            // Assert
            stopwatch.ElapsedMilliseconds.ShouldBeLessThan(50);
            TestContext.WriteLine($"Performed {LargeIterations:N0} implicit conversions in {stopwatch.ElapsedMilliseconds}ms");
        }

        [Test]
        [Category("Performance")]
        public void Result_ValueAccess_ShouldBeEfficient()
        {
            // Arrange
            var results = new List<Result<int>>();
            for (int i = 0; i < MediumIterations; i++)
            {
                results.Add(Result<int>.Success(i));
            }

            var stopwatch = Stopwatch.StartNew();

            // Act
            for (int i = 0; i < MediumIterations; i++)
            {
                var value = results[i].Value;
            }

            stopwatch.Stop();

            // Assert
            stopwatch.ElapsedMilliseconds.ShouldBeLessThan(20);
            TestContext.WriteLine($"Accessed values from {MediumIterations:N0} Result<int> instances in {stopwatch.ElapsedMilliseconds}ms");
        }

        [Test]
        [Category("Performance")]
        public void Result_FailureCreation_ShouldBeEfficient()
        {
            // Arrange
            var error = Error.Validation("Test error");
            var stopwatch = Stopwatch.StartNew();

            // Act
            for (int i = 0; i < MediumIterations; i++)
            {
                var result = Result<int>.Failure(error);
            }

            stopwatch.Stop();

            // Assert
            stopwatch.ElapsedMilliseconds.ShouldBeLessThan(50);
            TestContext.WriteLine($"Created {MediumIterations:N0} failed Result<int> instances in {stopwatch.ElapsedMilliseconds}ms");
        }

        [Test]
        [Category("Performance")]
        public void Result_ConversionToNonGeneric_ShouldBeEfficient()
        {
            // Arrange
            var genericResults = new List<Result<string>>();
            for (int i = 0; i < MediumIterations; i++)
            {
                genericResults.Add(Result<string>.Success($"Value {i}"));
            }

            var stopwatch = Stopwatch.StartNew();

            // Act
            for (int i = 0; i < MediumIterations; i++)
            {
                Result nonGeneric = genericResults[i];
            }

            stopwatch.Stop();

            // Assert
            stopwatch.ElapsedMilliseconds.ShouldBeLessThan(30);
            TestContext.WriteLine($"Converted {MediumIterations:N0} Result<T> to Result in {stopwatch.ElapsedMilliseconds}ms");
        }

        [Test]
        [Category("Stress")]
        public void Result_StressTest_ShouldHandleMillionOperations()
        {
            // Arrange
            var stopwatch = Stopwatch.StartNew();
            var successCount = 0;
            var failureCount = 0;

            // Act
            for (int i = 0; i < StressIterations; i++)
            {
                if (i % 10 == 0)
                {
                    var failure = Result<int>.Failure(Error.Validation("Error"));
                    if (failure.IsFailure) failureCount++;
                }
                else
                {
                    var success = Result<int>.Success(i);
                    if (success.IsSuccess) successCount++;
                }
            }

            stopwatch.Stop();

            // Assert
            successCount.ShouldBe(900_000);
            failureCount.ShouldBe(100_000);
            stopwatch.ElapsedMilliseconds.ShouldBeLessThan(1000); // Should complete within 1 second
            TestContext.WriteLine($"Stress test completed {StressIterations:N0} operations in {stopwatch.ElapsedMilliseconds}ms");
        }

        [Test]
        [Category("Memory")]
        public void Result_MemoryUsage_ShouldBeMinimal()
        {
            // Arrange
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            var initialMemory = GC.GetTotalMemory(false);

            // Act
            var results = new Result<int>[LargeIterations];
            for (int i = 0; i < LargeIterations; i++)
            {
                results[i] = Result<int>.Success(i);
            }

            var finalMemory = GC.GetTotalMemory(false);
            var memoryUsed = finalMemory - initialMemory;
            var bytesPerResult = memoryUsed / (double)LargeIterations;

            // Assert
            bytesPerResult.ShouldBeLessThan(32); // Result<T> should be very memory efficient as a struct
            TestContext.WriteLine($"Memory usage: {memoryUsed:N0} bytes for {LargeIterations:N0} results ({bytesPerResult:F2} bytes per result)");
        }
    }

    [TestFixture]
    public class ErrorPerformanceTests
    {
        [Test]
        [Category("Performance")]
        public void Error_Creation_ShouldBeEfficient()
        {
            // Arrange
            var stopwatch = Stopwatch.StartNew();

            // Act
            for (int i = 0; i < MediumIterations; i++)
            {
                var error = Error.Validation($"Error {i}");
            }

            stopwatch.Stop();

            // Assert
            stopwatch.ElapsedMilliseconds.ShouldBeLessThan(100); // Error creation involves string allocation, so slightly slower
            TestContext.WriteLine($"Created {MediumIterations:N0} Error instances in {stopwatch.ElapsedMilliseconds}ms");
        }

        [Test]
        [Category("Performance")]
        public void Error_ToString_ShouldBeEfficient()
        {
            // Arrange
            var errors = new List<Error>();
            for (int i = 0; i < SmallIterations; i++)
            {
                errors.Add(Error.Validation($"Error {i}", $"CODE_{i}"));
            }

            var stopwatch = Stopwatch.StartNew();

            // Act
            for (int i = 0; i < SmallIterations; i++)
            {
                var str = errors[i].ToString();
            }

            stopwatch.Stop();

            // Assert
            stopwatch.ElapsedMilliseconds.ShouldBeLessThan(50);
            TestContext.WriteLine($"Converted {SmallIterations:N0} Error instances to string in {stopwatch.ElapsedMilliseconds}ms");
        }

        [Test]
        [Category("Performance")]
        public void Error_AllTypes_CreationPerformance()
        {
            // Arrange
            var errorTypes = new (Func<string, Error> Factory, string Name)[]
            {
                (msg => Error.Validation(msg), "Validation"),
                (msg => Error.NotFound(msg), "NotFound"),
                (msg => Error.Conflict(msg), "Conflict"),
                (msg => Error.InternalError(msg), "InternalError"),
                (msg => Error.ExternalService(msg), "ExternalService"),
                (msg => Error.Unauthorized(msg), "Unauthorized"),
                (msg => Error.Forbidden(msg), "Forbidden")
            };

            // Act & Assert
            foreach (var (factory, name) in errorTypes)
            {
                var stopwatch = Stopwatch.StartNew();
                
                for (int i = 0; i < SmallIterations; i++)
                {
                    var error = factory($"Message {i}");
                }
                
                stopwatch.Stop();
                stopwatch.ElapsedMilliseconds.ShouldBeLessThan(50);
                TestContext.WriteLine($"Created {SmallIterations:N0} {name} errors in {stopwatch.ElapsedMilliseconds}ms");
            }
        }

        [Test]
        [Category("Performance")]
        public void Error_WithException_ShouldHandleEfficiently()
        {
            // Arrange
            var exception = new InvalidOperationException("Test exception");
            var stopwatch = Stopwatch.StartNew();

            // Act
            for (int i = 0; i < SmallIterations; i++)
            {
                var error = Error.InternalError($"Error {i}", $"CODE_{i}", exception);
            }

            stopwatch.Stop();

            // Assert
            stopwatch.ElapsedMilliseconds.ShouldBeLessThan(50);
            TestContext.WriteLine($"Created {SmallIterations:N0} Error instances with exceptions in {stopwatch.ElapsedMilliseconds}ms");
        }

        [Test]
        [Category("Memory")]
        public void Error_MemoryUsage_ShouldBeReasonable()
        {
            // Arrange
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            var initialMemory = GC.GetTotalMemory(false);

            // Act
            var errors = new Error[SmallIterations];
            for (int i = 0; i < SmallIterations; i++)
            {
                errors[i] = Error.Validation($"Error message {i}", $"CODE_{i}");
            }

            var finalMemory = GC.GetTotalMemory(false);
            var memoryUsed = finalMemory - initialMemory;
            var bytesPerError = memoryUsed / (double)SmallIterations;

            // Assert
            bytesPerError.ShouldBeLessThan(200); // Error objects contain strings, so more memory than Result<T>
            TestContext.WriteLine($"Memory usage: {memoryUsed:N0} bytes for {SmallIterations:N0} errors ({bytesPerError:F2} bytes per error)");
        }
    }

    [TestFixture]
    public class ConcurrencyPerformanceTests
    {
        [Test]
        [Category("Performance")]
        [Category("Concurrency")]
        public void Result_ParallelCreation_ShouldScaleWell()
        {
            // Arrange
            const int itemsPerThread = MediumIterations / Environment.ProcessorCount;
            var stopwatch = Stopwatch.StartNew();

            // Act
            var results = Enumerable.Range(0, MediumIterations)
                .AsParallel()
                .Select(i => Result<int>.Success(i))
                .ToArray();

            stopwatch.Stop();

            // Assert
            results.Length.ShouldBe(MediumIterations);
            results.All(r => r.IsSuccess).ShouldBeTrue();
            stopwatch.ElapsedMilliseconds.ShouldBeLessThan(200);
            TestContext.WriteLine($"Created {MediumIterations:N0} Result<int> in parallel in {stopwatch.ElapsedMilliseconds}ms using {Environment.ProcessorCount} cores");
        }

        [Test]
        [Category("Performance")]
        [Category("Concurrency")]
        public void Error_ParallelCreation_ShouldScaleWell()
        {
            // Arrange
            var stopwatch = Stopwatch.StartNew();

            // Act
            var errors = Enumerable.Range(0, SmallIterations)
                .AsParallel()
                .Select(i => Error.Validation($"Error {i}", $"CODE_{i}"))
                .ToArray();

            stopwatch.Stop();

            // Assert
            errors.Length.ShouldBe(SmallIterations);
            errors.All(e => e.Type == ErrorType.Validation).ShouldBeTrue();
            stopwatch.ElapsedMilliseconds.ShouldBeLessThan(100);
            TestContext.WriteLine($"Created {SmallIterations:N0} Error instances in parallel in {stopwatch.ElapsedMilliseconds}ms");
        }

        [Test]
        [Category("Performance")]
        [Category("Concurrency")]
        public void Mixed_ResultAndError_ParallelOperations_ShouldPerformWell()
        {
            // Arrange
            var stopwatch = Stopwatch.StartNew();

            // Act
            var operations = Enumerable.Range(0, MediumIterations)
                .AsParallel()
                .Select(i =>
                {
                    if (i % 5 == 0)
                    {
                        var error = Error.Validation($"Error {i}");
                        return Result<int>.Failure(error);
                    }
                    return Result<int>.Success(i);
                })
                .ToArray();

            stopwatch.Stop();

            // Assert
            operations.Length.ShouldBe(MediumIterations);
            var successCount = operations.Count(r => r.IsSuccess);
            var failureCount = operations.Count(r => r.IsFailure);
            
            successCount.ShouldBe(8000); // 80% success (4 out of 5)
            failureCount.ShouldBe(2000); // 20% failure (1 out of 5)
            stopwatch.ElapsedMilliseconds.ShouldBeLessThan(300);
            
            TestContext.WriteLine($"Completed {MediumIterations:N0} mixed operations in parallel in {stopwatch.ElapsedMilliseconds}ms");
            TestContext.WriteLine($"Success: {successCount:N0}, Failures: {failureCount:N0}");
        }
    }

    [TestFixture]
    public class BenchmarkTests
    {
        [Test]
        [Category("Benchmark")]
        public void Benchmark_ResultVsException_PerformanceComparison()
        {
            // Test Result<T> pattern vs exception throwing for error handling
            const int iterations = 10_000;

            // Benchmark Result<T> approach
            var resultStopwatch = Stopwatch.StartNew();
            var resultSuccessCount = 0;
            var resultFailureCount = 0;

            for (int i = 0; i < iterations; i++)
            {
                var result = i % 10 == 0 ? 
                    Result<int>.Failure(Error.Validation("Error")) : 
                    Result<int>.Success(i);
                    
                if (result.IsSuccess) resultSuccessCount++;
                else resultFailureCount++;
            }
            resultStopwatch.Stop();

            // Benchmark exception approach
            var exceptionStopwatch = Stopwatch.StartNew();
            var exceptionSuccessCount = 0;
            var exceptionFailureCount = 0;

            for (int i = 0; i < iterations; i++)
            {
                try
                {
                    if (i % 10 == 0) throw new InvalidOperationException("Error");
                    exceptionSuccessCount++;
                }
                catch
                {
                    exceptionFailureCount++;
                }
            }
            exceptionStopwatch.Stop();

            // Assert - Result<T> should be significantly faster than exceptions
            resultStopwatch.ElapsedMilliseconds.ShouldBeLessThan(exceptionStopwatch.ElapsedMilliseconds);
            
            TestContext.WriteLine($"Result<T> approach: {resultStopwatch.ElapsedMilliseconds}ms (Success: {resultSuccessCount}, Failures: {resultFailureCount})");
            TestContext.WriteLine($"Exception approach: {exceptionStopwatch.ElapsedMilliseconds}ms (Success: {exceptionSuccessCount}, Failures: {exceptionFailureCount})");
            TestContext.WriteLine($"Result<T> is {(double)exceptionStopwatch.ElapsedMilliseconds / resultStopwatch.ElapsedMilliseconds:F1}x faster");
        }

        [Test]
        [Category("Benchmark")]
        public void Benchmark_ErrorCreation_VsDifferentPatterns()
        {
            const int iterations = 1_000;

            // Benchmark Error creation
            var errorStopwatch = Stopwatch.StartNew();
            for (int i = 0; i < iterations; i++)
            {
                var error = Error.Validation($"Error {i}");
            }
            errorStopwatch.Stop();

            // Benchmark string concatenation (alternative approach)
            var stringStopwatch = Stopwatch.StartNew();
            for (int i = 0; i < iterations; i++)
            {
                var errorString = $"[Validation] VALIDATION_ERROR: Error {i}";
            }
            stringStopwatch.Stop();

            // Error creation should be comparable to string operations
            TestContext.WriteLine($"Error creation: {errorStopwatch.ElapsedMilliseconds}ms");
            TestContext.WriteLine($"String creation: {stringStopwatch.ElapsedMilliseconds}ms");
            
            // Both should be reasonably fast
            errorStopwatch.ElapsedMilliseconds.ShouldBeLessThan(100);
            stringStopwatch.ElapsedMilliseconds.ShouldBeLessThan(100);
        }
    }
}