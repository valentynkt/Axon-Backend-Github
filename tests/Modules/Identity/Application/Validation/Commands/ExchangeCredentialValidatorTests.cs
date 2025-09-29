using Axon.Modules.Identity.Application.Commands.ExchangeCredential;
using FluentValidation.TestHelper;

namespace Axon.Modules.Identity.Application.Tests.Validation;

/// <summary>
/// Test suite for ExchangeCredentialCommand validator.
/// Tests bearer token validation rules that execute before JWT decoding.
/// </summary>
[TestFixture]
public class ExchangeCredentialValidatorTests
{
    private ExchangeCredentialCommandValidator _validator = null!;

    [SetUp]
    public void SetUp()
    {
        _validator = new ExchangeCredentialCommandValidator();
    }

    [Test]
    [TestCase(null)]
    [TestCase("")]
    [TestCase("   ")]
    public void Validate_WithEmptyBearerToken_Should_HaveValidationError(string? bearerToken)
    {
        // Arrange
        var command = new ExchangeCredentialCommand(bearerToken!);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.BearerToken)
            .WithErrorMessage("Bearer token is required");
    }

    [Test]
    [TestCase("invalid-token")]
    [TestCase("only.two")]
    [TestCase("part1..part3")]
    [TestCase("..")]
    [TestCase("header.")]
    [TestCase(".payload.signature")]
    public void Validate_WithInvalidBearerTokenFormat_Should_HaveValidationError(string bearerToken)
    {
        // Arrange
        var command = new ExchangeCredentialCommand(bearerToken);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.BearerToken)
            .WithErrorMessage("Bearer token format is invalid");
    }

    [Test]
    public void Validate_WithValidBearerToken_Should_NotHaveValidationError()
    {
        // Arrange - Valid JWT format with 3 parts
        var command = new ExchangeCredentialCommand("header.payload.signature");

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }
}