using Axon.Shared.Common;
using Axon.Tests.Shared.Extensions;
using Axon.Tests.Shared.Builders;

namespace Axon.Tests.Shared.Tests.Extensions;

/// <summary>
/// Comprehensive validation tests for extension methods
/// </summary>
[TestFixture]
[Parallelizable(ParallelScope.Self)]
public sealed class ExtensionMethodsValidationTests
{
    [TestFixture]
    public class CollectionExtensionTests
    {
        [Test]
        public void IsNullOrEmpty_WithNullCollection_ShouldReturnTrue()
        {
            // Arrange
            List<string>? collection = null;

            // Act
            var result = collection.IsNullOrEmpty();

            // Assert
            result.ShouldBeTrue();
        }

        [Test]
        public void IsNullOrEmpty_WithEmptyCollection_ShouldReturnTrue()
        {
            // Arrange
            var collection = new List<string>();

            // Act
            var result = collection.IsNullOrEmpty();

            // Assert
            result.ShouldBeTrue();
        }

        [Test]
        public void IsNullOrEmpty_WithNonEmptyCollection_ShouldReturnFalse()
        {
            // Arrange
            var collection = new List<string> { "item" };

            // Act
            var result = collection.IsNullOrEmpty();

            // Assert
            result.ShouldBeFalse();
        }

        [Test]
        public void HasAny_WithNullCollection_ShouldReturnFalse()
        {
            // Arrange
            List<string>? collection = null;

            // Act
            var result = collection.HasAny();

            // Assert
            result.ShouldBeFalse();
        }

        [Test]
        public void HasAny_WithEmptyCollection_ShouldReturnFalse()
        {
            // Arrange
            var collection = new List<string>();

            // Act
            var result = collection.HasAny();

            // Assert
            result.ShouldBeFalse();
        }

        [Test]
        public void HasAny_WithNonEmptyCollection_ShouldReturnTrue()
        {
            // Arrange
            var collection = new List<string> { "item1", "item2" };

            // Act
            var result = collection.HasAny();

            // Assert
            result.ShouldBeTrue();
        }

        [Test]
        public void HasAnyWithPredicate_WithMatchingItems_ShouldReturnTrue()
        {
            // Arrange
            var collection = new List<int> { 1, 2, 3, 4, 5 };

            // Act
            var result = collection.HasAny(x => x > 3);

            // Assert
            result.ShouldBeTrue();
        }

        [Test]
        public void HasAnyWithPredicate_WithNoMatchingItems_ShouldReturnFalse()
        {
            // Arrange
            var collection = new List<int> { 1, 2, 3 };

            // Act
            var result = collection.HasAny(x => x > 5);

            // Assert
            result.ShouldBeFalse();
        }
    }

    [TestFixture]
    public class DateTimeExtensionTests
    {
        [Test]
        public void IsWeekend_WithSaturday_ShouldReturnTrue()
        {
            // Arrange
            var saturday = new DateTime(2024, 1, 6); // Saturday

            // Act
            var result = saturday.IsWeekend();

            // Assert
            result.ShouldBeTrue();
        }

        [Test]
        public void IsWeekend_WithSunday_ShouldReturnTrue()
        {
            // Arrange
            var sunday = new DateTime(2024, 1, 7); // Sunday

            // Act
            var result = sunday.IsWeekend();

            // Assert
            result.ShouldBeTrue();
        }

        [Test]
        public void IsWeekend_WithWeekday_ShouldReturnFalse()
        {
            // Arrange
            var monday = new DateTime(2024, 1, 8); // Monday

            // Act
            var result = monday.IsWeekend();

            // Assert
            result.ShouldBeFalse();
        }

        [Test]
        public void ToUtcDateOnly_ShouldReturnUtcDate()
        {
            // Arrange
            var dateTime = new DateTime(2024, 1, 15, 10, 30, 45, DateTimeKind.Local);

            // Act
            var result = dateTime.ToUtcDateOnly();

            // Assert
            result.ShouldBe(dateTime.ToUniversalTime().Date);
        }

        [Test]
        public void IsToday_WithTodaysDate_ShouldReturnTrue()
        {
            // Arrange
            var today = DateTime.Today;

            // Act
            var result = today.IsToday();

            // Assert
            result.ShouldBeTrue();
        }

        [Test]
        public void IsToday_WithYesterdaysDate_ShouldReturnFalse()
        {
            // Arrange
            var yesterday = DateTime.Today.AddDays(-1);

            // Act
            var result = yesterday.IsToday();

            // Assert
            result.ShouldBeFalse();
        }
    }

