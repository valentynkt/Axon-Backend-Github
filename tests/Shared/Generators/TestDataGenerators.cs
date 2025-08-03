using System.Net.Sockets;
using Axon.Shared.Common;
using Axon.Tests.Shared.Builders;

namespace Axon.Tests.Shared.Generators;

/// <summary>
/// Comprehensive test data generators for shared utilities with edge case coverage
/// </summary>
public static class TestDataGenerators
{
    private static readonly Random Random = new(42); // Fixed seed for reproducible tests

    /// <summary>
    /// Generates test data for Result scenarios
    /// </summary>
    public static class Results
    {
        /// <summary>
        /// Generates a collection of successful Result instances
        /// </summary>
        public static IEnumerable<Result<T>> SuccessfulResults<T>(IEnumerable<T> values)
        {
            return values.Select(Result<T>.Success);
        }

        /// <summary>
        /// Generates a collection of failed Result instances with various error types
        /// </summary>
        public static IEnumerable<Result<T>> FailedResults<T>(int count = 10)
        {
            var errors = Errors.AllErrorTypes(count);
            return errors.Select(Result<T>.Failure);
        }

        /// <summary>
        /// Generates a mixed collection of successful and failed Result instances
        /// </summary>
        public static IEnumerable<Result<T>> MixedResults<T>(IEnumerable<T> successValues, int failureCount = 5)
        {
            var successful = SuccessfulResults(successValues);
            var failed = FailedResults<T>(failureCount);
            return successful.Concat(failed).OrderBy(_ => Random.Next());
        }

        /// <summary>
        /// Generates Result instances with various string scenarios
        /// </summary>
        public static IEnumerable<Result<string>> StringResults()
        {
            var strings = Primitives.Strings().ToList();
            return MixedResults(strings, 3);
        }

        /// <summary>
        /// Generates Resul instances with various numeric scenarios
        /// </summary>
        public static IEnumerable<Result<int>> IntResults()
        {
            var ints = Primitives.Integers().ToList();
            return MixedResults(ints, 3);
        }

        /// <summary>
        /// Generates Result instances
        /// </summary>
        public static IEnumerable<Result<DateTime>> DateTimeResults()
        {
            var dateTimes = Primitives.DateTimes().ToList();
            return MixedResults(dateTimes, 2);
        }

        /// <summary>
        /// Generates Result instances
        /// </summary>
        public static IEnumerable<Result<Guid>> GuidResults()
        {
            var guids = Primitives.Guids().ToList();
            return MixedResults(guids, 2);
        }

        /// <summary>
        /// Generates nested Result scenarios for complex testing
        /// </summary>
        public static IEnumerable<Result<Result<T>>> NestedResults<T>(IEnumerable<T> values)
        {
            var innerResults = MixedResults(values, 2).ToList();
            var outerSuccessful = innerResults.Select(Result<Result<T>>.Success);
            var outerFailed = FailedResults<Result<T>>(2);
            return outerSuccessful.Concat(outerFailed);
        }

        /// <summary>
        /// Generates edge case Result instances for stress testing
        /// </summary>
        public static IEnumerable<Result<T>> EdgeCaseResults<T>(T defaultValue)
        {
            yield return Result<T>.Success(defaultValue);
            yield return Result<T>.Failure(Error.Validation("Edge case validation error"));
            yield return Result<T>.Failure(Error.InternalError("Edge case internal error", "EDGE_CASE", new InvalidOperationException("Edge case exception")));
        }
    }

