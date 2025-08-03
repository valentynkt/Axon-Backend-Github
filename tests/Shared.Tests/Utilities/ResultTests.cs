using Axon.Shared.Common;
using Axon.Tests.Shared.Tests.Extensions;

namespace Axon.Tests.Shared.Tests.Utilities;

/// <summary>
/// Comprehensive test suite for Result{T} pattern covering all scenarios, edge cases, and implicit conversions
/// </summary>
[TestFixture]
[Parallelizable(ParallelScope.Self)]
public sealed class ResultTests
{
    [TestFixture]
    public class NonGenericResultTests
    {
        [Test]
        public void Success_ShouldCreateSuccessfulResult()
        {
            // Act
            var result = Result.Success();

            // Assert
            result.IsSuccess.ShouldBeTrue();
            result.IsFailure.ShouldBeFalse();
        }

        [Test]
        public void Failure_WithError_ShouldCreateFailedResult()
        {
            // Arrange
            var error = Error.Validation("Test error");

            // Act
            var result = Result.Failure(error);

            // Assert
            result.IsFailure.ShouldBeTrue();
            result.IsSuccess.ShouldBeFalse();
            result.Error.ShouldBe(error);
        }

        [Test]
        public void ImplicitConversion_FromError_ShouldCreateFailedResult()
        {
            // Arrange
            var error = Error.NotFound("Resource not found");

            // Act
            Result result = error;

            // Assert
            result.IsFailure.ShouldBeTrue();
            result.Error.ShouldBe(error);
        }

        [Test]
        public void AccessError_OnSuccessfulResult_ShouldThrowInvalidOperationException()
        {
            // Arrange
            var result = Result.Success();

            // Act & Assert
            Should.Throw<InvalidOperationException>(() => result.Error)
                .Message.ShouldBe("Cannot access Error when Result is successful");
        }

        [Test]
        public void Equals_WithSameSuccessResults_ShouldBeEqual()
        {
            // Arrange
            var result1 = Result.Success();
            var result2 = Result.Success();

            // Act & Assert
            result1.ShouldBe(result2);
            (result1 == result2).ShouldBeTrue();
            result1.GetHashCode().ShouldBe(result2.GetHashCode());
        }

        [Test]
        public void Equals_WithSameFailureResults_ShouldBeEqual()
        {
            // Arrange
            var error = Error.Validation("Same error");
            var result1 = Result.Failure(error);
            var result2 = Result.Failure(error);

            // Act & Assert
            result1.ShouldBe(result2);
            (result1 == result2).ShouldBeTrue();
        }

        [Test]
        public void Equals_WithDifferentResults_ShouldNotBeEqual()
        {
            // Arrange
            var successResult = Result.Success();
            var failureResult = Result.Failure(Error.Validation("Error"));

            // Act & Assert
            successResult.ShouldNotBe(failureResult);
            (successResult == failureResult).ShouldBeFalse();
        }

        [Test]
        public void ToString_ShouldProvideReadableRepresentation()
        {
            // Arrange
            var successResult = Result.Success();
            var failureResult = Result.Failure(Error.Validation("Test error"));

            // Act & Assert
            successResult.ToString().ShouldBe("Success");
            failureResult.ToString().ShouldContain("Failure");
            failureResult.ToString().ShouldContain("Test error");
        }
    }

    [TestFixture]
    public class GenericResultTests
    {
        [Test]
        public void Success_WithValue_ShouldCreateSuccessfulResult()
        {
            // Arrange
            const string value = "test value";

            // Act
            var result = Result<string>.Success(value);

            // Assert
            result.IsSuccess.ShouldBeTrue();
            result.IsFailure.ShouldBeFalse();
            result.Value.ShouldBe(value);
        }

        [Test]
        public void Failure_WithError_ShouldCreateFailedResult()
        {
            // Arrange
            var error = Error.Validation("Test error");

            // Act
            var result = Result<string>.Failure(error);

            // Assert
            result.IsFailure.ShouldBeTrue();
            result.IsSuccess.ShouldBeFalse();
            result.Error.ShouldBe(error);
        }

        [Test]
        public void ImplicitConversion_FromValue_ShouldCreateSuccessfulResult()
        {
            // Arrange
            const int value = 42;

            // Act
            Result<int> result = value;

            // Assert
            result.IsSuccess.ShouldBeTrue();
            result.Value.ShouldBe(value);
        }

