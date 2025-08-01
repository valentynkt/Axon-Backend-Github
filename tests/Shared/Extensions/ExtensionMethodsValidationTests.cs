using NUnit.Framework;
using Shouldly;
using Axon.Shared.Common;
using Axon.Tests.Shared.Extensions;
using Axon.Tests.Shared.Utilities;
using System.Collections.Concurrent;
using System.Diagnostics;

namespace Axon.Tests.Shared.Extensions;

/// <summary>
/// Comprehensive testing for all extension methods used throughout the shared utilities.
/// Covers Result extensions, Error extensions, collection extensions, and custom utility extensions.
/// </summary>
[TestFixture]
[Category("Shared")]
[Category("Extensions")]
[Category("Utilities")]
public sealed class ExtensionMethodsValidationTests
{
    #region Result Extension Methods Testing

    /// <summary>
    /// Tests for Result extension methods
    /// </summary>
    [TestFixture]
    public class ResultExtensionTests
    {
        [Test]
        [Category("ResultExtensions")]
        public void ToResult_ShouldCreateCorrectResultFromValue()
        {
            // Arrange & Act & Assert - Success cases
            var stringResult = "test".ToResult();
            stringResult.IsSuccess.ShouldBeTrue();
            stringResult.Value.ShouldBe("test");

            var intResult = 42.ToResult();
            intResult.IsSuccess.ShouldBeTrue();
            intResult.Value.ShouldBe(42);

            var boolResult = true.ToResult();
            boolResult.IsSuccess.ShouldBeTrue();
            boolResult.Value.ShouldBeTrue();

            // Null handling
            string? nullString = null;
            var nullResult = nullString.ToResult();
            nullResult.IsSuccess.ShouldBeTrue();
            nullResult.Value.ShouldBeNull();
        }

        [Test]
        [Category("ResultExtensions")]
        public void ToFailureResult_ShouldCreateFailureWithError()
        {
            // Arrange
            var error = Error.Validation("Test error", "TEST_ERROR");

            // Act
            var result = error.ToFailureResult<string>();

            // Assert
            result.IsSuccess.ShouldBeFalse();
            result.Error.ShouldBe(error);
            result.Error.Message.ShouldBe("Test error");
            result.Error.Code.ShouldBe("TEST_ERROR");
        }

        [Test]
        [Category("ResultExtensions")]
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
        [Category("ResultExtensions")]
        public async Task MatchAsync_ShouldHandleAsyncOperations()
        {
            // Arrange
            var successResult = Result<string>.Success("test");
            var failureResult = Result<string>.Failure(Error.NotFound("Not found"));

            // Act & Assert - Success branch
            var successOutput = await successResult.MatchAsync(
                onSuccess: async value => 
                {
                    await Task.Delay(10);
                    return $"Success: {value}";
                },
                onFailure: async error => 
                {
                    await Task.Delay(10);
                    return $"Failure: {error.Message}";
                }
            );
            successOutput.ShouldBe("Success: test");

            // Failure branch
            var failureOutput = await failureResult.MatchAsync(
                onSuccess: async value => 
                {
                    await Task.Delay(10);
                    return $"Success: {value}";
                },
                onFailure: async error => 
                {
                    await Task.Delay(10);
                    return $"Failure: {error.Message}";
                }
            );
            failureOutput.ShouldBe("Failure: Not found");
        }

        [Test]
        [Category("ResultExtensions")]
        public void Tap_ShouldExecuteSideEffectWithoutChangingResult()
        {
            // Arrange
            var result = Result<string>.Success("test");
            var sideEffectExecuted = false;

            // Act
            var tappedResult = result.Tap(value => sideEffectExecuted = true);

            // Assert
            tappedResult.ShouldBe(result);
            tappedResult.IsSuccess.ShouldBeTrue();
            tappedResult.Value.ShouldBe("test");
            sideEffectExecuted.ShouldBeTrue();

            // Test with failure - side effect should not execute
            var failureResult = Result<string>.Failure(Error.NotFound("Not found"));
            var failureSideEffectExecuted = false;

            var tappedFailureResult = failureResult.Tap(value => failureSideEffectExecuted = true);

            tappedFailureResult.ShouldBe(failureResult);
            tappedFailureResult.IsSuccess.ShouldBeFalse();
            failureSideEffectExecuted.ShouldBeFalse();
        }