    [TestFixture]
    public class StringExtensionTests
    {
        [Test]
        public void IsNullOrWhiteSpace_WithNull_ShouldReturnTrue()
        {
            // Arrange
            string? value = null;

            // Act
            var result = value.IsNullOrWhiteSpace();

            // Assert
            result.ShouldBeTrue();
        }

        [Test]
        public void IsNullOrWhiteSpace_WithEmpty_ShouldReturnTrue()
        {
            // Arrange
            var value = string.Empty;

            // Act
            var result = value.IsNullOrWhiteSpace();

            // Assert
            result.ShouldBeTrue();
        }

        [Test]
        public void IsNullOrWhiteSpace_WithWhitespace_ShouldReturnTrue()
        {
            // Arrange
            var value = "   ";

            // Act
            var result = value.IsNullOrWhiteSpace();

            // Assert
            result.ShouldBeTrue();
        }

        [Test]
        public void IsNullOrWhiteSpace_WithValue_ShouldReturnFalse()
        {
            // Arrange
            var value = "test";

            // Act
            var result = value.IsNullOrWhiteSpace();

            // Assert
            result.ShouldBeFalse();
        }

        [Test]
        public void HasValue_WithNull_ShouldReturnFalse()
        {
            // Arrange
            string? value = null;

            // Act
            var result = value.HasValue();

            // Assert
            result.ShouldBeFalse();
        }

        [Test]
        public void HasValue_WithWhitespace_ShouldReturnFalse()
        {
            // Arrange
            var value = "   ";

            // Act
            var result = value.HasValue();

            // Assert
            result.ShouldBeFalse();
        }

        [Test]
        public void HasValue_WithValue_ShouldReturnTrue()
        {
            // Arrange
            var value = "test value";

            // Act
            var result = value.HasValue();

            // Assert
            result.ShouldBeTrue();
        }

        [Test]
        public void TruncateWithEllipsis_WithShortString_ShouldReturnOriginal()
        {
            // Arrange
            var value = "short";
            const int maxLength = 10;

            // Act
            var result = value.TruncateWithEllipsis(maxLength);

            // Assert
            result.ShouldBe(value);
        }

        [Test]
        public void TruncateWithEllipsis_WithLongString_ShouldTruncateWithEllipsis()
        {
            // Arrange
            var value = "This is a very long string that needs truncation";
            const int maxLength = 10;

            // Act
            var result = value.TruncateWithEllipsis(maxLength);

            // Assert
            result.ShouldBe("This is a...");
            result.Length.ShouldBe(maxLength + 3); // +3 for ellipsis
        }
    }

    [TestFixture]
    public class ErrorExtensionTests
    {
        [Test]
        public void ToResult_WithError_ShouldCreateFailedResult()
        {
            // Arrange
            var error = Error.Validation("Test validation error");

            // Act
            var result = error.ToResult();

            // Assert
            result.ShouldBeFailure();
            result.Error.ShouldBe(error);
        }

        [Test]
        public void ToResultGeneric_WithError_ShouldCreateFailedGenericResult()
        {
            // Arrange
            var error = Error.NotFound("Resource not found");

            // Act
            var result = error.ToResult<string>();

            // Assert
            result.ShouldBeFailure();
            result.Error.ShouldBe(error);
        }

        [Test]
        public void WithContext_WithError_ShouldAddContext()
        {
            // Arrange
            var error = Error.InternalError("Original error");
            const string context = "Additional context information";

            // Act
            var result = error.WithContext(context);

            // Assert
            result.Type.ShouldBe(error.Type);
            result.Message.ShouldContain("Original error");
            result.Message.ShouldContain(context);
        }

        [Test]
        public void WithCode_WithError_ShouldAddCode()
        {
            // Arrange
            var error = Error.Validation("Validation failed");
            const string code = "VAL_001";

            // Act
            var result = error.WithCode(code);

            // Assert
            result.Type.ShouldBe(error.Type);
            result.Message.ShouldBe(error.Message);
            result.Code.ShouldBe(code);
        }
    }

    [TestFixture]
    public class ResultExtensionTests
    {
        [Test]
        public void MapError_WithFailedResult_ShouldTransformError()
        {
            // Arrange
            var originalError = Error.Validation("Original validation error");
            var result = Result<string>.Failure(originalError);

            // Act
            var mappedResult = result.MapError(error => Error.InternalError($"Mapped: {error.Message}"));

            // Assert
            mappedResult.ShouldBeFailure();
            mappedResult.Error.Type.ShouldBe(ErrorType.InternalError);
            mappedResult.Error.Message.ShouldContain("Mapped: Original validation error");
        }

