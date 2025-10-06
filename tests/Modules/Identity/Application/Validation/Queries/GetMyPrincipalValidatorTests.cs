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
        var query = new GetMyPrincipalQuery(principalId);

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
        var query = new GetMyPrincipalQuery(principalId);

        // Act
        var result = _validator.TestValidate(query);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.PrincipalId)
            .WithErrorMessage("Principal ID cannot be empty");
    }

    #endregion
}