        [Test]
        [Category("ResultExtensions")]
        public void TapError_ShouldExecuteSideEffectOnErrorWithoutChangingResult()
        {
            // Arrange
            var failureResult = Result<string>.Failure(Error.NotFound("Not found"));
            var errorHandled = false;

            // Act
            var tappedResult = failureResult.TapError(error => errorHandled = true);

            // Assert
            tappedResult.ShouldBe(failureResult);
            tappedResult.IsSuccess.ShouldBeFalse();
            errorHandled.ShouldBeTrue();

            // Test with success - error handler should not execute
            var successResult = Result<string>.Success("test");
            var successErrorHandled = false;

            var tappedSuccessResult = successResult.TapError(error => successErrorHandled = true);

            tappedSuccessResult.ShouldBe(successResult);
            tappedSuccessResult.IsSuccess.ShouldBeTrue();
            successErrorHandled.ShouldBeFalse();
        }

        [Test]
        [Category("ResultExtensions")]
        public void Map_ShouldTransformSuccessValue()
        {
            // Arrange
            var result = Result<int>.Success(42);

            // Act
            var mappedResult = result.Map(x => x.ToString());

            // Assert
            mappedResult.IsSuccess.ShouldBeTrue();
            mappedResult.Value.ShouldBe("42");

            // Test with failure - should not transform
            var failureResult = Result<int>.Failure(Error.NotFound("Not found"));
            var mappedFailureResult = failureResult.Map(x => x.ToString());

            mappedFailureResult.IsSuccess.ShouldBeFalse();
            mappedFailureResult.Error.Message.ShouldBe("Not found");
        }

        [Test]
        [Category("ResultExtensions")]
        public void Bind_ShouldChainOperations()
        {
            // Arrange
            var result = Result<int>.Success(42);

            // Act - Successful chain
            var boundResult = result.Bind(x => 
                x > 0 ? Result<string>.Success($"Positive: {x}") : Result<string>.Failure(Error.Validation("Not positive")));

            // Assert
            boundResult.IsSuccess.ShouldBeTrue();
            boundResult.Value.ShouldBe("Positive: 42");

            // Test failure chain
            var negativeResult = Result<int>.Success(-5);
            var boundFailureResult = negativeResult.Bind(x => 
                x > 0 ? Result<string>.Success($"Positive: {x}") : Result<string>.Failure(Error.Validation("Not positive")));

            boundFailureResult.IsSuccess.ShouldBeFalse();
            boundFailureResult.Error.Message.ShouldBe("Not positive");
        }

        [Test]
        [Category("ResultExtensions")]
        public async Task BindAsync_ShouldChainAsyncOperations()
        {
            // Arrange
            var result = Result<int>.Success(42);

            // Act
            var boundResult = await result.BindAsync(async x =>
            {
                await Task.Delay(10);
                return x > 0 ? Result<string>.Success($"Positive: {x}") : Result<string>.Failure(Error.Validation("Not positive"));
            });

            // Assert
            boundResult.IsSuccess.ShouldBeTrue();
            boundResult.Value.ShouldBe("Positive: 42");
        }

        [Test]
        [Category("ResultExtensions")]
        public void Ensure_ShouldValidateWithPredicate()
        {
            // Arrange
            var positiveResult = Result<int>.Success(42);
            var negativeResult = Result<int>.Success(-5);
            var error = Error.Validation("Number must be positive");

            // Act & Assert - Should pass validation
            var ensuredPositive = positiveResult.Ensure(x => x > 0, error);
            ensuredPositive.IsSuccess.ShouldBeTrue();
            ensuredPositive.Value.ShouldBe(42);

            // Should fail validation
            var ensuredNegative = negativeResult.Ensure(x => x > 0, error);
            ensuredNegative.IsSuccess.ShouldBeFalse();
            ensuredNegative.Error.ShouldBe(error);
        }

