using Axon.Modules.Identity.Application.Queries.GetMyPrincipal;
using Axon.Modules.Identity.Domain.ValueObjects;
using FluentValidation.TestHelper;
using Shouldly;

namespace Axon.Modules.Identity.Application.Tests.Validation;

/// <summary>
/// Comprehensive test suite for GetMyPrincipalQuery validator.
/// Tests all validation rules, edge cases, and security constraints.
/// </summary>
[TestFixture]
public class GetMyPrincipalValidatorTests
{
    private GetMyPrincipalValidator _validator = null!;

    [SetUp]
    public void SetUp()
    {
        _validator = new GetMyPrincipalValidator();
    }

    #region Provider Type Validation Tests

    [TestFixture]
    public class ProviderTypeValidationTests : GetMyPrincipalValidatorTests
    {
        [Test]
        public void Validate_WithNullProviderType_Should_HaveValidationError()
        {
            // Arrange
            var query = new GetMyPrincipalQuery(null!, "issuer", "subject", null);

            // Act
            var result = _validator.TestValidate(query);

            // Assert
            result.ShouldHaveValidationErrorFor(x => x.ProviderType)
                .WithErrorMessage("Provider type is required");
        }

        [Test]
        public void Validate_WithValidProviderType_Should_NotHaveValidationError()
        {
            // Arrange
            var providerType = ProviderType.Create("dynamic").Value;
            var query = new GetMyPrincipalQuery(providerType, "issuer", "subject", null);

            // Act
            var result = _validator.TestValidate(query);

            // Assert
            result.ShouldNotHaveValidationErrorFor(x => x.ProviderType);
        }

        [Test]
        [TestCase("dynamic")]
        [TestCase("github")]
        [TestCase("google")]
        [TestCase("discord")]
        [TestCase("twitter")]
        public void Validate_WithDifferentValidProviderTypes_Should_NotHaveValidationError(string providerValue)
        {
            // Arrange
            var providerType = ProviderType.Create(providerValue).Value;
            var query = new GetMyPrincipalQuery(providerType, "issuer", "subject", null);

            // Act
            var result = _validator.TestValidate(query);

            // Assert
            result.ShouldNotHaveValidationErrorFor(x => x.ProviderType);
        }
    }

    #endregion

    #region Issuer Validation Tests

    [TestFixture]
    public class IssuerValidationTests : GetMyPrincipalValidatorTests
    {
        [Test]
        [TestCase("")]
        [TestCase("   ")]
        [TestCase(null)]
        public void Validate_WithInvalidIssuer_Should_HaveValidationError(string? issuer)
        {
            // Arrange
            var providerType = ProviderType.Create("dynamic").Value;
            var query = new GetMyPrincipalQuery(providerType, issuer!, "subject", null);

            // Act
            var result = _validator.TestValidate(query);

            // Assert
            result.ShouldHaveValidationErrorFor(x => x.Issuer)
                .WithErrorMessage("Issuer is required");
        }

        [Test]
        public void Validate_WithTooLongIssuer_Should_HaveValidationError()
        {
            // Arrange
            var longIssuer = new string('a', 256); // Exceeds 255 character limit
            var providerType = ProviderType.Create("dynamic").Value;
            var query = new GetMyPrincipalQuery(providerType, longIssuer, "subject", null);

            // Act
            var result = _validator.TestValidate(query);

            // Assert
            result.ShouldHaveValidationErrorFor(x => x.Issuer)
                .WithErrorMessage("Issuer cannot exceed 255 characters");
        }

        [Test]
        public void Validate_WithMaxLengthIssuer_Should_NotHaveValidationError()
        {
            // Arrange
            var maxLengthIssuer = new string('a', 255);
            var providerType = ProviderType.Create("dynamic").Value;
            var query = new GetMyPrincipalQuery(providerType, maxLengthIssuer, "subject", null);

            // Act
            var result = _validator.TestValidate(query);

            // Assert
            result.ShouldNotHaveValidationErrorFor(x => x.Issuer);
        }

        [Test]
        [TestCase("app.dynamicauth.com/test-env")]
        [TestCase("github.com")]
        [TestCase("accounts.google.com")]
        [TestCase("appleid.apple.com")]
        [TestCase("https://auth.example.com")]
        [TestCase("urn:example:issuer")]
        public void Validate_WithValidIssuers_Should_NotHaveValidationError(string issuer)
        {
            // Arrange
            var providerType = ProviderType.Create("dynamic").Value;
            var query = new GetMyPrincipalQuery(providerType, issuer, "subject", null);

            // Act
            var result = _validator.TestValidate(query);

            // Assert
            result.ShouldNotHaveValidationErrorFor(x => x.Issuer);
        }