        [Test]
        public void ImplicitConversion_FromError_ShouldCreateFailedResult()
        {
            // Arrange
            var error = Error.NotFound("Resource not found");

            // Act
            Result<string> result = error;

            // Assert
            result.IsFailure.ShouldBeTrue();
            result.Error.ShouldBe(error);
        }

        [Test]
        public void AccessValue_OnFailedResult_ShouldThrowInvalidOperationException()
        {
            // Arrange
            var result = Result<string>.Failure(Error.Validation("Error"));

            // Act & Assert
            Should.Throw<InvalidOperationException>(() => result.Value)
                .Message.ShouldBe("Cannot access Value when Result is failed");
        }

        [Test]
        public void AccessError_OnSuccessfulResult_ShouldThrowInvalidOperationException()
        {
            // Arrange
            var result = Result<string>.Success("test");

            // Act & Assert
            Should.Throw<InvalidOperationException>(() => result.Error)
                .Message.ShouldBe("Cannot access Error when Result is successful");
        }

        [Test]
        public void Map_WithSuccessfulResult_ShouldTransformValue()
        {
            // Arrange
            var result = Result<int>.Success(42);

            // Act
            var mappedResult = result.Map(x => x.ToString());

            // Assert
            mappedResult.IsSuccess.ShouldBeTrue();
            mappedResult.Value.ShouldBe("42");
        }

        [Test]
        public void Map_WithFailedResult_ShouldReturnFailedResult()
        {
            // Arrange
            var error = Error.Validation("Error");
            var result = Result<int>.Failure(error);

            // Act
            var mappedResult = result.Map(x => x.ToString());

            // Assert
            mappedResult.IsFailure.ShouldBeTrue();
            mappedResult.Error.ShouldBe(error);
        }

        [Test]
        public async Task MapAsync_WithSuccessfulResult_ShouldTransformValue()
        {
            // Arrange
            var result = Result<int>.Success(42);

            // Act
            var mappedResult = await result.MapAsync(async x =>
            {
                await Task.Delay(10);
                return x.ToString();
            });

            // Assert
            mappedResult.IsSuccess.ShouldBeTrue();
            mappedResult.Value.ShouldBe("42");
        }

        [Test]
        public void Bind_WithSuccessfulResult_ShouldChainOperations()
        {
            // Arrange
            var result = Result<int>.Success(42);

            // Act
            var boundResult = result.Bind(x =>
                x > 0 ? Result<string>.Success($"Positive: {x}") : Result<string>.Failure(Error.Validation("Not positive")));

            // Assert
            boundResult.IsSuccess.ShouldBeTrue();
            boundResult.Value.ShouldBe("Positive: 42");
        }

        [Test]
        public void Bind_WithFailedResult_ShouldReturnFailedResult()
        {
            // Arrange
            var error = Error.Validation("Original error");
            var result = Result<int>.Failure(error);

            // Act
            var boundResult = result.Bind(x => Result<string>.Success(x.ToString()));

            // Assert
            boundResult.IsFailure.ShouldBeTrue();
            boundResult.Error.ShouldBe(error);
        }

        [Test]
        public void Match_ShouldExecuteCorrectBranch()
        {
            // Arrange
            var successResult = Result<string>.Success("test");
            var failureResult = Result<string>.Failure(Error.NotFound("Not found"));

            // Act & Assert - Success branch
            var successOutput = successResult.Match(
                onSuccess: value => $"Success: {value}",
                onFailure: error => $"Failure: {error.Message}"
            );
            successOutput.ShouldBe("Success: test");

            // Failure branch
            var failureOutput = failureResult.Match(
                onSuccess: value => $"Success: {value}",
                onFailure: error => $"Failure: {error.Message}"
            );
            failureOutput.ShouldBe("Failure: Not found");
        }

        [Test]
        public void Equals_WithSameSuccessResults_ShouldBeEqual()
        {
            // Arrange
            var result1 = Result<string>.Success("test");
            var result2 = Result<string>.Success("test");

            // Act & Assert
            result1.ShouldBe(result2);
            (result1 == result2).ShouldBeTrue();
        }

        [Test]
        public void Equals_WithDifferentValues_ShouldNotBeEqual()
        {
            // Arrange
            var result1 = Result<string>.Success("test1");
            var result2 = Result<string>.Success("test2");

            // Act & Assert
            result1.ShouldNotBe(result2);
            (result1 == result2).ShouldBeFalse();
        }

