using Axon.Shared.Common;
using Axon.Tests.Shared.Extensions;
using Axon.Tests.Shared.Builders;
using Axon.Tests.Shared.Generators;
using System.Text.Json;
using System.Runtime.Serialization.Formatters.Binary;

namespace Axon.Tests.Shared.Utilities;

/// <summary>
/// Advanced test suite for Result pattern covering serialization, pattern matching, functional composition, and edge cases
/// </summary>
[TestFixture]
[Parallelizable(ParallelScope.Self)]
public sealed class AdvancedResultTests
{
    [TestFixture]
    public class SerializationTests
    {
        [Test]
        public void Result_Success_ShouldSerializeToJson()
        {
            // Arrange
            var result = Result<string>.Success("test value");

            // Act
            var json = JsonSerializer.Serialize(result);
            var deserialized = JsonSerializer.Deserialize<Result<string>>(json);

            // Assert
            deserialized.IsSuccess.ShouldBeTrue();
            deserialized.Value.ShouldBe("test value");
        }

        [Test]
        public void Result_Failure_ShouldSerializeToJson()
        {
            // Arrange
            var error = Error.Validation("Test error", "TEST_CODE");
            var result = Result<string>.Failure(error);

            // Act
            var json = JsonSerializer.Serialize(result);
            var deserialized = JsonSerializer.Deserialize<Result<string>>(json);

            // Assert
            deserialized.IsFailure.ShouldBeTrue();
            deserialized.Error.Message.ShouldBe("Test error");
            deserialized.Error.Code.ShouldBe("TEST_CODE");
            deserialized.Error.Type.ShouldBe(ErrorType.Validation);
        }

        [Test]
        public void Result_WithComplexType_ShouldSerializeCorrectly()
        {
            // Arrange
            var user = new UserDto { Id = 1, Name = "John Doe", Email = "john@example.com" };
            var result = Result<UserDto>.Success(user);

            // Act
            var json = JsonSerializer.Serialize(result);
            var deserialized = JsonSerializer.Deserialize<Result<UserDto>>(json);

            // Assert
            deserialized.IsSuccess.ShouldBeTrue();
            deserialized.Value.Id.ShouldBe(1);
            deserialized.Value.Name.ShouldBe("John Doe");
            deserialized.Value.Email.ShouldBe("john@example.com");
        }

        [Test]
        public void Result_WithNestedExceptions_ShouldPreserveErrorInformation()
        {
            // Arrange
            var innerException = new ArgumentException("Inner error");
            var outerException = new InvalidOperationException("Outer error", innerException);
            var error = Error.InternalError("Complex error", "COMPLEX_ERROR", outerException);
            var result = Result<string>.Failure(error);

            // Act
            var json = JsonSerializer.Serialize(result);
            var deserialized = JsonSerializer.Deserialize<Result<string>>(json);

            // Assert
            deserialized.IsFailure.ShouldBeTrue();
            deserialized.Error.Message.ShouldBe("Complex error");
            deserialized.Error.Code.ShouldBe("COMPLEX_ERROR");
            // Note: Exception details may not serialize perfectly with System.Text.Json
        }

        [Test]
        public void Result_NonGeneric_ShouldSerializeCorrectly()
        {
            // Arrange
            var result = Result.Success();

            // Act
            var json = JsonSerializer.Serialize(result);
            var deserialized = JsonSerializer.Deserialize<Result>(json);

            // Assert
            deserialized.IsSuccess.ShouldBeTrue();
        }

        private class UserDto
        {
            public int Id { get; set; }
            public string Name { get; set; } = string.Empty;
            public string Email { get; set; } = string.Empty;
        }
    }

    [TestFixture]
    public class PatternMatchingTests
    {
        [Test]
        public void Result_Success_ShouldMatchSuccessPattern()
        {
            // Arrange
            var result = Result<int>.Success(42);
            string output = string.Empty;

            // Act
            if (result.IsSuccess)
            {
                output = $"Success: {result.Value}";
            }
            else
            {
                output = $"Error: {result.Error.Message}";
            }

            // Assert
            output.ShouldBe("Success: 42");
        }

        [Test]
        public void Result_Failure_ShouldMatchFailurePattern()
        {
            // Arrange
            var error = Error.NotFound("Item not found");
            var result = Result<int>.Failure(error);
            string output = string.Empty;

            // Act
            if (result.IsSuccess)
            {
                output = $"Success: {result.Value}";
            }
            else
            {
                output = $"Error: {result.Error.Message}";
            }

            // Assert
            output.ShouldBe("Error: Item not found");
        }