        [Test]
        public void Validate_WithSpecialCharactersInIssuer_Should_NotHaveValidationError()
        {
            // Arrange
            var issuerWithSpecialChars = "https://auth.example.com/oauth2/v1?client_id=123&scope=openid";
            var providerType = ProviderType.Create("dynamic").Value;
            var query = new GetMyPrincipalQuery(providerType, issuerWithSpecialChars, "subject", null);

            // Act
            var result = _validator.TestValidate(query);

            // Assert
            result.ShouldNotHaveValidationErrorFor(x => x.Issuer);
        }
    }

    #endregion

    #region Subject Validation Tests

    [TestFixture]
    public class SubjectValidationTests : GetMyPrincipalValidatorTests
    {
        [Test]
        [TestCase("")]
        [TestCase("   ")]
        [TestCase(null)]
        public void Validate_WithInvalidSubject_Should_HaveValidationError(string? subject)
        {
            // Arrange
            var providerType = ProviderType.Create("dynamic").Value;
            var query = new GetMyPrincipalQuery(providerType, "issuer", subject!, null);

            // Act
            var result = _validator.TestValidate(query);

            // Assert
            result.ShouldHaveValidationErrorFor(x => x.Subject)
                .WithErrorMessage("Subject is required");
        }

        [Test]
        public void Validate_WithTooLongSubject_Should_HaveValidationError()
        {
            // Arrange
            var longSubject = new string('a', 256); // Exceeds 255 character limit
            var providerType = ProviderType.Create("dynamic").Value;
            var query = new GetMyPrincipalQuery(providerType, "issuer", longSubject, null);

            // Act
            var result = _validator.TestValidate(query);

            // Assert
            result.ShouldHaveValidationErrorFor(x => x.Subject)
                .WithErrorMessage("Subject cannot exceed 255 characters");
        }

        [Test]
        public void Validate_WithMaxLengthSubject_Should_NotHaveValidationError()
        {
            // Arrange
            var maxLengthSubject = new string('a', 255);
            var providerType = ProviderType.Create("dynamic").Value;
            var query = new GetMyPrincipalQuery(providerType, "issuer", maxLengthSubject, null);

            // Act
            var result = _validator.TestValidate(query);

            // Assert
            result.ShouldNotHaveValidationErrorFor(x => x.Subject);
        }

        [Test]
        [TestCase("user-123")]
        [TestCase("john.doe@example.com")]
        [TestCase("1234567890")]
        [TestCase("github-user-123")]
        [TestCase("550e8400-e29b-41d4-a716-446655440000")] // UUID
        public void Validate_WithValidSubjects_Should_NotHaveValidationError(string subject)
        {
            // Arrange
            var providerType = ProviderType.Create("dynamic").Value;
            var query = new GetMyPrincipalQuery(providerType, "issuer", subject, null);

            // Act
            var result = _validator.TestValidate(query);

            // Assert
            result.ShouldNotHaveValidationErrorFor(x => x.Subject);
        }

        [Test]
        public void Validate_WithSpecialCharactersInSubject_Should_NotHaveValidationError()
        {
            // Arrange
            var subjectWithSpecialChars = "user+123_test.special-chars@domain.com";
            var providerType = ProviderType.Create("dynamic").Value;
            var query = new GetMyPrincipalQuery(providerType, "issuer", subjectWithSpecialChars, null);

            // Act
            var result = _validator.TestValidate(query);

            // Assert
            result.ShouldNotHaveValidationErrorFor(x => x.Subject);
        }

        [Test]
        public void Validate_WithUnicodeInSubject_Should_NotHaveValidationError()
        {
            // Arrange
            var unicodeSubject = "用户123-測試";
            var providerType = ProviderType.Create("dynamic").Value;
            var query = new GetMyPrincipalQuery(providerType, "issuer", unicodeSubject, null);

            // Act
            var result = _validator.TestValidate(query);

            // Assert
            result.ShouldNotHaveValidationErrorFor(x => x.Subject);
        }
    }

    #endregion

    #region If-None-Match Validation Tests