        [Test]
        public void MapError_WithSuccessfulResult_ShouldReturnOriginalSuccess()
        {
            // Arrange
            var result = Result<string>.Success("test value");

            // Act
            var mappedResult = result.MapError(error => Error.InternalError("Should not be called"));

            // Assert
            mappedResult.ShouldBeSuccess();
            mappedResult.Value.ShouldBe("test value");
        }

        [Test]
        public void Ensure_WithPredicateTrue_ShouldReturnOriginalResult()
        {
            // Arrange
            var result = Result<string>.Success("valid value");

            // Act
            var ensuredResult = result.Ensure(value => value.Length > 0, Error.Validation("Value must not be empty"));

            // Assert
            ensuredResult.ShouldBeSuccess();
            ensuredResult.Value.ShouldBe("valid value");
        }

        [Test]
        public void Ensure_WithPredicateFalse_ShouldReturnFailure()
        {
            // Arrange
            var result = Result<string>.Success("");
            var validationError = Error.Validation("Value must not be empty");

            // Act
            var ensuredResult = result.Ensure(value => value.Length > 0, validationError);

            // Assert
            ensuredResult.ShouldBeFailure();
            ensuredResult.Error.ShouldBe(validationError);
        }

        [Test]
        public void Ensure_WithFailedResult_ShouldReturnOriginalFailure()
        {
            // Arrange
            var originalError = Error.NotFound("Not found");
            var result = Result<string>.Failure(originalError);

            // Act
            var ensuredResult = result.Ensure(value => true, Error.Validation("Should not matter"));

            // Assert
            ensuredResult.ShouldBeFailure();
            ensuredResult.Error.ShouldBe(originalError);
        }
    }

    [TestFixture]
    public class ExtensionMethodsPerformanceTests
    {
        [Test]
        [Category("Performance")]
        public void Extensions_PerformanceStressTest_ShouldBeEfficient()
        {
            // Arrange
            const int iterations = 100_000;
            var strings = Enumerable.Range(0, iterations).Select(i => $"test string {i}").ToList();
            var results = strings.Select(s => Result<string>.Success(s)).ToList();

            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            // Act
            var processedResults = results
                .Select(r => r.Map(s => s.ToUpperInvariant()))
                .Where(r => r.IsSuccess)
                .Select(r => r.Value)
                .Count(s => s.HasValue());

            stopwatch.Stop();

            // Assert
            processedResults.ShouldBe(iterations);
            stopwatch.ElapsedMilliseconds.ShouldBeLessThan(500); // Should complete in reasonable time
        }

        [Test]
        [Category("Performance")]
        public void Extensions_ConcurrentUsage_ShouldBeThreadSafe()
        {
            // Arrange
            const int iterations = 10_000;
            var results = Enumerable.Range(0, iterations)
                .Select(i => Result<int>.Success(i))
                .ToList();

            // Act & Assert
            Should.NotThrow(() =>
            {
                Parallel.ForEach(results, result =>
                {
                    result.ShouldBeSuccess();
                    var doubled = result.Map(x => x * 2);
                    doubled.ShouldBeSuccess();
                    doubled.Value.ShouldBeGreaterThanOrEqualTo(0);
                });
            });
        }
    }

    [TestFixture]
    public class TestExtensionMethods
    {
        [Test]
        public void CustomTestExtensions_ShouldWorkCorrectly()
        {
            // Arrange
            var successResult = Result<string>.Success("test");
            var failureResult = Result<string>.Failure(Error.Validation("error"));

            // Act & Assert
            successResult.ShouldBeSuccess();
            failureResult.ShouldBeFailure();
            
            successResult.ShouldBeSuccessWithValue().ShouldBe("test");
            Should.Throw<ShouldAssertException>(() => failureResult.ShouldBeSuccessWithValue());
        }

        [Test]
        public void ExtensionMethods_WithComplexScenarios_ShouldHandleCorrectly()
        {
            // Arrange
            var complexResult = Result<List<string>>.Success(new List<string> { "item1", "item2", "item3" });

            // Act & Assert
            complexResult.ShouldBeSuccessAnd(list =>
            {
                list.ShouldNotBeNull();
                list.Count.ShouldBe(3);
                list.HasAny().ShouldBeTrue();
                list.ShouldContain("item1");
            });
        }
    }
}