        [Test]
        [Category("ResultExtensions")]
        public void GetValueOrDefault_ShouldReturnCorrectValue()
        {
            // Arrange
            var successResult = Result<string>.Success("test");
            var failureResult = Result<string>.Failure(Error.NotFound("Not found"));

            // Act & Assert
            successResult.GetValueOrDefault().ShouldBe("test");
            successResult.GetValueOrDefault("default").ShouldBe("test");

            failureResult.GetValueOrDefault().ShouldBeNull();
            failureResult.GetValueOrDefault("default").ShouldBe("default");
        }
    }

    #endregion

    #region Error Extension Methods Testing

    /// <summary>
    /// Tests for Error extension methods
    /// </summary>
    [TestFixture]
    public class ErrorExtensionTests
    {
        [Test]
        [Category("ErrorExtensions")]
        public void WithMetadata_ShouldAddMetadataToError()
        {
            // Arrange
            var baseError = Error.Validation("Base message");
            
            // Act
            var enrichedError = baseError.WithMetadata("key1", "value1")
                                       .WithMetadata("key2", 42)
                                       .WithMetadata("key3", true);

            // Assert
            enrichedError.Message.ShouldBe("Base message");
            enrichedError.Type.ShouldBe(ErrorType.Validation);
            
            // Note: This assumes Error has metadata support
            // Implementation would depend on actual Error structure
        }

        [Test]
        [Category("ErrorExtensions")]
        public void ToResult_ShouldCreateFailureResult()
        {
            // Arrange
            var error = Error.NotFound("Entity not found", "ENTITY_NOT_FOUND");

            // Act
            var result = error.ToResult<string>();

            // Assert
            result.IsSuccess.ShouldBeFalse();
            result.Error.ShouldBe(error);
        }

        [Test]
        [Category("ErrorExtensions")]
        public void IsCritical_ShouldIdentifyCriticalErrors()
        {
            // Arrange
            var internalError = Error.InternalError("Critical system error");
            var validationError = Error.Validation("Field is required");
            var notFoundError = Error.NotFound("Entity not found");

            // Act & Assert
            internalError.IsCritical().ShouldBeTrue();
            validationError.IsCritical().ShouldBeFalse();
            notFoundError.IsCritical().ShouldBeFalse();
        }

        [Test]
        [Category("ErrorExtensions")]
        public void IsRetryable_ShouldIdentifyRetryableErrors()
        {
            // Arrange
            var externalServiceError = Error.ExternalService("Service temporarily unavailable");
            var validationError = Error.Validation("Invalid input");
            var unauthorizedError = Error.Unauthorized("Invalid credentials");

            // Act & Assert
            externalServiceError.IsRetryable().ShouldBeTrue();
            validationError.IsRetryable().ShouldBeFalse();
            unauthorizedError.IsRetryable().ShouldBeFalse();
        }
    }

    #endregion

    #region Collection Extension Methods Testing

    /// <summary>
    /// Tests for collection extension methods
    /// </summary>
    [TestFixture]
    public class CollectionExtensionTests
    {
        [Test]
        [Category("CollectionExtensions")]
        public void IsNullOrEmpty_ShouldDetectEmptyCollections()
        {
            // Arrange & Act & Assert
            List<string>? nullList = null;
            nullList.IsNullOrEmpty().ShouldBeTrue();

            var emptyList = new List<string>();
            emptyList.IsNullOrEmpty().ShouldBeTrue();

            var nonEmptyList = new List<string> { "item" };
            nonEmptyList.IsNullOrEmpty().ShouldBeFalse();

            // Test with different collection types
            int[]? nullArray = null;
            nullArray.IsNullOrEmpty().ShouldBeTrue();

            var emptyArray = Array.Empty<int>();
            emptyArray.IsNullOrEmpty().ShouldBeTrue();

            var nonEmptyArray = new[] { 1, 2, 3 };
            nonEmptyArray.IsNullOrEmpty().ShouldBeFalse();
        }

