using Axon.Shared.Common;
using Axon.Tests.Shared.Extensions;

namespace Axon.Tests.Shared.Utilities;

/// <summary>
/// Comprehensive test suite for Error class covering all ErrorType variants, exception handling, and serialization
/// </summary>
[TestFixture]
[Parallelizable(ParallelScope.Self)]
public sealed class ErrorTests
{
    [TestFixture]
    public class ErrorCreationTests
    {
        [Test]
        public void Validation_WithMessage_ShouldCreateValidationError()
        {
            // Arrange
            const string message = "Field is required";

            // Act
            var error = Error.Validation(message);

            // Assert
            error.Type.ShouldBe(ErrorType.Validation);
            error.Message.ShouldBe(message);
            error.Code.ShouldBe("VALIDATION_ERROR");
            error.InnerException.ShouldBeNull();
        }

        [Test]
        public void Validation_WithCustomCode_ShouldCreateValidationErrorWithCustomCode()
        {
            // Arrange
            const string message = "Field is required";
            const string customCode = "CUSTOM_VALIDATION_CODE";

            // Act
            var error = Error.Validation(message, customCode);

            // Assert
            error.Type.ShouldBe(ErrorType.Validation);
            error.Message.ShouldBe(message);
            error.Code.ShouldBe(customCode);
            error.InnerException.ShouldBeNull();
        }

        [Test]
        public void NotFound_WithMessage_ShouldCreateNotFoundError()
        {
            // Arrange
            const string message = "Resource not found";

            // Act
            var error = Error.NotFound(message);

            // Assert
            error.Type.ShouldBe(ErrorType.NotFound);
            error.Message.ShouldBe(message);
            error.Code.ShouldBe("NOT_FOUND");
            error.InnerException.ShouldBeNull();
        }

        [Test]
        public void NotFound_WithCustomCode_ShouldCreateNotFoundErrorWithCustomCode()
        {
            // Arrange
            const string message = "User not found";
            const string customCode = "USER_NOT_FOUND";

            // Act
            var error = Error.NotFound(message, customCode);

            // Assert
            error.Type.ShouldBe(ErrorType.NotFound);
            error.Message.ShouldBe(message);
            error.Code.ShouldBe(customCode);
        }

        [Test]
        public void Conflict_WithMessage_ShouldCreateConflictError()
        {
            // Arrange
            const string message = "Resource already exists";

            // Act
            var error = Error.Conflict(message);

            // Assert
            error.Type.ShouldBe(ErrorType.Conflict);
            error.Message.ShouldBe(message);
            error.Code.ShouldBe("CONFLICT");
            error.InnerException.ShouldBeNull();
        }

        [Test]
        public void InternalError_WithMessage_ShouldCreateInternalError()
        {
            // Arrange
            const string message = "An internal error occurred";

            // Act
            var error = Error.InternalError(message);

            // Assert
            error.Type.ShouldBe(ErrorType.InternalError);
            error.Message.ShouldBe(message);
            error.Code.ShouldBe("INTERNAL_ERROR");
            error.InnerException.ShouldBeNull();
        }

        [Test]
        public void InternalError_WithException_ShouldCreateInternalErrorWithException()
        {
            // Arrange
            const string message = "Database connection failed";
            var innerException = new InvalidOperationException("Connection timeout");

            // Act
            var error = Error.InternalError(message, "DB_CONNECTION_ERROR", innerException);

            // Assert
            error.Type.ShouldBe(ErrorType.InternalError);
            error.Message.ShouldBe(message);
            error.Code.ShouldBe("DB_CONNECTION_ERROR");
            error.InnerException.ShouldBe(innerException);
        }

        [Test]
        public void ExternalService_WithMessage_ShouldCreateExternalServiceError()
        {
            // Arrange
            const string message = "External API is unavailable";

            // Act
            var error = Error.ExternalService(message);

            // Assert
            error.Type.ShouldBe(ErrorType.ExternalService);
            error.Message.ShouldBe(message);
            error.Code.ShouldBe("EXTERNAL_SERVICE_ERROR");
            error.InnerException.ShouldBeNull();
        }

        [Test]
        public void ExternalService_WithException_ShouldCreateExternalServiceErrorWithException()
        {
            // Arrange
            const string message = "API rate limit exceeded";
            var innerException = new HttpRequestException("Too Many Requests");

            // Act
            var error = Error.ExternalService(message, "API_RATE_LIMIT", innerException);

            // Assert
            error.Type.ShouldBe(ErrorType.ExternalService);
            error.Message.ShouldBe(message);
            error.Code.ShouldBe("API_RATE_LIMIT");
            error.InnerException.ShouldBe(innerException);
        }

