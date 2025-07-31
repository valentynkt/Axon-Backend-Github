using Axon.Modules.Chat.Application.Commands.ProcessMessage;
using FluentValidation.TestHelper;
using NUnit.Framework;

namespace Axon.Modules.Chat.Application.Tests.Commands.ProcessMessage;

/// <summary>
/// Unit tests for ProcessMessageValidator (Application layer)
/// </summary>
[TestFixture]
[Category("Unit")]
[Category("Application")]
[Category("Validation")]
public sealed class ProcessMessageValidatorTests
{
    private ProcessMessageValidator _validator = null!;

    [SetUp]
    public void SetUp()
    {
        _validator = new ProcessMessageValidator();
    }

    #region Message Validation Tests

    [Test]
    public void Should_HaveError_WhenMessageIsEmpty()
    {
        // Arrange
        var command = new ProcessMessageCommand("");

        // Act & Assert
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.Message)
            .WithErrorMessage("Message cannot be empty");
    }

    [Test]
    public void Should_HaveError_WhenMessageIsNull()
    {
        // Arrange
        var command = new ProcessMessageCommand(null!);

        // Act & Assert
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.Message)
            .WithErrorMessage("Message cannot be empty");
    }

    [Test]
    public void Should_HaveError_WhenMessageExceedsMaxLength()
    {
        // Arrange
        var longMessage = new string('a', 4001); // Exceeds 4000 character limit
        var command = new ProcessMessageCommand(longMessage);

        // Act & Assert
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.Message)
            .WithErrorMessage("Message cannot exceed 4000 characters");
    }

    [Test]
    public void Should_NotHaveError_WhenMessageIsValid()
    {
        // Arrange
        var command = new ProcessMessageCommand("Valid message");

        // Act & Assert
        var result = _validator.TestValidate(command);
        result.ShouldNotHaveValidationErrorFor(x => x.Message);
    }

    [Test]
    public void Should_NotHaveError_WhenMessageIsAtMaxLength()
    {
        // Arrange
        var maxLengthMessage = new string('a', 4000); // Exactly at limit
        var command = new ProcessMessageCommand(maxLengthMessage);

        // Act & Assert
        var result = _validator.TestValidate(command);
        result.ShouldNotHaveValidationErrorFor(x => x.Message);
    }

    #endregion

    #region Complex Validation Scenarios

    [Test]
    public void Should_HaveError_WhenMessageIsInvalid()
    {
        // Arrange
        var command = new ProcessMessageCommand(""); // Invalid: empty

        // Act & Assert
        var result = _validator.TestValidate(command);
        
        result.ShouldHaveValidationErrorFor(x => x.Message);
    }

    [Test]
    public void Should_NotHaveAnyErrors_WhenAllFieldsAreValid()
    {
        // Arrange
        var command = new ProcessMessageCommand(
            Message: "Valid test message",
            PreviousResponseId: "response-123" // Valid: optional
        );

        // Act & Assert
        var result = _validator.TestValidate(command);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Test]
    public void Should_NotHaveAnyErrors_WhenOnlyMessageIsProvided()
    {
        // Arrange
        var command = new ProcessMessageCommand("Simple test message");

        // Act & Assert
        var result = _validator.TestValidate(command);
        result.ShouldNotHaveAnyValidationErrors();
    }

    #endregion

    #region Edge Cases

    [Test]
    public void Should_NotHaveError_WhenMessageContainsSpecialCharacters()
    {
        // Arrange
        var messageWithSpecialChars = "Test message with special chars: àáâãäåæçèéêë 中文 🎉 @#$%^&*()";
        var command = new ProcessMessageCommand(messageWithSpecialChars);

        // Act & Assert
        var result = _validator.TestValidate(command);
        result.ShouldNotHaveValidationErrorFor(x => x.Message);
    }

    [Test]
    public void Should_NotHaveError_WhenMessageContainsNewlines()
    {
        // Arrange
        var messageWithNewlines = "Line 1\nLine 2\r\nLine 3";
        var command = new ProcessMessageCommand(messageWithNewlines);

        // Act & Assert
        var result = _validator.TestValidate(command);
        result.ShouldNotHaveValidationErrorFor(x => x.Message);
    }

    [Test]
    public void Should_NotHaveError_WhenPreviousResponseIdIsProvided()
    {
        // Arrange
        var command = new ProcessMessageCommand(
            "Test message",
            "response-123");

        // Act & Assert
        var result = _validator.TestValidate(command);
        result.ShouldNotHaveValidationErrorFor(x => x.PreviousResponseId);
    }

    [Test]
    public void Should_NotHaveError_WhenPreviousResponseIdIsGuid()
    {
        // Arrange
        var command = new ProcessMessageCommand(
            "Test message",
            Guid.NewGuid().ToString());

        // Act & Assert
        var result = _validator.TestValidate(command);
        result.ShouldNotHaveValidationErrorFor(x => x.PreviousResponseId);
    }

    #endregion
}