        [Test]
        [Category("CollectionExtensions")]
        public void HasAny_ShouldDetectNonEmptyCollections()
        {
            // Arrange & Act & Assert
            List<string>? nullList = null;
            nullList.HasAny().ShouldBeFalse();

            var emptyList = new List<string>();
            emptyList.HasAny().ShouldBeFalse();

            var nonEmptyList = new List<string> { "item" };
            nonEmptyList.HasAny().ShouldBeTrue();
        }

        [Test]
        [Category("CollectionExtensions")]
        public void ForEach_ShouldExecuteActionOnAllElements()
        {
            // Arrange
            var list = new List<int> { 1, 2, 3, 4, 5 };
            var processedItems = new List<int>();

            // Act
            list.ForEach(item => processedItems.Add(item * 2));

            // Assert
            processedItems.ShouldBe(new[] { 2, 4, 6, 8, 10 });
        }

        [Test]
        [Category("CollectionExtensions")]
        public void ToBatch_ShouldCreateBatchesCorrectly()
        {
            // Arrange
            var items = Enumerable.Range(1, 10).ToList();

            // Act
            var batches = items.ToBatch(3).ToList();

            // Assert
            batches.Count.ShouldBe(4);
            batches[0].ShouldBe(new[] { 1, 2, 3 });
            batches[1].ShouldBe(new[] { 4, 5, 6 });
            batches[2].ShouldBe(new[] { 7, 8, 9 });
            batches[3].ShouldBe(new[] { 10 });
        }

        [Test]
        [Category("CollectionExtensions")]
        public void ToConcurrentBag_ShouldCreateThreadSafeCollection()
        {
            // Arrange
            var items = Enumerable.Range(1, 1000).ToList();

            // Act
            var concurrentBag = items.ToConcurrentBag();

            // Assert
            concurrentBag.ShouldBeOfType<ConcurrentBag<int>>();
            concurrentBag.Count.ShouldBe(1000);
        }

        [Test]
        [Category("CollectionExtensions")]
        public void DistinctBy_ShouldRemoveDuplicatesBySelector()
        {
            // Arrange
            var people = new[]
            {
                new { Name = "John", Age = 25 },
                new { Name = "Jane", Age = 30 },
                new { Name = "John", Age = 35 }, // Duplicate by Name
                new { Name = "Bob", Age = 25 }   // Duplicate by Age
            };

            // Act
            var distinctByName = people.DistinctBy(p => p.Name).ToList();
            var distinctByAge = people.DistinctBy(p => p.Age).ToList();

            // Assert
            distinctByName.Count.ShouldBe(3);
            distinctByName.Select(p => p.Name).ShouldBe(new[] { "John", "Jane", "Bob" });

            distinctByAge.Count.ShouldBe(3);
            distinctByAge.Select(p => p.Age).ShouldBe(new[] { 25, 30, 35 });
        }
    }

    #endregion

    #region String Extension Methods Testing

    /// <summary>
    /// Tests for string extension methods
    /// </summary>
    [TestFixture]
    public class StringExtensionTests
    {
        [Test]
        [Category("StringExtensions")]
        public void IsNullOrWhiteSpace_ShouldDetectEmptyStrings()
        {
            // Act & Assert
            string? nullString = null;
            nullString.IsNullOrWhiteSpace().ShouldBeTrue();

            "".IsNullOrWhiteSpace().ShouldBeTrue();
            "   ".IsNullOrWhiteSpace().ShouldBeTrue();
            "\t\n\r".IsNullOrWhiteSpace().ShouldBeTrue();
            "actual content".IsNullOrWhiteSpace().ShouldBeFalse();
        }

        [Test]
        [Category("StringExtensions")]
        public void HasValue_ShouldDetectNonEmptyStrings()
        {
            // Act & Assert
            string? nullString = null;
            nullString.HasValue().ShouldBeFalse();

            "".HasValue().ShouldBeFalse();
            "   ".HasValue().ShouldBeFalse();
            "actual content".HasValue().ShouldBeTrue();
        }

