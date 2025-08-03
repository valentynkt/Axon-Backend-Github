using NUnit.Framework;
using Shouldly;
using Axon.Shared.Common;
using Axon.Tests.Shared.Fixtures;
using Axon.Tests.Shared.Builders;
using Axon.Tests.Shared.Tests.Extensions;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Text.Json;

namespace Axon.Tests.Shared.Tests.Validation;

/// <summary>
/// Comprehensive validation testing for primitive types, custom value objects, and primitive wrappers.
/// Tests validation logic, edge cases, serialization, and cross-cultural compatibility.
/// </summary>
[TestFixture]
[Category("Shared")]
[Category("Validation")]
[Category("PrimitiveTypes")]
public sealed class PrimitiveTypesValidationTests
{
    #region Value Object Testing

    /// <summary>
    /// Tests for common value object patterns like Email, Phone, etc.
    /// </summary>
    [TestFixture]
    public class ValueObjectValidationTests
    {
        [Test]
        [Category("EmailValidation")]
        public void EmailValueObject_ShouldValidateCorrectly()
        {
            // Arrange & Act & Assert - Valid emails
            var validEmails = new[]
            {
                "user@example.com",
                "test.email+tag@domain.co.uk",
                "user123@sub.domain.org",
                "simple@localhost",
                "x@example.com",
                "test@domain-with-dash.com"
            };

            foreach (var email in validEmails)
            {
                var result = ValidateEmail(email);
                result.IsSuccess.ShouldBeTrue($"Email '{email}' should be valid");
            }

            // Invalid emails
            var invalidEmails = new[]
            {
                "",
                "invalid-email",
                "@domain.com",
                "user@",
                "plainaddress",
                "user@.com",
                "user@domain.",
                "user..name@domain.com",
                "user@domain..com"
            };

            foreach (var email in invalidEmails)
            {
                var result = ValidateEmail(email);
                result.IsSuccess.ShouldBeFalse($"Email '{email}' should be invalid");
                result.Error.Type.ShouldBe(ErrorType.Validation);
            }
        }

        [Test]
        [Category("PhoneValidation")]
        public void PhoneValueObject_ShouldValidateFormats()
        {
            // Arrange & Act & Assert - Valid phone numbers
            var validPhones = new[]
            {
                "+1-555-123-4567",
                "+44-20-7946-0958",
                "+81-3-1234-5678",
                "555-1234",
                "(555) 123-4567",
                "555.123.4567"
            };

            foreach (var phone in validPhones)
            {
                var result = ValidatePhoneNumber(phone);
                result.IsSuccess.ShouldBeTrue($"Phone '{phone}' should be valid");
            }

            // Invalid phone numbers
            var invalidPhones = new[]
            {
                "",
                "abc-def-ghij",
                "123",
                "+1-555-123-456",
                "555-12345-67890",
                "phone-number"
            };

            foreach (var phone in invalidPhones)
            {
                var result = ValidatePhoneNumber(phone);
                result.IsSuccess.ShouldBeFalse($"Phone '{phone}' should be invalid");
            }
        }

        [Test]
        [Category("UrlValidation")]
        public void UrlValueObject_ShouldValidateUris()
        {
            // Valid URLs
            var validUrls = new[]
            {
                "https://www.example.com",
                "http://localhost:8080",
                "https://sub.domain.co.uk/path?query=value",
                "ftp://files.example.com/path/file.txt",
                "https://example.com:443/secure/path"
            };

            foreach (var url in validUrls)
            {
                var result = ValidateUrl(url);
                result.IsSuccess.ShouldBeTrue($"URL '{url}' should be valid");
            }

            // Invalid URLs
            var invalidUrls = new[]
            {
                "",
                "not-a-url",
                "http://",
                "://example.com",
                "http://.example.com",
                "invalid-protocol://example.com"
            };

            foreach (var url in invalidUrls)
            {
                var result = ValidateUrl(url);
                result.IsSuccess.ShouldBeFalse($"URL '{url}' should be invalid");
            }
        }

        private static Result<string> ValidateEmail(string email)
        {
            if (string.IsNullOrWhiteSpace(email))
                return Result<string>.Failure(Error.Validation("Email cannot be empty", "EMAIL_EMPTY"));

            if (!email.Contains('@') || email.StartsWith('@') || email.EndsWith('@'))
                return Result<string>.Failure(Error.Validation("Invalid email format", "EMAIL_INVALID_FORMAT"));

            var parts = email.Split('@');
            if (parts.Length != 2 || string.IsNullOrWhiteSpace(parts[0]) || string.IsNullOrWhiteSpace(parts[1]))
                return Result<string>.Failure(Error.Validation("Invalid email structure", "EMAIL_INVALID_STRUCTURE"));

            if (parts[1].Contains("..") || parts[0].Contains(".."))
                return Result<string>.Failure(Error.Validation("Email contains consecutive dots", "EMAIL_CONSECUTIVE_DOTS"));

            return Result<string>.Success(email);
        }