        [Test]
        public void Result_WithSwitchExpression_ShouldWorkCorrectly()
        {
            // Arrange
            var successResult = Result<string>.Success("success");
            var failureResult = Result<string>.Failure(Error.Validation("validation error"));

            // Act
            var successOutput = successResult switch
            {
                { IsSuccess: true } => $"Got value: {successResult.Value}",
                { IsFailure: true } => $"Got error: {successResult.Error.Message}",
                _ => "Unknown"
            };

            var failureOutput = failureResult switch
            {
                { IsSuccess: true } => $"Got value: {failureResult.Value}",
                { IsFailure: true } => $"Got error: {failureResult.Error.Message}",
                _ => "Unknown"
            };

            // Assert
            successOutput.ShouldBe("Got value: success");
            failureOutput.ShouldBe("Got error: validation error");
        }

        [Test]
        public void Result_WithErrorTypeMatching_ShouldDistinguishErrorTypes()
        {
            // Arrange
            var validationError = Result<string>.Failure(Error.Validation("Validation failed"));
            var notFoundError = Result<string>.Failure(Error.NotFound("Not found"));
            var internalError = Result<string>.Failure(Error.InternalError("Internal error"));

            // Act & Assert
            GetErrorCategory(validationError).ShouldBe("User Error");
            GetErrorCategory(notFoundError).ShouldBe("User Error");
            GetErrorCategory(internalError).ShouldBe("System Error");
        }

        private static string GetErrorCategory(Result<string> result)
        {
            return result.IsFailure ? result.Error.Type switch
            {
                ErrorType.Validation or ErrorType.NotFound or ErrorType.Conflict => "User Error",
                ErrorType.Unauthorized or ErrorType.Forbidden => "Auth Error",
                ErrorType.InternalError or ErrorType.ExternalService => "System Error",
                _ => "Unknown Error"
            } : "Success";
        }
    }

    [TestFixture]
    public class FunctionalCompositionTests
    {
        [Test]
        public void Result_Map_ShouldTransformSuccessValue()
        {
            // Arrange
            var result = Result<int>.Success(5);

            // Act
            var mappedResult = Map(result, x => x * 2);

            // Assert
            mappedResult.IsSuccess.ShouldBeTrue();
            mappedResult.Value.ShouldBe(10);
        }

        [Test]
        public void Result_Map_ShouldPreserveFailure()
        {
            // Arrange
            var error = Error.Validation("Input error");
            var result = Result<int>.Failure(error);

            // Act
            var mappedResult = Map(result, x => x * 2);

            // Assert
            mappedResult.IsFailure.ShouldBeTrue();
            mappedResult.Error.ShouldBe(error);
        }

        [Test]
        public void Result_Bind_ShouldChainSuccessfulOperations()
        {
            // Arrange
            var result = Result<int>.Success(5);

            // Act
            var chainedResult = Bind(result, x => x > 0 ? Result<string>.Success($"Positive: {x}") : Result<string>.Failure(Error.Validation("Not positive")));

            // Assert
            chainedResult.IsSuccess.ShouldBeTrue();
            chainedResult.Value.ShouldBe("Positive: 5");
        }

        [Test]
        public void Result_Bind_ShouldShortCircuitOnFailure()
        {
            // Arrange
            var error = Error.Validation("Initial error");
            var result = Result<int>.Failure(error);

            // Act
            var chainedResult = Bind(result, x => Result<string>.Success($"Value: {x}"));

            // Assert
            chainedResult.IsFailure.ShouldBeTrue();
            chainedResult.Error.ShouldBe(error);
        }

        [Test]
        public void Result_Bind_WithFailureInChain_ShouldReturnFailure()
        {
            // Arrange
            var result = Result<int>.Success(-5);

            // Act
            var chainedResult = Bind(result, x => x > 0 ? Result<string>.Success($"Positive: {x}") : Result<string>.Failure(Error.Validation("Not positive")));

            // Assert
            chainedResult.IsFailure.ShouldBeTrue();
            chainedResult.Error.Message.ShouldBe("Not positive");
        }