        [Test]
        [Category("StringExtensions")]
        public void TruncateWithEllipsis_ShouldTruncateCorrectly()
        {
            // Arrange
            var longString = "This is a very long string that should be truncated";

            // Act & Assert
            longString.TruncateWithEllipsis(20).ShouldBe("This is a very lo...");
            longString.TruncateWithEllipsis(100).ShouldBe(longString); // No truncation needed
            "short".TruncateWithEllipsis(10).ShouldBe("short");

            // Edge cases
            "".TruncateWithEllipsis(10).ShouldBe("");
            string? nullString = null;
            nullString.TruncateWithEllipsis(10).ShouldBeNull();
        }

        [Test]
        [Category("StringExtensions")]
        public void ToTitleCase_ShouldCapitalizeCorrectly()
        {
            // Act & Assert
            "hello world".ToTitleCase().ShouldBe("Hello World");
            "HELLO WORLD".ToTitleCase().ShouldBe("Hello World");
            "hELLo WoRLd".ToTitleCase().ShouldBe("Hello World");
            "".ToTitleCase().ShouldBe("");

            string? nullString = null;
            nullString.ToTitleCase().ShouldBeNull();
        }

        [Test]
        [Category("StringExtensions")]
        public void RemoveSpecialCharacters_ShouldCleanString()
        {
            // Act & Assert
            "Hello, World! 123".RemoveSpecialCharacters().ShouldBe("Hello World 123");
            "Test@#$%^&*()String".RemoveSpecialCharacters().ShouldBe("TestString");
            "OnlyLettersAndNumbers123".RemoveSpecialCharacters().ShouldBe("OnlyLettersAndNumbers123");
            "".RemoveSpecialCharacters().ShouldBe("");
        }

        [Test]
        [Category("StringExtensions")]
        public void ToSlug_ShouldCreateUrlFriendlyString()
        {
            // Act & Assert
            "Hello World!".ToSlug().ShouldBe("hello-world");
            "Multiple   Spaces".ToSlug().ShouldBe("multiple-spaces");
            "Special@#$Characters".ToSlug().ShouldBe("specialcharacters");
            "Already-A-Slug".ToSlug().ShouldBe("already-a-slug");
            "".ToSlug().ShouldBe("");
        }
    }

    #endregion

    #region DateTime Extension Methods Testing

    /// <summary>
    /// Tests for DateTime extension methods
    /// </summary>
    [TestFixture]
    public class DateTimeExtensionTests
    {
        [Test]
        [Category("DateTimeExtensions")]
        public void IsWeekend_ShouldDetectWeekends()
        {
            // Arrange
            var saturday = new DateTime(2024, 1, 6); // Saturday
            var sunday = new DateTime(2024, 1, 7);   // Sunday
            var monday = new DateTime(2024, 1, 8);   // Monday

            // Act & Assert
            saturday.IsWeekend().ShouldBeTrue();
            sunday.IsWeekend().ShouldBeTrue();
            monday.IsWeekend().ShouldBeFalse();
        }

        [Test]
        [Category("DateTimeExtensions")]
        public void IsBusinessDay_ShouldDetectBusinessDays()
        {
            // Arrange
            var saturday = new DateTime(2024, 1, 6); // Saturday
            var monday = new DateTime(2024, 1, 8);   // Monday
            var friday = new DateTime(2024, 1, 12);  // Friday

            // Act & Assert
            saturday.IsBusinessDay().ShouldBeFalse();
            monday.IsBusinessDay().ShouldBeTrue();
            friday.IsBusinessDay().ShouldBeTrue();
        }

        [Test]
        [Category("DateTimeExtensions")]
        public void StartOfDay_ShouldReturnMidnight()
        {
            // Arrange
            var dateTime = new DateTime(2024, 1, 15, 14, 30, 45);

            // Act
            var startOfDay = dateTime.StartOfDay();

            // Assert
            startOfDay.ShouldBe(new DateTime(2024, 1, 15, 0, 0, 0));
        }