    /// <summary>
    /// Generates test data for Error scenarios
    /// </summary>
    public static class Errors
    {
        /// <summary>
        /// Generates all ErrorType variants with sample data
        /// </summary>
        public static IEnumerable<Error> AllErrorTypes(int countPerType = 1)
        {
            var errorTypes = Enum.GetValues<ErrorType>();
            
            for (int i = 0; i < countPerType; i++)
            {
                foreach (var errorType in errorTypes)
                {
                    yield return errorType switch
                    {
                        ErrorType.Validation => Error.Validation($"Validation error {i + 1}", $"VAL_ERROR_{i + 1}"),
                        ErrorType.NotFound => Error.NotFound($"Resource {i + 1} not found", $"NOT_FOUND_{i + 1}"),
                        ErrorType.Conflict => Error.Conflict($"Conflict error {i + 1}", $"CONFLICT_{i + 1}"),
                        ErrorType.InternalError => Error.InternalError($"Internal error {i + 1}", $"INTERNAL_{i + 1}", GenerateException(i)),
                        ErrorType.ExternalService => Error.ExternalService($"External service error {i + 1}", $"EXT_SERVICE_{i + 1}", GenerateHttpException(i)),
                        ErrorType.Unauthorized => Error.Unauthorized($"Unauthorized access {i + 1}", $"UNAUTHORIZED_{i + 1}"),
                        ErrorType.Forbidden => Error.Forbidden($"Forbidden access {i + 1}", $"FORBIDDEN_{i + 1}"),
                        _ => throw new ArgumentOutOfRangeException($"Unknown error type: {errorType}")
                    };
                }
            }
        }

        /// <summary>
        /// Generates validation errors with common field validation scenarios
        /// </summary>
        public static IEnumerable<Error> ValidationErrors()
        {
            yield return Error.Validation("Email is required", "EMAIL_REQUIRED");
            yield return Error.Validation("Email format is invalid", "EMAIL_INVALID_FORMAT");
            yield return Error.Validation("Password must be at least 8 characters", "PASSWORD_TOO_SHORT");
            yield return Error.Validation("Age must be between 18 and 120", "AGE_OUT_OF_RANGE");
            yield return Error.Validation("Phone number format is invalid", "PHONE_INVALID_FORMAT");
            yield return Error.Validation("Date must be in the future", "DATE_INVALID_RANGE");
            yield return Error.Validation("File size cannot exceed 10MB", "FILE_TOO_LARGE");
            yield return Error.Validation("Username already exists", "USERNAME_TAKEN");
        }

        /// <summary>
        /// Generates business logic errors
        /// </summary>
        public static IEnumerable<Error> BusinessErrors()
        {
            yield return Error.Conflict("User already has an active subscription", "SUBSCRIPTION_ACTIVE");
            yield return Error.NotFound("Product with SKU 'ABC123' not found", "PRODUCT_NOT_FOUND");
            yield return Error.Forbidden("Insufficient balance for transaction", "INSUFFICIENT_BALANCE");
            yield return Error.Conflict("Order cannot be cancelled after shipping", "ORDER_SHIPPED");
            yield return Error.NotFound("Shopping cart is empty", "CART_EMPTY");
        }

        /// <summary>
        /// Generates infrastructure errors
        /// </summary>
        public static IEnumerable<Error> InfrastructureErrors()
        {
            yield return Error.InternalError("Database connection timeout", "DB_TIMEOUT", new TimeoutException("Connection timeout"));
            yield return Error.ExternalService("Payment gateway unavailable", "PAYMENT_UNAVAILABLE", new HttpRequestException("Service unavailable"));
            yield return Error.InternalError("Memory allocation failed", "MEMORY_ERROR", new OutOfMemoryException("Insufficient memory"));
            yield return Error.ExternalService("Email service rate limit exceeded", "EMAIL_RATE_LIMIT", new InvalidOperationException("Rate limit"));
        }

        /// <summary>
        /// Generates errors with nested exceptions
        /// </summary>
        public static IEnumerable<Error> ErrorsWithNestedExceptions()
        {
            var innerException = new ArgumentException("Inner argument exception");
            var middleException = new InvalidOperationException("Middle operation exception", innerException);
            var outerException = new ApplicationException("Outer application exception", middleException);

            yield return Error.InternalError("Nested exception error", "NESTED_ERROR", outerException);
            
            var httpInner = new SocketException(10054); // Connection reset
            var httpOuter = new HttpRequestException("HTTP request failed", httpInner);
            yield return Error.ExternalService("HTTP nested error", "HTTP_NESTED", httpOuter);
        }

