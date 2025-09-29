using Axon.Modules.Identity.Application.Commands.RefreshToken;
using FluentValidation.TestHelper;
using NUnit.Framework;

namespace Axon.Modules.Identity.Application.Tests.Validation.Commands;

/// <summary>
/// Test suite for RefreshTokenCommand validator.
/// Tests JWT format validation rules that execute before token verification.
/// Sprint 7 - Following ExchangeCredentialValidator test patterns.
/// </summary>
[TestFixture]
public class RefreshTokenValidatorTests
{
    private RefreshTokenCommandValidator _validator = null!;

    [SetUp]
    public void SetUp()
    {
        _validator = new RefreshTokenCommandValidator();
    }

    #region Empty/Null/Whitespace Tests (3 tests)

    [Test]
    [TestCase(null)]
    [TestCase("")]
    [TestCase("   ")]
    [TestCase("\t")]
    [TestCase("\n")]
    public void Validate_WithEmptyRefreshToken_Should_HaveValidationError(string? refreshToken)
    {
        // Arrange
        var command = new RefreshTokenCommand(refreshToken!);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.RefreshToken)
            .WithErrorMessage("Refresh token is required");
    }

    #endregion

    #region Invalid JWT Format Tests (5 tests)

    [Test]
    [TestCase("invalid-token")]
    [TestCase("only.two")]
    [TestCase("one.two.three.four")]
    [TestCase("no-dots-at-all")]
    public void Validate_WithInvalidJwtFormat_Should_HaveValidationError(string refreshToken)
    {
        // Arrange
        var command = new RefreshTokenCommand(refreshToken);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.RefreshToken)
            .WithErrorMessage("Refresh token format is invalid");
    }

    [Test]
    [TestCase("header..signature")]
    [TestCase(".payload.signature")]
    [TestCase("header.payload.")]
    [TestCase("..")]
    [TestCase("header. .signature")]
    [TestCase("header.\t.signature")]
    public void Validate_WithEmptyJwtParts_Should_HaveValidationError(string refreshToken)
    {
        // Arrange
        var command = new RefreshTokenCommand(refreshToken);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.RefreshToken)
            .WithErrorMessage("Refresh token format is invalid");
    }

    #endregion

    #region Valid JWT Format Tests (2 tests)

    [Test]
    public void Validate_WithValidJwtFormat_Should_NotHaveValidationError()
    {
        // Arrange - Valid JWT format with 3 parts
        var command = new RefreshTokenCommand("header.payload.signature");

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Test]
    [TestCase("eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJzdWIiOiIxMjM0NTY3ODkwIiwibmFtZSI6IkpvaG4gRG9lIiwiaWF0IjoxNTE2MjM5MDIyfQ.SflKxwRJSMeKKF2QT4fwpMeJf36POk6yJV_adQssw5c")]
    [TestCase("abc.def.ghi")]
    [TestCase("very-long-header-part.very-long-payload-part.very-long-signature-part")]
    [TestCase("a.b.c")]
    public void Validate_WithRealisticJwtTokens_Should_NotHaveValidationError(string refreshToken)
    {
        // Arrange
        var command = new RefreshTokenCommand(refreshToken);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    #endregion
}