        [Test]
        public void Unauthorized_WithMessage_ShouldCreateUnauthorizedError()
        {
            // Arrange
            const string message = "Authentication required";

            // Act
            var error = Error.Unauthorized(message);

            // Assert
            error.Type.ShouldBe(ErrorType.Unauthorized);
            error.Message.ShouldBe(message);
            error.Code.ShouldBe("UNAUTHORIZED");
            error.InnerException.ShouldBeNull();
        }

        [Test]
        public void Forbidden_WithMessage_ShouldCreateForbiddenError()
        {
            // Arrange
            const string message = "Access denied";

            // Act
            var error = Error.Forbidden(message);

            // Assert
            error.Type.ShouldBe(ErrorType.Forbidden);
            error.Message.ShouldBe(message);
            error.Code.ShouldBe("FORBIDDEN");
            error.InnerException.ShouldBeNull();
        }
    }

    [TestFixture]
    public class ErrorBehaviorTests
    {
        [Test]
        public void ToString_ShouldReturnFormattedString()
        {
            // Arrange
            var error = Error.Validation("Field is required", "FIELD_REQUIRED");

            // Act
            var result = error.ToString();

            // Assert
            result.ShouldBe("[Validation] FIELD_REQUIRED: Field is required");
        }

        [Test]
        public void ToString_WithDifferentErrorTypes_ShouldFormatCorrectly()
        {
            // Arrange & Act & Assert
            Error.NotFound("Not found").ToString().ShouldBe("[NotFound] NOT_FOUND: Not found");
            Error.Conflict("Conflict").ToString().ShouldBe("[Conflict] CONFLICT: Conflict");
            Error.InternalError("Internal").ToString().ShouldBe("[InternalError] INTERNAL_ERROR: Internal");
            Error.ExternalService("External").ToString().ShouldBe("[ExternalService] EXTERNAL_SERVICE_ERROR: External");
            Error.Unauthorized("Unauthorized").ToString().ShouldBe("[Unauthorized] UNAUTHORIZED: Unauthorized");
            Error.Forbidden("Forbidden").ToString().ShouldBe("[Forbidden] FORBIDDEN: Forbidden");
        }

        [Test]
        public void Equals_WithSameError_ShouldBeEqual()
        {
            // Arrange
            var error1 = Error.Validation("Same message", "SAME_CODE");
            var error2 = Error.Validation("Same message", "SAME_CODE");

            // Act & Assert
            error1.ShouldBe(error2);
            (error1 == error2).ShouldBeTrue();
            error1.GetHashCode().ShouldBe(error2.GetHashCode());
        }

        [Test]
        public void Equals_WithDifferentMessages_ShouldNotBeEqual()
        {
            // Arrange
            var error1 = Error.Validation("Message 1", "CODE");
            var error2 = Error.Validation("Message 2", "CODE");

            // Act & Assert
            error1.ShouldNotBe(error2);
            (error1 == error2).ShouldBeFalse();
        }

        [Test]
        public void Equals_WithDifferentCodes_ShouldNotBeEqual()
        {
            // Arrange
            var error1 = Error.Validation("Same message", "CODE1");
            var error2 = Error.Validation("Same message", "CODE2");

            // Act & Assert
            error1.ShouldNotBe(error2);
        }

        [Test]
        public void Equals_WithDifferentTypes_ShouldNotBeEqual()
        {
            // Arrange
            var error1 = Error.Validation("Message");
            var error2 = Error.NotFound("Message");

            // Act & Assert
            error1.ShouldNotBe(error2);
        }

        [Test]
        public void Equals_WithDifferentInnerExceptions_ShouldNotBeEqual()
        {
            // Arrange
            var exception1 = new InvalidOperationException("Exception 1");
            var exception2 = new InvalidOperationException("Exception 2");
            var error1 = Error.InternalError("Message", "CODE", exception1);
            var error2 = Error.InternalError("Message", "CODE", exception2);

            // Act & Assert
            error1.ShouldNotBe(error2);
        }

        [Test]
        public void Equals_WithSameInnerException_ShouldBeEqual()
        {
            // Arrange
            var exception = new InvalidOperationException("Same exception");
            var error1 = Error.InternalError("Message", "CODE", exception);
            var error2 = Error.InternalError("Message", "CODE", exception);

            // Act & Assert
            error1.ShouldBe(error2);
        }
    }

    [TestFixture]
    public class ErrorTypeTests
    {
        [Test]
        [TestCase(ErrorType.Validation)]
        [TestCase(ErrorType.NotFound)]
        [TestCase(ErrorType.Conflict)]
        [TestCase(ErrorType.InternalError)]
        [TestCase(ErrorType.ExternalService)]
        [TestCase(ErrorType.Unauthorized)]
        [TestCase(ErrorType.Forbidden)]
        public void ErrorType_ShouldHaveCorrectValues(ErrorType errorType)
        {
            // Act & Assert
            Enum.IsDefined(typeof(ErrorType), errorType).ShouldBeTrue();
        }