        /// <summary>
        /// Generates errors with edge case messages
        /// </summary>
        public static IEnumerable<Error> EdgeCaseErrors()
        {
            yield return Error.Validation("", "EMPTY_MESSAGE"); // Empty message
            yield return Error.Validation(new string('A', 10000), "VERY_LONG_MESSAGE"); // Very long message
            yield return Error.Validation("Message with unicode: 🚨 测试 العربية", "UNICODE_MESSAGE"); // Unicode
            yield return Error.Validation("Message with special chars: !@#$%^&*()", "SPECIAL_CHARS"); // Special characters
            yield return Error.Validation("Multi\nline\nmessage", "MULTILINE_MESSAGE"); // Multi-line message
            yield return Error.Validation("Message with\ttabs", "TAB_MESSAGE"); // Tabs
        }

        private static int? GenerateException(int index)
        {
            return index % 3 switch
            {
                0 => throw new InvalidOperationException($"Test exception {index}"),
                1 => throw new ArgumentException($"Test argument exception {index}"),
                2 => null,
                _ => null
            };
        }

        private static int? GenerateHttpException(int index)
        {
            return index % 2 switch
            {
                0 => throw new HttpRequestException($"HTTP error {index}"),
                1 => throw new TimeoutException($"HTTP timeout {index}"),
                _ => null
            };
        }
    }

    /// <summary>
    /// Generates primitive type test data with edge cases
    /// </summary>
    public static class Primitives
    {
        /// <summary>
        /// Generates string test data with various edge cases
        /// </summary>
        public static IEnumerable<string> Strings()
        {
            yield return string.Empty;
            yield return " ";
            yield return "a";
            yield return "normal string";
            yield return "String with UPPER and lower";
            yield return "123456789";
            yield return "string with spaces   ";
            yield return "   string with leading spaces";
            yield return "\t\n\r";
            yield return "unicode: 🚨 测试 العربية Тест";
            yield return "special chars: !@#$%^&*()_+-=[]{}|;':\",./<>?`~";
            yield return new string('A', 1000); // Long string
            yield return "multi\nline\nstring";
            yield return "string\twith\ttabs";
            yield return "Path\\With\\Backslashes";
            yield return "Path/With/Slashes";
            yield return "email@domain.com";
            yield return "https://example.com/path?query=value";
        }

        /// <summary>
        /// Generates nullable string test data
        /// </summary>
        public static IEnumerable<string?> NullableStrings()
        {
            yield return null;
            foreach (var str in Strings())
            {
                yield return str;
            }
        }

        /// <summary>
        /// Generates integer test data with edge cases
        /// </summary>
        public static IEnumerable<int> Integers()
        {
            yield return int.MinValue;
            yield return int.MaxValue;
            yield return 0;
            yield return 1;
            yield return -1;
            yield return 42;
            yield return -42;
            yield return 2147483647;
            yield return -2147483648;
            
            // Random integers
            for (int i = 0; i < 5; i++)
            {
                yield return Random.Next();
            }
        }

        /// <summary>
        /// Generates long test data
        /// </summary>
        public static IEnumerable<long> Longs()
        {
            yield return long.MinValue;
            yield return long.MaxValue;
            yield return 0L;
            yield return 1L;
            yield return -1L;
            yield return int.MaxValue + 1L;
            yield return int.MinValue - 1L;
        }

        /// <summary>
        /// Generates decimal test data
        /// </summary>
        public static IEnumerable<decimal> Decimals()
        {
            yield return decimal.MinValue;
            yield return decimal.MaxValue;
            yield return decimal.Zero;
            yield return decimal.One;
            yield return decimal.MinusOne;
            yield return 3.14159m;
            yield return -3.14159m;
            yield return 0.000001m;
            yield return 999999.999999m;
        }

        /// <summary>
        /// Generates DateTime test data
        /// </summary>
        public static IEnumerable<DateTime> DateTimes()
        {
            yield return DateTime.MinValue;
            yield return DateTime.MaxValue;
            yield return DateTime.UnixEpoch;
            yield return new DateTime(2000, 1, 1);
            yield return new DateTime(2023, 12, 31, 23, 59, 59);
            yield return DateTime.UtcNow;
            yield return DateTime.Now;
            yield return DateTime.Today;
            
            // Random dates
            var startDate = new DateTime(1990, 1, 1);
            var endDate = new DateTime(2030, 12, 31);
            var range = endDate - startDate;
            
            for (int i = 0; i < 5; i++)
            {
                yield return startDate.AddDays(Random.Next((int)range.TotalDays));
            }
        }