    [TestFixture]
    public class IfNoneMatchValidationTests : GetMyPrincipalValidatorTests
    {
        [Test]
        public void Validate_WithNullIfNoneMatch_Should_NotHaveValidationError()
        {
            // Arrange
            var providerType = ProviderType.Create("dynamic").Value;
            var query = new GetMyPrincipalQuery(providerType, "issuer", "subject", null);

            // Act
            var result = _validator.TestValidate(query);

            // Assert
            result.ShouldNotHaveValidationErrorFor(x => x.IfNoneMatch);
        }

        [Test]
        public void Validate_WithEmptyIfNoneMatch_Should_NotHaveValidationError()
        {
            // Arrange
            var providerType = ProviderType.Create("dynamic").Value;
            var query = new GetMyPrincipalQuery(providerType, "issuer", "subject", "");

            // Act
            var result = _validator.TestValidate(query);

            // Assert
            result.ShouldNotHaveValidationErrorFor(x => x.IfNoneMatch);
        }

        [Test]
        public void Validate_WithTooLongIfNoneMatch_Should_HaveValidationError()
        {
            // Arrange
            var longIfNoneMatch = new string('a', 65); // Exceeds 64 character limit
            var providerType = ProviderType.Create("dynamic").Value;
            var query = new GetMyPrincipalQuery(providerType, "issuer", "subject", longIfNoneMatch);

            // Act
            var result = _validator.TestValidate(query);

            // Assert
            result.ShouldHaveValidationErrorFor(x => x.IfNoneMatch)
                .WithErrorMessage("If-None-Match header cannot exceed 64 characters");
        }

        [Test]
        public void Validate_WithMaxLengthIfNoneMatch_Should_NotHaveValidationError()
        {
            // Arrange
            var maxLengthIfNoneMatch = new string('a', 64);
            var providerType = ProviderType.Create("dynamic").Value;
            var query = new GetMyPrincipalQuery(providerType, "issuer", "subject", maxLengthIfNoneMatch);

            // Act
            var result = _validator.TestValidate(query);

            // Assert
            result.ShouldNotHaveValidationErrorFor(x => x.IfNoneMatch);
        }

        [Test]
        [TestCase("W/\"abc123\"")]
        [TestCase("\"abc123\"")]
        [TestCase("abc123")]
        [TestCase("*")]
        [TestCase("W/\"0815\"")]
        public void Validate_WithValidIfNoneMatchValues_Should_NotHaveValidationError(string ifNoneMatch)
        {
            // Arrange
            var providerType = ProviderType.Create("dynamic").Value;
            var query = new GetMyPrincipalQuery(providerType, "issuer", "subject", ifNoneMatch);

            // Act
            var result = _validator.TestValidate(query);

            // Assert
            result.ShouldNotHaveValidationErrorFor(x => x.IfNoneMatch);
        }

        [Test]
        public void Validate_WithQuotedETag_Should_NotHaveValidationError()
        {
            // Arrange
            var quotedETag = "\"1234567890abcdef\"";
            var providerType = ProviderType.Create("dynamic").Value;
            var query = new GetMyPrincipalQuery(providerType, "issuer", "subject", quotedETag);

            // Act
            var result = _validator.TestValidate(query);

            // Assert
            result.ShouldNotHaveValidationErrorFor(x => x.IfNoneMatch);
        }

        [Test]
        public void Validate_WithWeakETag_Should_NotHaveValidationError()
        {
            // Arrange
            var weakETag = "W/\"1234567890abcdef\"";
            var providerType = ProviderType.Create("dynamic").Value;
            var query = new GetMyPrincipalQuery(providerType, "issuer", "subject", weakETag);

            // Act
            var result = _validator.TestValidate(query);

            // Assert
            result.ShouldNotHaveValidationErrorFor(x => x.IfNoneMatch);
        }
    }

    #endregion

    #region Complete Query Validation Tests

    [TestFixture]
    public class CompleteQueryValidationTests : GetMyPrincipalValidatorTests
    {
        [Test]
        public void Validate_WithAllValidParameters_Should_NotHaveValidationError()
        {
            // Arrange
            var providerType = ProviderType.Create("dynamic").Value;
            var query = new GetMyPrincipalQuery(
                providerType,
                "app.dynamicauth.com/test-env",
                "test-user-123",
                "\"abc123def456\"");

            // Act
            var result = _validator.TestValidate(query);

            // Assert
            result.ShouldNotHaveAnyValidationErrors();
        }

        [Test]
        public void Validate_WithMinimalValidParameters_Should_NotHaveValidationError()
        {
            // Arrange
            var providerType = ProviderType.Create("dynamic").Value;
            var query = new GetMyPrincipalQuery(providerType, "i", "s", null);

            // Act
            var result = _validator.TestValidate(query);

            // Assert
            result.ShouldNotHaveAnyValidationErrors();
        }