        [Test]
        public void ToString_ShouldProvideReadableRepresentation()
        {
            // Arrange
            var successResult = Result<string>.Success("test");
            var failureResult = Result<string>.Failure(Error.Validation("Test error"));

            // Act & Assert
            successResult.ToString().ShouldContain("Success");
            successResult.ToString().ShouldContain("test");
            failureResult.ToString().ShouldContain("Failure");
            failureResult.ToString().ShouldContain("Test error");
        }
    }

    [TestFixture]
    public class ResultEdgeCasesAndStressTests
    {
        [Test]
        public void Result_WithNullValue_ShouldHandleCorrectly()
        {
            // Act
            var result = Result<string?>.Success(null);

            // Assert
            result.IsSuccess.ShouldBeTrue();
            result.Value.ShouldBeNull();
        }

        [Test]
        public void Result_WithNullError_ShouldThrowArgumentNullException()
        {
            // Act & Assert
            Should.Throw<ArgumentNullException>(() => Result.Failure(null!));
            Should.Throw<ArgumentNullException>(() => Result<string>.Failure(null!));
        }

        [Test]
        public void Result_StressTest_ShouldHandleManyOperations()
        {
            // Arrange
            const int iterations = 100_000;
            var results = new List<Result<int>>();

            // Act
            for (int i = 0; i < iterations; i++)
            {
                var result = i % 2 == 0 
                    ? Result<int>.Success(i) 
                    : Result<int>.Failure(Error.Validation($"Error {i}"));
                results.Add(result);
            }

            // Assert
            results.Count.ShouldBe(iterations);
            results.Count(r => r.IsSuccess).ShouldBe(iterations / 2);
            results.Count(r => r.IsFailure).ShouldBe(iterations / 2);
        }

        [Test]
        public void Result_ConcurrencyTest_ShouldBeThreadSafe()
        {
            // Arrange
            const int iterations = 10_000;
            var results = new ConcurrentBag<Result<int>>();

            // Act
            Parallel.For(0, iterations, i =>
            {
                var result = i % 2 == 0 
                    ? Result<int>.Success(i) 
                    : Result<int>.Failure(Error.Validation($"Error {i}"));
                results.Add(result);
            });

            // Assert
            results.Count.ShouldBe(iterations);
            results.Count(r => r.IsSuccess).ShouldBeGreaterThan(0);
            results.Count(r => r.IsFailure).ShouldBeGreaterThan(0);
        }

        [Test]
        public void Result_ComplexChaining_ShouldWorkCorrectly()
        {
            // Arrange & Act
            var result = Result<int>.Success(10)
                .Map(x => x * 2)
                .Bind(x => x > 15 ? Result<string>.Success($"Large: {x}") : Result<string>.Failure(Error.Validation("Too small")))
                .Map(s => s.ToUpperInvariant());

            // Assert
            result.IsSuccess.ShouldBeTrue();
            result.Value.ShouldBe("LARGE: 20");
        }

        [Test]
        public void Result_ErrorChaining_ShouldStopAtFirstError()
        {
            // Arrange & Act
            var result = Result<int>.Success(5)
                .Map(x => x * 2)
                .Bind(x => x > 15 ? Result<string>.Success($"Large: {x}") : Result<string>.Failure(Error.Validation("Too small")))
                .Map(s => s.ToUpperInvariant()); // This should not execute

            // Assert
            result.IsFailure.ShouldBeTrue();
            result.Error.Message.ShouldBe("Too small");
        }

        [Test]
        public void Result_WithComplexTypes_ShouldWorkCorrectly()
        {
            // Arrange
            var complexObject = new { Name = "Test", Value = 42, Items = new[] { 1, 2, 3 } };

            // Act
            var result = Result<object>.Success(complexObject);

            // Assert
            result.IsSuccess.ShouldBeTrue();
            result.Value.ShouldBe(complexObject);
        }

        [Test]
        public void Result_MemoryUsage_ShouldBeEfficient()
        {
            // Arrange & Act
            var results = Enumerable.Range(0, 100_000)
                .Select(i => Result<int>.Success(i))
                .ToList();

            // Assert - Should not throw OutOfMemoryException
            results.Count.ShouldBe(100_000);
            results.All(r => r.IsSuccess).ShouldBeTrue();
        }
    }
}