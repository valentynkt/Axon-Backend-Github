using Axon.Modules.Identity.Application.Queries.GetMyPrincipal;
using BuildingBlocks.Primitives.Ids;
using FluentValidation.TestHelper;
using Shouldly;

namespace Axon.Modules.Identity.Application.Tests.Validation;

/// <summary>
/// Test suite for the refactored GetMyPrincipalQuery validator.
/// Tests validation of the simplified query structure that uses AxonPrincipalId directly.
/// </summary>
[TestFixture]
public class GetMyPrincipalValidatorRefactoredTests
{
    private GetMyPrincipalValidator _validator = null!;

    [SetUp]
    public void SetUp()
    {
        _validator = new GetMyPrincipalValidator();
    }

    #region Principal ID Validation Tests

    [Test]
    public void Validate_WithValidPrincipalId_Should_NotHaveValidationError()
    {
        // Arrange
        var principalId = new AxonUserId(Guid.NewGuid());
        var query = new GetMyPrincipalQuery(principalId, null);

        // Act
        var result = _validator.TestValidate(query);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.PrincipalId);
    }

    [Test]
    public void Validate_WithEmptyPrincipalId_Should_HaveValidationError()
    {
        // Arrange
        var principalId = new AxonUserId(Guid.Empty);
        var query = new GetMyPrincipalQuery(principalId, null);

        // Act
        var result = _validator.TestValidate(query);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.PrincipalId)
            .WithErrorMessage("Principal ID cannot be empty");
    }

    #endregion

    #region If-None-Match Validation Tests

    [Test]
    public void Validate_WithNullIfNoneMatch_Should_NotHaveValidationError()
    {
        // Arrange
        var principalId = new AxonUserId(Guid.NewGuid());
        var query = new GetMyPrincipalQuery(principalId, null);

        // Act
        var result = _validator.TestValidate(query);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.IfNoneMatch);
    }

    [Test]
    public void Validate_WithEmptyIfNoneMatch_Should_NotHaveValidationError()
    {
        // Arrange
        var principalId = new AxonUserId(Guid.NewGuid());
        var query = new GetMyPrincipalQuery(principalId, "");

        // Act
        var result = _validator.TestValidate(query);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.IfNoneMatch);
    }

    [Test]
    public void Validate_WithTooLongIfNoneMatch_Should_HaveValidationError()
    {
        // Arrange
        var principalId = new AxonUserId(Guid.NewGuid());
        var longIfNoneMatch = new string('a', 65); // Exceeds 64 character limit
        var query = new GetMyPrincipalQuery(principalId, longIfNoneMatch);

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
        var principalId = new AxonUserId(Guid.NewGuid());
        var maxLengthIfNoneMatch = new string('a', 64);
        var query = new GetMyPrincipalQuery(principalId, maxLengthIfNoneMatch);

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
        var principalId = new AxonUserId(Guid.NewGuid());
        var query = new GetMyPrincipalQuery(principalId, ifNoneMatch);

        // Act
        var result = _validator.TestValidate(query);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.IfNoneMatch);
    }

    #endregion

    #region Complete Query Validation Tests

    [Test]
    public void Validate_WithAllValidParameters_Should_NotHaveValidationError()
    {
        // Arrange
        var principalId = new AxonUserId(Guid.NewGuid());
        var query = new GetMyPrincipalQuery(principalId, "\"abc123def456\"");

        // Act
        var result = _validator.TestValidate(query);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Test]
    public void Validate_WithMinimalValidParameters_Should_NotHaveValidationError()
    {
        // Arrange
        var principalId = new AxonUserId(Guid.NewGuid());
        var query = new GetMyPrincipalQuery(principalId, null);

        // Act
        var result = _validator.TestValidate(query);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Test]
    public void Validate_WithMultipleErrors_Should_ReportAllErrors()
    {
        // Arrange
        var principalId = new AxonUserId(Guid.Empty); // Invalid principal ID
        var query = new GetMyPrincipalQuery(principalId, new string('a', 65)); // Invalid If-None-Match

        // Act
        var result = _validator.TestValidate(query);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.PrincipalId);
        result.ShouldHaveValidationErrorFor(x => x.IfNoneMatch);
    }

    #endregion

    #region Realistic Scenarios Tests

    [Test]
    public void Validate_WithRealisticScenarios_Should_HandleCorrectly()
    {
        var scenarios = new[]
        {
            // Scenario 1: Fresh request without caching
            (new AxonUserId(Guid.NewGuid()), (string?)null),

            // Scenario 2: Request with ETag for caching
            (new AxonUserId(Guid.NewGuid()), "\"abc123def456\""),

            // Scenario 3: Request with weak ETag
            (new AxonUserId(Guid.NewGuid()), "W/\"weak-etag-123\""),

            // Scenario 4: Request with quoted ETag
            (new AxonUserId(Guid.NewGuid()), "\"strong-etag-789\""),

            // Scenario 5: Request with simple ETag
            (new AxonUserId(Guid.NewGuid()), "simple-etag")
        };

        foreach (var (principalId, ifNoneMatch) in scenarios)
        {
            // Arrange
            var query = new GetMyPrincipalQuery(principalId, ifNoneMatch);

            // Act
            var result = _validator.TestValidate(query);

            // Assert
            result.ShouldNotHaveAnyValidationErrors();
        }
    }

    #endregion
}