        /// <summary>
        /// Generates Guid test data
        /// </summary>
        public static IEnumerable<Guid> Guids()
        {
            yield return Guid.Empty;
            yield return new Guid("00000000-0000-0000-0000-000000000001");
            yield return new Guid("FFFFFFFF-FFFF-FFFF-FFFF-FFFFFFFFFFFF");
            
            for (int i = 0; i < 10; i++)
            {
                yield return Guid.NewGuid();
            }
        }

        /// <summary>
        /// Generates boolean test data
        /// </summary>
        public static IEnumerable<bool> Booleans()
        {
            yield return true;
            yield return false;
        }
    }

    /// <summary>
    /// Generates complex scenarios for integration testing
    /// </summary>
    public static class Scenarios
    {
        /// <summary>
        /// Generates CRUD operation scenarios
        /// </summary>
        public static IEnumerable<(string Operation, Result<string> Expected)> CrudScenarios()
        {
            yield return ("Create", Result<string>.Success("User created successfully"));
            yield return ("Read", Result<string>.Success("User data retrieved"));
            yield return ("Update", Result<string>.Success("User updated successfully"));
            yield return ("Delete", Result<string>.Success("User deleted successfully"));
            yield return ("CreateDuplicate", Result<string>.Failure(Error.Conflict("User already exists")));
            yield return ("ReadNotFound", Result<string>.Failure(Error.NotFound("User not found")));
            yield return ("UpdateNotFound", Result<string>.Failure(Error.NotFound("User not found")));
            yield return ("DeleteNotFound", Result<string>.Failure(Error.NotFound("User not found")));
        }

        /// <summary>
        /// Generates validation scenarios
        /// </summary>
        public static IEnumerable<(string Input, Result<string> Expected)> ValidationScenarios()
        {
            yield return ("valid@email.com", Result<string>.Success("valid@email.com"));
            yield return ("invalid-email", Result<string>.Failure(Error.Validation("Invalid email format")));
            yield return ("", Result<string>.Failure(Error.Validation("Email is required")));
            yield return ("user@domain", Result<string>.Failure(Error.Validation("Invalid email format")));
            yield return ("user@.com", Result<string>.Failure(Error.Validation("Invalid email format")));
            yield return ("a".PadRight(500, 'a') + "@domain.com", Result<string>.Failure(Error.Validation("Email too long")));
        }

        /// <summary>
        /// Generates authentication scenarios
        /// </summary>
        public static IEnumerable<(string Username, string Password, Result<string> Expected)> AuthenticationScenarios()
        {
            yield return ("admin", "password123", Result<string>.Success("Authentication successful"));
            yield return ("user", "userpass", Result<string>.Success("Authentication successful"));
            yield return ("admin", "wrongpass", Result<string>.Failure(Error.Unauthorized("Invalid credentials")));
            yield return ("nonexistent", "password", Result<string>.Failure(Error.Unauthorized("Invalid credentials")));
            yield return ("", "password", Result<string>.Failure(Error.Validation("Username is required")));
            yield return ("admin", "", Result<string>.Failure(Error.Validation("Password is required")));
            yield return ("locked_user", "password", Result<string>.Failure(Error.Forbidden("Account is locked")));
        }

        /// <summary>
        /// Generates performance test scenarios
        /// </summary>
        public static IEnumerable<(int ItemCount, TimeSpan ExpectedMaxDuration)> PerformanceScenarios()
        {
            yield return (100, TimeSpan.FromMilliseconds(10));
            yield return (1_000, TimeSpan.FromMilliseconds(50));
            yield return (10_000, TimeSpan.FromMilliseconds(200));
            yield return (100_000, TimeSpan.FromSeconds(1));
        }

        /// <summary>
        /// Generates concurrent operation scenarios
        /// </summary>
        public static IEnumerable<(int ThreadCount, int OperationsPerThread, Type ExpectedResultType)> ConcurrencyScenarios()
        {
            yield return (2, 100, typeof(Result<int>));
            yield return (4, 250, typeof(Result<string>));
            yield return (8, 125, typeof(Result<Guid>));
            yield return (Environment.ProcessorCount, 100, typeof(Result<DateTime>));
        }
    }
}