        private static Result<string> ValidatePhoneNumber(string phone)
        {
            if (string.IsNullOrWhiteSpace(phone))
                return Result<string>.Failure(Error.Validation("Phone number cannot be empty", "PHONE_EMPTY"));

            // Simple validation - at least 4 digits
            var digitsOnly = new string(phone.Where(char.IsDigit).ToArray());
            if (digitsOnly.Length < 4)
                return Result<string>.Failure(Error.Validation("Phone number too short", "PHONE_TOO_SHORT"));

            if (digitsOnly.Length > 15)
                return Result<string>.Failure(Error.Validation("Phone number too long", "PHONE_TOO_LONG"));

            return Result<string>.Success(phone);
        }

        private static Result<string> ValidateUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
                return Result<string>.Failure(Error.Validation("URL cannot be empty", "URL_EMPTY"));

            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
                return Result<string>.Failure(Error.Validation("Invalid URL format", "URL_INVALID_FORMAT"));

            if (string.IsNullOrWhiteSpace(uri.Scheme))
                return Result<string>.Failure(Error.Validation("URL missing scheme", "URL_MISSING_SCHEME"));

            return Result<string>.Success(url);
        }
    }

    #endregion

    #region Primitive Wrapper Testing

    /// <summary>
    /// Tests for primitive type wrappers and validation
    /// </summary>
    [TestFixture]
    public class PrimitiveWrapperTests
    {
        [Test]
        [Category("StringValidation")]
        public void StringPrimitives_ShouldValidateConstraints()
        {
            // Required string validation
            ValidateRequiredString("").IsSuccess.ShouldBeFalse();
            ValidateRequiredString("   ").IsSuccess.ShouldBeFalse();
            ValidateRequiredString("valid").IsSuccess.ShouldBeTrue();

            // Length constraints
            ValidateStringLength("short", 10, 20).IsSuccess.ShouldBeFalse();
            ValidateStringLength("perfect length", 10, 20).IsSuccess.ShouldBeTrue();
            ValidateStringLength("this string is way too long for the constraint", 10, 20).IsSuccess.ShouldBeFalse();

            // Pattern validation
            ValidatePattern("ABC123", @"^[A-Z]{3}\d{3}$").IsSuccess.ShouldBeTrue();
            ValidatePattern("abc123", @"^[A-Z]{3}\d{3}$").IsSuccess.ShouldBeFalse();
        }

        [Test]
        [Category("NumericValidation")]
        public void NumericPrimitives_ShouldValidateRanges()
        {
            // Integer range validation
            ValidateIntegerRange(5, 1, 10).IsSuccess.ShouldBeTrue();
            ValidateIntegerRange(0, 1, 10).IsSuccess.ShouldBeFalse();
            ValidateIntegerRange(15, 1, 10).IsSuccess.ShouldBeFalse();

            // Decimal precision validation
            ValidateDecimalPrecision(123.45m, 2).IsSuccess.ShouldBeTrue();
            ValidateDecimalPrecision(123.456m, 2).IsSuccess.ShouldBeFalse();

            // Positive number validation
            ValidatePositiveNumber(10).IsSuccess.ShouldBeTrue();
            ValidatePositiveNumber(0).IsSuccess.ShouldBeFalse();
            ValidatePositiveNumber(-5).IsSuccess.ShouldBeFalse();
        }

        [Test]
        [Category("DateTimeValidation")]
        public void DateTimePrimitives_ShouldValidateRanges()
        {
            var now = DateTime.UtcNow;
            var past = now.AddDays(-30);
            var future = now.AddDays(30);

            // Date range validation
            ValidateDateRange(now, past, future).IsSuccess.ShouldBeTrue();
            ValidateDateRange(past.AddDays(-1), past, future).IsSuccess.ShouldBeFalse();
            ValidateDateRange(future.AddDays(1), past, future).IsSuccess.ShouldBeFalse();

            // Future date validation
            ValidateFutureDate(future).IsSuccess.ShouldBeTrue();
            ValidateFutureDate(past).IsSuccess.ShouldBeFalse();

            // Business hours validation
            var businessHour = new DateTime(2024, 1, 15, 10, 0, 0); // Monday 10 AM
            var afterHours = new DateTime(2024, 1, 15, 22, 0, 0); // Monday 10 PM
            var weekend = new DateTime(2024, 1, 14, 10, 0, 0); // Sunday 10 AM

            ValidateBusinessHours(businessHour).IsSuccess.ShouldBeTrue();
            ValidateBusinessHours(afterHours).IsSuccess.ShouldBeFalse();
            ValidateBusinessHours(weekend).IsSuccess.ShouldBeFalse();
        }

        [Test]
        [Category("GuidValidation")]
        public void GuidPrimitives_ShouldValidateFormats()
        {
            // Valid GUIDs
            var validGuid = Guid.NewGuid();
            ValidateGuid(validGuid).IsSuccess.ShouldBeTrue();
            ValidateNonEmptyGuid(validGuid).IsSuccess.ShouldBeTrue();

            // Empty GUID
            ValidateGuid(Guid.Empty).IsSuccess.ShouldBeTrue();
            ValidateNonEmptyGuid(Guid.Empty).IsSuccess.ShouldBeFalse();

            // GUID string parsing
            ValidateGuidString(validGuid.ToString()).IsSuccess.ShouldBeTrue();
            ValidateGuidString("not-a-guid").IsSuccess.ShouldBeFalse();
            ValidateGuidString("").IsSuccess.ShouldBeFalse();
        }

        #region Validation Helper Methods

        private static Result<string> ValidateRequiredString(string value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? Result<string>.Failure(Error.Validation("String is required", "STRING_REQUIRED"))
                : Result<string>.Success(value);
        }

        private static Result<string> ValidateStringLength(string value, int minLength, int maxLength)
        {
            if (string.IsNullOrEmpty(value))
                return Result<string>.Failure(Error.Validation("String cannot be null or empty", "STRING_NULL_EMPTY"));

            if (value.Length < minLength)
                return Result<string>.Failure(Error.Validation($"String too short (min: {minLength})", "STRING_TOO_SHORT"));

            if (value.Length > maxLength)
                return Result<string>.Failure(Error.Validation($"String too long (max: {maxLength})", "STRING_TOO_LONG"));

            return Result<string>.Success(value);
        }

        private static Result<string> ValidatePattern(string value, string pattern)
        {
            if (string.IsNullOrEmpty(value))
                return Result<string>.Failure(Error.Validation("Value cannot be null or empty", "VALUE_NULL_EMPTY"));

            if (!System.Text.RegularExpressions.Regex.IsMatch(value, pattern))
                return Result<string>.Failure(Error.Validation($"Value does not match pattern: {pattern}", "PATTERN_MISMATCH"));

            return Result<string>.Success(value);
        }

        private static Result<int> ValidateIntegerRange(int value, int min, int max)
        {
            if (value < min || value > max)
                return Result<int>.Failure(Error.Validation($"Value {value} is outside range [{min}, {max}]", "INTEGER_OUT_OF_RANGE"));

            return Result<int>.Success(value);
        }

        private static Result<decimal> ValidateDecimalPrecision(decimal value, int decimalPlaces)
        {
            var factor = (decimal)Math.Pow(10, decimalPlaces);
            if (value != Math.Round(value, decimalPlaces))
                return Result<decimal>.Failure(Error.Validation($"Decimal has more than {decimalPlaces} decimal places", "DECIMAL_PRECISION_EXCEEDED"));

            return Result<decimal>.Success(value);
        }

        private static Result<int> ValidatePositiveNumber(int value)
        {
            return value <= 0
                ? Result<int>.Failure(Error.Validation("Number must be positive", "NUMBER_NOT_POSITIVE"))
                : Result<int>.Success(value);
        }

        private static Result<DateTime> ValidateDateRange(DateTime value, DateTime min, DateTime max)
        {
            if (value < min || value > max)
                return Result<DateTime>.Failure(Error.Validation($"Date {value} is outside range [{min}, {max}]", "DATE_OUT_OF_RANGE"));

            return Result<DateTime>.Success(value);
        }

        private static Result<DateTime> ValidateFutureDate(DateTime value)
        {
            return value <= DateTime.UtcNow
                ? Result<DateTime>.Failure(Error.Validation("Date must be in the future", "DATE_NOT_FUTURE"))
                : Result<DateTime>.Success(value);
        }

        private static Result<DateTime> ValidateBusinessHours(DateTime value)
        {
            // Monday-Friday, 9 AM - 5 PM
            if (value.DayOfWeek == DayOfWeek.Saturday || value.DayOfWeek == DayOfWeek.Sunday)
                return Result<DateTime>.Failure(Error.Validation("Date must be on a weekday", "DATE_NOT_WEEKDAY"));

            if (value.Hour < 9 || value.Hour >= 17)
                return Result<DateTime>.Failure(Error.Validation("Date must be during business hours (9 AM - 5 PM)", "DATE_NOT_BUSINESS_HOURS"));

            return Result<DateTime>.Success(value);
        }

        private static Result<Guid> ValidateGuid(Guid value)
        {
            return Result<Guid>.Success(value);
        }

        private static Result<Guid> ValidateNonEmptyGuid(Guid value)
        {
            return value == Guid.Empty
                ? Result<Guid>.Failure(Error.Validation("GUID cannot be empty", "GUID_EMPTY"))
                : Result<Guid>.Success(value);
        }

        private static Result<Guid> ValidateGuidString(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return Result<Guid>.Failure(Error.Validation("GUID string cannot be empty", "GUID_STRING_EMPTY"));

            if (!Guid.TryParse(value, out var guid))
                return Result<Guid>.Failure(Error.Validation("Invalid GUID format", "GUID_INVALID_FORMAT"));

            return Result<Guid>.Success(guid);
        }

        #endregion
    }

    #endregion

    #region Cross-Cultural Validation Testing

    /// <summary>
    /// Tests validation logic across different cultures and locales
    /// </summary>
    [TestFixture]
    public class CrossCulturalValidationTests
    {
        [Test]
        [Category("CultureSpecific")]
        public void NumericValidation_ShouldHandleDifferentCultures()
        {
            var cultures = new[]
            {
                CultureInfo.GetCultureInfo("en-US"), // Decimal: 1.23
                CultureInfo.GetCultureInfo("de-DE"), // Decimal: 1,23
                CultureInfo.GetCultureInfo("fr-FR"), // Decimal: 1,23
            };

            foreach (var culture in cultures)
            {
                var originalCulture = CultureInfo.CurrentCulture;
                try
                {
                    CultureInfo.CurrentCulture = culture;

                    // Test decimal parsing in different cultures
                    var decimalValue = 1234.56m;
                    var formatted = decimalValue.ToString("F2", culture);
                    
                    // Should be able to parse back correctly
                    if (decimal.TryParse(formatted, NumberStyles.Number, culture, out var parsed))
                    {
                        parsed.ShouldBe(decimalValue, $"Decimal parsing failed for culture {culture.Name}");
                    }
                    else
                    {
                        Assert.Fail($"Failed to parse decimal in culture {culture.Name}");
                    }
                }
                finally
                {
                    CultureInfo.CurrentCulture = originalCulture;
                }
            }
        }

        [Test]
        [Category("CultureSpecific")]
        public void DateTimeValidation_ShouldHandleDifferentFormats()
        {
            var cultures = new[]
            {
                CultureInfo.GetCultureInfo("en-US"), // MM/dd/yyyy
                CultureInfo.GetCultureInfo("en-GB"), // dd/MM/yyyy
                CultureInfo.GetCultureInfo("de-DE"), // dd.MM.yyyy
                CultureInfo.GetCultureInfo("ja-JP"), // yyyy/MM/dd
            };

            var testDate = new DateTime(2024, 3, 15, 14, 30, 0);

            foreach (var culture in cultures)
            {
                var originalCulture = CultureInfo.CurrentCulture;
                try
                {
                    CultureInfo.CurrentCulture = culture;

                    // Test date formatting and parsing
                    var formatted = testDate.ToString("d", culture);
                    
                    if (DateTime.TryParse(formatted, culture, DateTimeStyles.None, out var parsed))
                    {
                        parsed.Date.ShouldBe(testDate.Date, $"Date parsing failed for culture {culture.Name}");
                    }
                    else
                    {
                        Assert.Fail($"Failed to parse date in culture {culture.Name}");
                    }
                }
                finally
                {
                    CultureInfo.CurrentCulture = originalCulture;
                }
            }
        }
    }

    #endregion

    #region Serialization Validation Testing

    /// <summary>
    /// Tests primitive type serialization and validation
    /// </summary>
    [TestFixture]
    public class SerializationValidationTests
    {
        [Test]
        [Category("JsonSerialization")]
        public void PrimitiveTypes_ShouldSerializeAndValidateCorrectly()
        {
            // Test various primitive types
            var testData = new
            {
                StringValue = "test string",
                IntValue = 42,
                DecimalValue = 123.45m,
                BoolValue = true,
                DateValue = DateTime.UtcNow,
                GuidValue = Guid.NewGuid(),
                NullableIntValue = (int?)null,
                EnumValue = ErrorType.Validation
            };

            // Serialize to JSON
            var json = JsonSerializer.Serialize(testData, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = true
            });

            json.ShouldNotBeNullOrEmpty();

            // Deserialize and validate
            var deserialized = JsonSerializer.Deserialize<dynamic>(json);
            deserialized.ShouldNotBeNull();

            // Validate that serialization preserved types appropriately
            ValidateJsonSerialization(json).IsSuccess.ShouldBeTrue();
        }

        [Test]
        [Category("EdgeCaseSerialization")]
        public void EdgeCasePrimitives_ShouldHandleSerializationGracefully()
        {
            var edgeCases = new[]
            {
                new { Value = (string?)null, Type = "null string" },
                new { Value = string.Empty, Type = "empty string" },
                new { Value = "   ", Type = "whitespace string" },
                new { Value = int.MaxValue, Type = "max int" },
                new { Value = int.MinValue, Type = "min int" },
                new { Value = DateTime.MinValue, Type = "min date" },
                new { Value = DateTime.MaxValue, Type = "max date" },
                new { Value = Guid.Empty, Type = "empty guid" }
            };

            foreach (var testCase in edgeCases)
            {
                try
                {
                    var json = JsonSerializer.Serialize(testCase);
                    json.ShouldNotBeNullOrEmpty($"Serialization failed for {testCase.Type}");
                    
                    var deserialized = JsonSerializer.Deserialize<dynamic>(json);
                    deserialized.ShouldNotBeNull($"Deserialization failed for {testCase.Type}");
                }
                catch (Exception ex)
                {
                    Assert.Fail($"Serialization/deserialization failed for {testCase.Type}: {ex.Message}");
                }
            }
        }

        private static Result<string> ValidateJsonSerialization(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                return Result<string>.Failure(Error.Validation("JSON cannot be empty", "JSON_EMPTY"));

            try
            {
                using var document = JsonDocument.Parse(json);
                return Result<string>.Success(json);
            }
            catch (JsonException ex)
            {
                return Result<string>.Failure(Error.Validation($"Invalid JSON format: {ex.Message}", "JSON_INVALID_FORMAT", ex));
            }
        }
    }

    #endregion

    #region Performance Validation Testing

    /// <summary>
    /// Tests validation performance under load
    /// </summary>
    [TestFixture]
    public class ValidationPerformanceTests
    {
        [Test]
        [Category("Performance")]
        [Category("Load")]
        public async Task ValidationOperations_ShouldPerformUnderLoad()
        {
            const int operationCount = 10_000;
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            // Test string validation performance
            var stringValidations = Enumerable.Range(0, operationCount)
                .Select(i => ValidateRequiredString($"test-string-{i}"))
                .ToList();

            stopwatch.Stop();
            
            // All validations should succeed
            stringValidations.ShouldAllBe(r => r.IsSuccess);
            
            // Performance should be reasonable (under 1 second for 10K operations)
            stopwatch.ElapsedMilliseconds.ShouldBeLessThan(1000, 
                $"String validation took {stopwatch.ElapsedMilliseconds}ms for {operationCount} operations");

            await TestContext.Out.WriteLineAsync($"String validation: {operationCount} operations in {stopwatch.ElapsedMilliseconds}ms");
        }

        [Test]
        [Category("Performance")]
        [Category("Memory")]
        public void ValidationOperations_ShouldNotLeakMemory()
        {
            const int operationCount = 100_000;
            
            GC.Collect();
            GC.WaitForPendingFinalizers();
            var initialMemory = GC.GetTotalMemory(false);

            // Perform many validation operations
            for (int i = 0; i < operationCount; i++)
            {
                var result = ValidateRequiredString($"test-{i}");
                result.IsSuccess.ShouldBeTrue();
            }

            GC.Collect();
            GC.WaitForPendingFinalizers();
            var finalMemory = GC.GetTotalMemory(false);

            var memoryIncrease = finalMemory - initialMemory;
            var memoryIncreaseKB = memoryIncrease / 1024.0;

            // Memory increase should be reasonable (less than 10KB per 1000 operations)
            memoryIncreaseKB.ShouldBeLessThan(operationCount / 100.0, 
                $"Memory increase was {memoryIncreaseKB:F2} KB for {operationCount} operations");

            TestContext.Out.WriteLine($"Memory validation: {operationCount} operations increased memory by {memoryIncreaseKB:F2} KB");
        }

        private static Result<string> ValidateRequiredString(string value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? Result<string>.Failure(Error.Validation("String is required", "STRING_REQUIRED"))
                : Result<string>.Success(value);
        }
    }

    #endregion
}