        [Test]
        public void Result_Match_ShouldExecuteCorrectFunction()
        {
            // Arrange
            var successResult = Result<int>.Success(42);
            var failureResult = Result<int>.Failure(Error.NotFound("Not found"));

            // Act
            var successOutput = Match(successResult, 
                onSuccess: value => $"Success: {value}",
                onFailure: error => $"Error: {error.Message}");

            var failureOutput = Match(failureResult,
                onSuccess: value => $"Success: {value}",
                onFailure: error => $"Error: {error.Message}");

            // Assert
            successOutput.ShouldBe("Success: 42");
            failureOutput.ShouldBe("Error: Not found");
        }

        [Test]
        public void Result_Combine_ShouldMergeMultipleResults()
        {
            // Arrange
            var result1 = Result<int>.Success(1);
            var result2 = Result<int>.Success(2);
            var result3 = Result<int>.Success(3);
            var failureResult = Result<int>.Failure(Error.Validation("Error"));

            // Act
            var allSuccess = CombineResults(result1, result2, result3);
            var withFailure = CombineResults(result1, failureResult, result3);

            // Assert
            allSuccess.IsSuccess.ShouldBeTrue();
            allSuccess.Value.ShouldBe(new[] { 1, 2, 3 });

            withFailure.IsFailure.ShouldBeTrue();
            withFailure.Error.Message.ShouldBe("Error");
        }

        // Helper methods for functional composition
        private static Result<TOutput> Map<TInput, TOutput>(Result<TInput> result, Func<TInput, TOutput> mapper)
        {
            return result.IsSuccess ? Result<TOutput>.Success(mapper(result.Value)) : Result<TOutput>.Failure(result.Error);
        }

        private static Result<TOutput> Bind<TInput, TOutput>(Result<TInput> result, Func<TInput, Result<TOutput>> binder)
        {
            return result.IsSuccess ? binder(result.Value) : Result<TOutput>.Failure(result.Error);
        }

        private static TOutput Match<TInput, TOutput>(Result<TInput> result, Func<TInput, TOutput> onSuccess, Func<Error, TOutput> onFailure)
        {
            return result.IsSuccess ? onSuccess(result.Value) : onFailure(result.Error);
        }

        private static Result<T[]> CombineResults<T>(params Result<T>[] results)
        {
            var values = new List<T>();
            foreach (var result in results)
            {
                if (result.IsFailure)
                    return Result<T[]>.Failure(result.Error);
                values.Add(result.Value);
            }
            return Result<T[]>.Success(values.ToArray());
        }
    }

    [TestFixture]
    public class AdvancedScenarioTests
    {
        [Test]
        public void Result_WithAsyncOperations_ShouldComposeCorrectly()
        {
            // This test demonstrates how Result<T> works with async/await patterns
            var result = ProcessAsync("valid input").GetAwaiter().GetResult();
            
            result.IsSuccess.ShouldBeTrue();
            result.Value.ShouldBe("Processed: valid input");
        }

        [Test]
        public void Result_WithAsyncFailure_ShouldPreserveError()
        {
            // This test demonstrates error preservation in async chains
            var result = ProcessAsync("").GetAwaiter().GetResult();
            
            result.IsFailure.ShouldBeTrue();
            result.Error.Type.ShouldBe(ErrorType.Validation);
        }

        [Test]
        public void Result_WithLinqOperations_ShouldWorkCorrectly()
        {
            // Arrange
            var results = new[]
            {
                Result<int>.Success(1),
                Result<int>.Success(2),
                Result<int>.Failure(Error.Validation("Error")),
                Result<int>.Success(4)
            };

            // Act
            var successfulResults = results.Where(r => r.IsSuccess).Select(r => r.Value).ToArray();
            var errors = results.Where(r => r.IsFailure).Select(r => r.Error).ToArray();

            // Assert
            successfulResults.ShouldBe(new[] { 1, 2, 4 });
            errors.Length.ShouldBe(1);
            errors[0].Type.ShouldBe(ErrorType.Validation);
        }

        [Test]
        public void Result_WithDeepNesting_ShouldHandleCorrectly()
        {
            // Arrange
            var deepResult = Result<Result<Result<string>>>.Success(
                Result<Result<string>>.Success(
                    Result<string>.Success("deep value")));

            // Act & Assert
            deepResult.IsSuccess.ShouldBeTrue();
            deepResult.Value.IsSuccess.ShouldBeTrue();
            deepResult.Value.Value.IsSuccess.ShouldBeTrue();
            deepResult.Value.Value.Value.ShouldBe("deep value");
        }