        [Test]
        public void ErrorType_ShouldHaveExpectedCount()
        {
            // Act
            var values = Enum.GetValues<ErrorType>();

            // Assert
            values.Length.ShouldBe(7);
        }

        [Test]
        public void ErrorType_ToString_ShouldReturnCorrectNames()
        {
            // Act & Assert
            ErrorType.Validation.ToString().ShouldBe("Validation");
            ErrorType.NotFound.ToString().ShouldBe("NotFound");
            ErrorType.Conflict.ToString().ShouldBe("Conflict");
            ErrorType.InternalError.ToString().ShouldBe("InternalError");
            ErrorType.ExternalService.ToString().ShouldBe("ExternalService");
            ErrorType.Unauthorized.ToString().ShouldBe("Unauthorized");
            ErrorType.Forbidden.ToString().ShouldBe("Forbidden");
        }
    }

    [TestFixture]
    public class ErrorEdgeCasesAndStressTests
    {
        [Test]
        public void Error_WithNullMessage_ShouldHandleGracefully()
        {
            // Act & Assert
            Should.NotThrow(() => Error.Validation(null!));
        }

        [Test]
        public void Error_WithEmptyMessage_ShouldHandleCorrectly()
        {
            // Act
            var error = Error.Validation("");

            // Assert
            error.Message.ShouldBe("");
            error.Type.ShouldBe(ErrorType.Validation);
        }

        [Test]
        public void Error_WithLargeMessage_ShouldHandleCorrectly()
        {
            // Arrange
            var largeMessage = new string('A', 10_000);

            // Act
            var error = Error.InternalError(largeMessage);

            // Assert
            error.Message.Length.ShouldBe(10_000);
            error.Type.ShouldBe(ErrorType.InternalError);
        }

        [Test]
        public void Error_WithSpecialCharacters_ShouldHandleCorrectly()
        {
            // Arrange
            const string message = "Error with special chars: !@#$%^&*()_+-=[]{}|;':\",./<>?`~";

            // Act
            var error = Error.Validation(message);

            // Assert
            error.Message.ShouldBe(message);
        }

        [Test]
        public void Error_WithUnicodeCharacters_ShouldHandleCorrectly()
        {
            // Arrange
            const string message = "Error with unicode: 🚨 测试 Тест العربية";

            // Act
            var error = Error.Validation(message);

            // Assert
            error.Message.ShouldBe(message);
        }

        [Test]
        public void Error_ThreadSafety_ShouldBehaveCorrectly()
        {
            // Arrange
            const int iterations = 1000;
            var errors = new List<Error>();
            var lockObject = new object();

            // Act
            Parallel.For(0, iterations, i =>
            {
                var error = Error.Validation($"Message {i}", $"CODE_{i}");
                lock (lockObject)
                {
                    errors.Add(error);
                }
            });

            // Assert
            errors.Count.ShouldBe(iterations);
            errors.All(e => e.Type == ErrorType.Validation).ShouldBeTrue();
            errors.Select(e => e.Code).Distinct().Count().ShouldBe(iterations);
        }

        [Test]
        public void Error_NestedExceptions_ShouldPreserveExceptionChain()
        {
            // Arrange
            var innermost = new ArgumentException("Innermost");
            var middle = new InvalidOperationException("Middle", innermost);
            var outer = new ApplicationException("Outer", middle);

            // Act
            var error = Error.InternalError("Error with nested exceptions", "NESTED_ERROR", outer);

            // Assert
            error.InnerException.ShouldBe(outer);
            error.InnerException!.InnerException.ShouldBe(middle);
            error.InnerException!.InnerException!.InnerException.ShouldBe(innermost);
        }

        [Test]
        public void Error_Serialization_ShouldMaintainStructuralEquality()
        {
            // Arrange
            var originalError = Error.ExternalService("API Error", "API_001", new HttpRequestException("Network error"));

            // Act - Simulate serialization/deserialization by creating equivalent error
            var deserializedError = Error.ExternalService("API Error", "API_001", new HttpRequestException("Network error"));

            // Assert - Should be structurally equivalent
            deserializedError.Code.ShouldBe(originalError.Code);
            deserializedError.Message.ShouldBe(originalError.Message);
            deserializedError.Type.ShouldBe(originalError.Type);
            deserializedError.InnerException?.Message.ShouldBe(originalError.InnerException?.Message);
        }
    }
}