        [Test]
        public void Validate_WithMultipleErrors_Should_ReportAllErrors()
        {
            // Arrange
            var query = new GetMyPrincipalQuery(
                null!, // Invalid provider type
                "", // Invalid issuer
                "", // Invalid subject
                new string('a', 65)); // Invalid If-None-Match

            // Act
            var result = _validator.TestValidate(query);

            // Assert
            result.ShouldHaveValidationErrorFor(x => x.ProviderType);
            result.ShouldHaveValidationErrorFor(x => x.Issuer);
            result.ShouldHaveValidationErrorFor(x => x.Subject);
            result.ShouldHaveValidationErrorFor(x => x.IfNoneMatch);
        }
    }

    #endregion

    #region Edge Cases and Security Tests

    [TestFixture]
    public class EdgeCasesAndSecurityTests : GetMyPrincipalValidatorTests
    {
        [Test]
        public void Validate_WithBoundaryValues_Should_HandleCorrectly()
        {
            // Arrange - Test exactly at the limits
            var providerType = ProviderType.Create("dynamic").Value;
            var query = new GetMyPrincipalQuery(
                providerType,
                new string('a', 255), // Exactly at issuer limit
                new string('b', 255), // Exactly at subject limit
                new string('c', 64)); // Exactly at If-None-Match limit

            // Act
            var result = _validator.TestValidate(query);

            // Assert
            result.ShouldNotHaveAnyValidationErrors();
        }

        [Test]
        public void Validate_WithSpecialCharacters_Should_HandleCorrectly()
        {
            // Arrange
            var providerType = ProviderType.Create("discord").Value;
            var query = new GetMyPrincipalQuery(
                providerType,
                "https://auth.example.com/oauth2/v1?client_id=123&redirect_uri=https://app.com/callback",
                "user.name+tag@domain-example.com",
                "W/\"special-chars-123_456.789\"");

            // Act
            var result = _validator.TestValidate(query);

            // Assert
            result.ShouldNotHaveAnyValidationErrors();
        }

        [Test]
        public void Validate_WithPotentialInjectionAttempts_Should_BeHandledSafely()
        {
            // Test potential injection attempts - these should be validated for length/format only
            // Actual security protection happens at other layers
            var providerType = ProviderType.Create("dynamic").Value;
            var query = new GetMyPrincipalQuery(
                providerType,
                "<script>alert('xss')</script>",
                "'; DROP TABLE users; --",
                "\"<img src=x onerror=alert(1)>\"");

            // Act
            var result = _validator.TestValidate(query);

            // Assert
            // Should pass validation since these are just strings being validated for length
            result.ShouldNotHaveValidationErrorFor(x => x.Issuer);
            result.ShouldNotHaveValidationErrorFor(x => x.Subject);
            result.ShouldNotHaveValidationErrorFor(x => x.IfNoneMatch);
        }

        [Test]
        public void Validate_WithWhitespaceInOptionalField_Should_HandleCorrectly()
        {
            // Arrange
            var providerType = ProviderType.Create("dynamic").Value;
            var query = new GetMyPrincipalQuery(
                providerType,
                "issuer",
                "subject",
                "   "); // Whitespace-only If-None-Match

            // Act
            var result = _validator.TestValidate(query);

            // Assert
            result.ShouldNotHaveValidationErrorFor(x => x.IfNoneMatch);
        }

        [Test]
        public void Validate_WithRealisticScenarios_Should_HandleCorrectly()
        {
            var scenarios = new[]
            {
                // Dynamic provider scenario
                (ProviderType.Create("dynamic").Value, "app.dynamicauth.com/production", "user-12345-abcdef"),

                // GitHub provider scenario
                (ProviderType.Create("github").Value, "github.com", "octocat"),

                // Google provider scenario
                (ProviderType.Create("google").Value, "accounts.google.com", "110123456789012345678"),

                // Manual provider scenario
                (ProviderType.Create("manual").Value, "manual.verification", "wallet-0x1234567890abcdef")
            };

            foreach (var (providerType, issuer, subject) in scenarios)
            {
                // Arrange
                var query = new GetMyPrincipalQuery(providerType, issuer, subject, null);

                // Act
                var result = _validator.TestValidate(query);

                // Assert
                result.ShouldNotHaveAnyValidationErrors();
            }
        }
    }

    #endregion
}