        [Test]
        public void Result_WithCustomEqualityComparison_ShouldWorkCorrectly()
        {
            // Arrange
            var person1 = new Person(1, "John");
            var person2 = new Person(1, "John");
            var person3 = new Person(2, "Jane");

            var result1 = Result<Person>.Success(person1);
            var result2 = Result<Person>.Success(person2);
            var result3 = Result<Person>.Success(person3);

            // Act & Assert
            result1.Value.ShouldBe(result2.Value); // Records have value equality
            result1.Value.ShouldNotBe(result3.Value);
        }

        [Test]
        public void Result_WithLargeDataSets_ShouldPerformWell()
        {
            // Arrange
            const int itemCount = 10_000;
            var largeArray = Enumerable.Range(1, itemCount).ToArray();
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            // Act
            var result = Result<int[]>.Success(largeArray);
            var processedResult = result.IsSuccess ? 
                Result<int>.Success(result.Value.Sum()) : 
                Result<int>.Failure(result.Error);

            stopwatch.Stop();

            // Assert
            processedResult.IsSuccess.ShouldBeTrue();
            processedResult.Value.ShouldBe(itemCount * (itemCount + 1) / 2); // Sum formula
            stopwatch.ElapsedMilliseconds.ShouldBeLessThan(100);
        }

        private static async Task<Result<string>> ProcessAsync(string input)
        {
            await Task.Delay(1); // Simulate async work
            
            if (string.IsNullOrEmpty(input))
                return Result<string>.Failure(Error.Validation("Input cannot be empty"));
                
            return Result<string>.Success($"Processed: {input}");
        }

        private record Person(int Id, string Name);
    }

    [TestFixture]
    public class EdgeCaseStressTests
    {
        [Test]
        public void Result_WithExtremelyLongErrorMessage_ShouldHandleCorrectly()
        {
            // Arrange
            var longMessage = new string('A', 100_000);
            var error = Error.Validation(longMessage);
            var result = Result<string>.Failure(error);

            // Act & Assert
            result.IsFailure.ShouldBeTrue();
            result.Error.Message.Length.ShouldBe(100_000);
        }

        [Test]
        public void Result_WithVeryDeepExceptionChain_ShouldPreserveInformation()
        {
            // Arrange
            Exception currentException = new ArgumentException("Base exception");
            for (int i = 1; i < 100; i++)
            {
                currentException = new InvalidOperationException($"Layer {i}", currentException);
            }

            var error = Error.InternalError("Deep exception chain", "DEEP_ERROR", currentException);
            var result = Result<string>.Failure(error);

            // Act & Assert
            result.IsFailure.ShouldBeTrue();
            result.Error.InnerException.ShouldNotBeNull();
            result.Error.InnerException!.Message.ShouldBe("Layer 99");
        }

        [Test]
        public void Result_WithNullReferenceScenarios_ShouldBehaveCorrectly()
        {
            // Arrange & Act & Assert
            Should.NotThrow(() => Result<string?>.Success(null));
            
            var nullResult = Result<string?>.Success(null);
            nullResult.IsSuccess.ShouldBeTrue();
            nullResult.Value.ShouldBeNull();
        }

        [Test]
        public void Result_WithUnicodeContent_ShouldPreserveCharacters()
        {
            // Arrange
            var unicodeValue = "🚀 Unicode test: 测试 العربية Ελληνικά 日本語 🎉";
            var result = Result<string>.Success(unicodeValue);

            // Act & Assert
            result.IsSuccess.ShouldBeTrue();
            result.Value.ShouldBe(unicodeValue);
        }

        [Test]
        public void Result_MemoryPressure_ShouldGarbageCollectProperly()
        {
            // Arrange
            const int iterations = 100_000;
            var initialMemory = GC.GetTotalMemory(true);

            // Act
            for (int i = 0; i < iterations; i++)
            {
                var result = Result<string>.Success($"Value {i}");
                var error = Error.Validation($"Error {i}");
                var failedResult = Result<string>.Failure(error);
                
                // Use the results to prevent optimization
                _ = result.IsSuccess;
                _ = failedResult.IsFailure;
            }

            // Force garbage collection
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            var finalMemory = GC.GetTotalMemory(false);

            // Assert
            var memoryIncrease = finalMemory - initialMemory;
            memoryIncrease.ShouldBeLessThan(50_000_000); // Should not consume excessive memory
        }
    }
}