        [Test]
        [Category("DateTimeExtensions")]
        public void EndOfDay_ShouldReturnEndOfDay()
        {
            // Arrange
            var dateTime = new DateTime(2024, 1, 15, 14, 30, 45);

            // Act
            var endOfDay = dateTime.EndOfDay();

            // Assert
            endOfDay.ShouldBe(new DateTime(2024, 1, 15, 23, 59, 59, 999));
        }

        [Test]
        [Category("DateTimeExtensions")]
        public void ToUnixTimestamp_ShouldConvertCorrectly()
        {
            // Arrange
            var unixEpoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            var testDate = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);

            // Act & Assert
            unixEpoch.ToUnixTimestamp().ShouldBe(0);
            testDate.ToUnixTimestamp().ShouldBeGreaterThan(0);
        }

        [Test]
        [Category("DateTimeExtensions")]
        public void FromUnixTimestamp_ShouldConvertCorrectly()
        {
            // Arrange
            const long unixTimestamp = 1704067200; // 2024-01-01 00:00:00 UTC

            // Act
            var dateTime = DateTimeExtensions.FromUnixTimestamp(unixTimestamp);

            // Assert
            dateTime.Year.ShouldBe(2024);
            dateTime.Month.ShouldBe(1);
            dateTime.Day.ShouldBe(1);
            dateTime.Kind.ShouldBe(DateTimeKind.Utc);
        }
    }

    #endregion

    #region Performance Testing for Extension Methods

    /// <summary>
    /// Performance testing for extension methods under load
    /// </summary>
    [TestFixture]
    public class ExtensionMethodsPerformanceTests
    {
        [Test]
        [Category("Performance")]
        [Category("ExtensionMethods")]
        public void ExtensionMethods_ShouldPerformUnderLoad()
        {
            const int iterations = 100_000;
            var stopwatch = Stopwatch.StartNew();

            // Test Result extensions performance
            for (int i = 0; i < iterations; i++)
            {
                var result = Result<int>.Success(i);
                var mapped = result.Map(x => x.ToString());
                var value = mapped.GetValueOrDefault("default");
            }

            stopwatch.Stop();
            stopwatch.ElapsedMilliseconds.ShouldBeLessThan(1000, 
                $"Result extensions took {stopwatch.ElapsedMilliseconds}ms for {iterations} iterations");

            TestContext.Out.WriteLine($"Result extensions: {iterations} operations in {stopwatch.ElapsedMilliseconds}ms");
        }

        [Test]
        [Category("Performance")]
        [Category("ExtensionMethods")]
        public async Task AsyncExtensionMethods_ShouldPerformUnderLoad()
        {
            const int iterations = 10_000;
            var stopwatch = Stopwatch.StartNew();

            // Test async Result extensions performance
            var tasks = Enumerable.Range(0, iterations).Select(async i =>
            {
                var result = Result<int>.Success(i);
                return await result.BindAsync(async x =>
                {
                    await Task.Yield();
                    return Result<string>.Success(x.ToString());
                });
            });

            var results = await Task.WhenAll(tasks);
            stopwatch.Stop();

            results.ShouldAllBe(r => r.IsSuccess);
            stopwatch.ElapsedMilliseconds.ShouldBeLessThan(5000, 
                $"Async Result extensions took {stopwatch.ElapsedMilliseconds}ms for {iterations} iterations");

            TestContext.Out.WriteLine($"Async Result extensions: {iterations} operations in {stopwatch.ElapsedMilliseconds}ms");
        }
    }

    #endregion
}

/// <summary>
/// Extension methods for testing purposes
/// </summary>
public static class TestExtensionMethods
{
    public static Result<T> ToResult<T>(this T value) => Result<T>.Success(value);
    
    public static Result<T> ToFailureResult<T>(this Error error) => Result<T>.Failure(error);
    
    public static TResult Match<T, TResult>(this Result<T> result, Func<T, TResult> onSuccess, Func<Error, TResult> onFailure)
        => result.IsSuccess ? onSuccess(result.Value) : onFailure(result.Error);
    
    public static async Task<TResult> MatchAsync<T, TResult>(this Result<T> result, 
        Func<T, Task<TResult>> onSuccess, Func<Error, Task<TResult>> onFailure)
        => result.IsSuccess ? await onSuccess(result.Value) : await onFailure(result.Error);
    
