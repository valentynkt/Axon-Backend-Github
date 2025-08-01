using Axon.Shared.Common;
using Axon.Tests.Shared.Extensions;
using Axon.Tests.Shared.TestBase;

namespace Axon.Tests.Shared.Utilities;

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
        public void ToString_OnSuccessResult_ShouldReturnSuccessString()
        {
            // Arrange
            var result = Result.Success();

            // Act
            var stringRepresentation = result.ToString();

            // Assert
            stringRepresentation.ShouldContain("Success");
        }

        [Test]
        public void ToString_OnFailureResult_ShouldReturnErrorString()
        {
            // Arrange
            var error = Error.Validation("Test validation error");
            var result = Result.Failure(error);

            // Act
            var stringRepresentation = result.ToString();

            // Assert
            stringRepresentation.ShouldContain("Failure");
            stringRepresentation.ShouldContain(error.ToString());
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
        public void ImplicitConversion_ToNonGenericResult_FromSuccessful_ShouldCreateSuccessfulResult()
        {
            // Arrange
            var genericResult = Result<string>.Success("test");

            // Act
            Result nonGenericResult = genericResult;

            // Assert
            nonGenericResult.IsSuccess.ShouldBeTrue();
        }

        [Test]
        public void ImplicitConversion_ToNonGenericResult_FromFailed_ShouldCreateFailedResult()
        {
            // Arrange
            var error = Error.Validation("Test error");
            var genericResult = Result<string>.Failure(error);

            // Act
            Result nonGenericResult = genericResult;

            // Assert
            nonGenericResult.IsFailure.ShouldBeTrue();
            nonGenericResult.Error.ShouldBe(error);
        }

        [Test]
        public void AccessValue_OnFailedResult_ShouldThrowInvalidOperationException()
        {
            // Arrange
            var result = Result<string>.Failure(Error.Validation("Test error"));

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
        public void Equals_WithSameSuccessResults_ShouldBeEqual()
        {
            // Arrange
            var result1 = Result<string>.Success("test");
            var result2 = Result<string>.Success("test");

            // Act & Assert
            result1.ShouldBe(result2);
            (result1 == result2).ShouldBeTrue();
            result1.GetHashCode().ShouldBe(result2.GetHashCode());
        }

        [Test]
        public void Equals_WithDifferentSuccessValues_ShouldNotBeEqual()
        {
            // Arrange
            var result1 = Result<string>.Success("test1");
            var result2 = Result<string>.Success("test2");

            // Act & Assert
            result1.ShouldNotBe(result2);
            (result1 == result2).ShouldBeFalse();
        }

        [Test]
        public void Equals_WithSameFailureResults_ShouldBeEqual()
        {
            // Arrange
            var error = Error.Validation("Same error");
            var result1 = Result<string>.Failure(error);
            var result2 = Result<string>.Failure(error);

            // Act & Assert
            result1.ShouldBe(result2);
        }

        [Test]
        public void Equals_WithDifferentResults_ShouldNotBeEqual()
        {
            // Arrange
            var successResult = Result<string>.Success("test");
            var failureResult = Result<string>.Failure(Error.Validation("Error"));

            // Act & Assert
            successResult.ShouldNotBe(failureResult);
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("valid string")]
        public void Success_WithNullableReferenceTypes_ShouldHandleCorrectly(string? value)
        {
            // Act
            var result = Result<string?>.Success(value);

            // Assert
            result.IsSuccess.ShouldBeTrue();
            result.Value.ShouldBe(value);
        }

        [Test]
        public void Success_WithValueTypes_ShouldHandleCorrectly()
        {
            // Arrange
            var dateTime = DateTime.UtcNow;

            // Act
            var result = Result<DateTime>.Success(dateTime);

            // Assert
            result.IsSuccess.ShouldBeTrue();
            result.Value.ShouldBe(dateTime);
        }

        [Test]
        public void Success_WithComplexTypes_ShouldHandleCorrectly()
        {
            // Arrange
            var complexObject = new { Name = "Test", Value = 42 };

            // Act
            var result = Result<object>.Success(complexObject);

            // Assert
            result.IsSuccess.ShouldBeTrue();
            result.Value.ShouldBe(complexObject);
        }

        [Test]
        public void ToString_OnSuccessResult_ShouldIncludeValue()
        {
            // Arrange
            const string value = "test value";
            var result = Result<string>.Success(value);

            // Act
            var stringRepresentation = result.ToString();

            // Assert
            stringRepresentation.ShouldContain("Success");
            stringRepresentation.ShouldContain(value);
        }

        [Test]
        public void ToString_OnFailureResult_ShouldIncludeError()
        {
            // Arrange
            var error = Error.Validation("Test validation error");
            var result = Result<string>.Failure(error);

            // Act
            var stringRepresentation = result.ToString();

            // Assert
            stringRepresentation.ShouldContain("Failure");
            stringRepresentation.ShouldContain(error.ToString());
        }
    }

    [TestFixture]
    public class ResultEdgeCasesAndStressTests
    {
        [Test]
        public void Result_WithLargeString_ShouldHandleCorrectly()
        {
            // Arrange
            var largeString = new string('A', 10_000);

            // Act
            var result = Result<string>.Success(largeString);

            // Assert
            result.IsSuccess.ShouldBeTrue();
            result.Value.Length.ShouldBe(10_000);
        }

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
        public void Result_MultipleConversions_ShouldMaintainState()
        {
            // Arrange
            const string originalValue = "test";
            Result<string> originalResult = originalValue;

            // Act
            Result nonGenericResult = originalResult;
            var backToGeneric = nonGenericResult.IsSuccess ? 
                Result<string>.Success(originalValue) : 
                Result<string>.Failure(nonGenericResult.Error);

            // Assert
            backToGeneric.IsSuccess.ShouldBeTrue();
            backToGeneric.Value.ShouldBe(originalValue);
        }

        [Test]
        public void Result_ThreadSafety_ShouldBehaveCorrectly()
        {
            // Arrange
            const int iterations = 1000;
            var results = new List<Result<int>>();
            var lockObject = new object();

            // Act
            Parallel.For(0, iterations, i =>
            {
                var result = Result<int>.Success(i);
                lock (lockObject)
                {
                    results.Add(result);
                }
            });

            // Assert
            results.Count.ShouldBe(iterations);
            results.All(r => r.IsSuccess).ShouldBeTrue();
            results.Select(r => r.Value).OrderBy(x => x).SequenceEqual(Enumerable.Range(0, iterations)).ShouldBeTrue();
        }

        [Test]
        public void Result_MemoryUsage_ShouldBeEfficient()
        {
            // Arrange
            const int iterations = 10_000;
            var results = new List<Result<int>>();

            // Act
            var initialMemory = GC.GetTotalMemory(true);
            
            for (int i = 0; i < iterations; i++)
            {
                results.Add(Result<int>.Success(i));
            }
            
            var finalMemory = GC.GetTotalMemory(false);
            var memoryUsed = finalMemory - initialMemory;

            // Assert - Should use reasonable amount of memory (struct should be efficient)
            results.Count.ShouldBe(iterations);
            memoryUsed.ShouldBeLessThan(iterations * 100); // Rough estimate - structs should be very memory efficient
        }
    }
}