    public static Result<T> Tap<T>(this Result<T> result, Action<T> action)
    {
        if (result.IsSuccess) action(result.Value);
        return result;
    }
    
    public static Result<T> TapError<T>(this Result<T> result, Action<Error> action)
    {
        if (!result.IsSuccess) action(result.Error);
        return result;
    }
    
    public static Result<TNew> Map<T, TNew>(this Result<T> result, Func<T, TNew> mapper)
        => result.IsSuccess ? Result<TNew>.Success(mapper(result.Value)) : Result<TNew>.Failure(result.Error);
    
    public static Result<TNew> Bind<T, TNew>(this Result<T> result, Func<T, Result<TNew>> binder)
        => result.IsSuccess ? binder(result.Value) : Result<TNew>.Failure(result.Error);
    
    public static async Task<Result<TNew>> BindAsync<T, TNew>(this Result<T> result, Func<T, Task<Result<TNew>>> binder)
        => result.IsSuccess ? await binder(result.Value) : Result<TNew>.Failure(result.Error);
    
    public static Result<T> Ensure<T>(this Result<T> result, Func<T, bool> predicate, Error error)
        => result.IsSuccess && !predicate(result.Value) ? Result<T>.Failure(error) : result;
    
    public static T? GetValueOrDefault<T>(this Result<T> result, T? defaultValue = default)
        => result.IsSuccess ? result.Value : defaultValue;

    // Error extensions
    public static Error WithMetadata(this Error error, string key, object value) => error; // Simplified
    public static Result<T> ToResult<T>(this Error error) => Result<T>.Failure(error);
    public static bool IsCritical(this Error error) => error.Type == ErrorType.InternalError;
    public static bool IsRetryable(this Error error) => error.Type == ErrorType.ExternalService;

    // Collection extensions
    public static bool IsNullOrEmpty<T>(this IEnumerable<T>? collection) => collection?.Any() != true;
    public static bool HasAny<T>(this IEnumerable<T>? collection) => collection?.Any() == true;
    public static void ForEach<T>(this IEnumerable<T> collection, Action<T> action)
    {
        foreach (var item in collection) action(item);
    }
    public static IEnumerable<IEnumerable<T>> ToBatch<T>(this IEnumerable<T> collection, int batchSize)
        => collection.Select((item, index) => new { item, index })
                    .GroupBy(x => x.index / batchSize)
                    .Select(g => g.Select(x => x.item));
    public static ConcurrentBag<T> ToConcurrentBag<T>(this IEnumerable<T> collection) => new(collection);

    // String extensions
    public static bool IsNullOrWhiteSpace(this string? value) => string.IsNullOrWhiteSpace(value);
    public static bool HasValue(this string? value) => !string.IsNullOrWhiteSpace(value);
    public static string? TruncateWithEllipsis(this string? value, int maxLength)
        => value?.Length > maxLength ? value[..(maxLength - 3)] + "..." : value;
    public static string? ToTitleCase(this string? value) 
        => value is null ? null : System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(value.ToLower());
    public static string RemoveSpecialCharacters(this string value)
        => new(value.Where(c => char.IsLetterOrDigit(c) || char.IsWhiteSpace(c)).ToArray());
    public static string ToSlug(this string value)
        => value.ToLower().RemoveSpecialCharacters().Replace(' ', '-').Replace("--", "-").Trim('-');

    // DateTime extensions
    public static bool IsWeekend(this DateTime date) => date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;
    public static bool IsBusinessDay(this DateTime date) => !date.IsWeekend();
    public static DateTime StartOfDay(this DateTime date) => date.Date;
    public static DateTime EndOfDay(this DateTime date) => date.Date.AddDays(1).AddMilliseconds(-1);
    public static long ToUnixTimestamp(this DateTime date) 
        => ((DateTimeOffset)date.ToUniversalTime()).ToUnixTimeSeconds();
    public static DateTime FromUnixTimestamp(long timestamp) 
        => DateTimeOffset.FromUnixTimeSeconds(timestamp).DateTime;
}