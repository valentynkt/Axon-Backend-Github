using Axon.Api.Contracts.Chat;
using Axon.Api.Endpoints.Chat.ProcessMessage;
using FluentValidation.TestHelper;
using NUnit.Framework;

namespace Axon.Api.Tests.Endpoints.Chat.ProcessMessage;

/// <summary>
/// Unit tests for ProcessMessageRequestValidator (API layer)
/// </summary>
[TestFixture]
[Category("Unit")]
[Category("Api")]
[Category("Validation")]
public sealed class ProcessMessageRequestValidatorTests
{
    private ProcessMessageRequestValidator _validator = null!;

    [SetUp]
    public void SetUp()
    {
        _validator = new ProcessMessageRequestValidator();
    }

    #region Message Validation Tests

    [Test]
    public void Should_HaveError_WhenMessageIsEmpty()
    {
        // Arrange
        var request = new ProcessMessageRequest("");

        // Act & Assert
        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.Message)
            .WithErrorMessage("Message cannot be empty");
    }

    [Test]
    public void Should_HaveError_WhenMessageIsNull()
    {
        // Arrange
        var request = new ProcessMessageRequest(null!);

        // Act & Assert
        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.Message)
            .WithErrorMessage("Message cannot be empty");
    }

    [Test]
    public void Should_HaveError_WhenMessageExceedsMaxLength()
    {
        // Arrange
        var longMessage = new string('a', 10001); // Exceeds 10,000 character limit
        var request = new ProcessMessageRequest(longMessage);

        // Act & Assert
        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.Message)
            .WithErrorMessage("Message cannot exceed 10,000 characters");
    }

    [Test]
    public void Should_NotHaveError_WhenMessageIsValid()
    {
        // Arrange
        var request = new ProcessMessageRequest("Valid message");

        // Act & Assert
        var result = _validator.TestValidate(request);
        result.ShouldNotHaveValidationErrorFor(x => x.Message);
    }

    [Test]
    public void Should_NotHaveError_WhenMessageIsAtMaxLength()
    {
        // Arrange
        var maxLengthMessage = new string('a', 10000); // Exactly at limit
        var request = new ProcessMessageRequest(maxLengthMessage);

        // Act & Assert
        var result = _validator.TestValidate(request);
        result.ShouldNotHaveValidationErrorFor(x => x.Message);
    }

    #endregion


    #region Complex Validation Scenarios

    [Test]
    public void Should_HaveError_WhenMessageIsInvalid()
    {
        // Arrange
        var request = new ProcessMessageRequest(""); // Invalid: empty

        // Act & Assert
        var result = _validator.TestValidate(request);
        
        result.ShouldHaveValidationErrorFor(x => x.Message);
    }

    [Test]
    public void Should_NotHaveAnyErrors_WhenAllFieldsAreValid()
    {
        // Arrange
        var request = new ProcessMessageRequest(
            Message: "Valid test message",
            ConversationId: "conversation-456");

        // Act & Assert
        var result = _validator.TestValidate(request);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Test]
    public void Should_NotHaveAnyErrors_WhenOnlyMessageIsProvided()
    {
        // Arrange
        var request = new ProcessMessageRequest("Simple test message");

        // Act & Assert
        var result = _validator.TestValidate(request);
        result.ShouldNotHaveAnyValidationErrors();
    }

    #endregion

    #region Edge Cases

    [Test]
    public void Should_NotHaveError_WhenMessageContainsSpecialCharacters()
    {
        // Arrange
        var messageWithSpecialChars = "Test message with special chars: àáâãäåæçèéêë 中文 🎉 @#$%^&*()";
        var request = new ProcessMessageRequest(messageWithSpecialChars);

        // Act & Assert
        var result = _validator.TestValidate(request);
        result.ShouldNotHaveValidationErrorFor(x => x.Message);
    }

    [Test]
    public void Should_NotHaveError_WhenMessageContainsNewlines()
    {
        // Arrange
        var messageWithNewlines = "Line 1\nLine 2\r\nLine 3";
        var request = new ProcessMessageRequest(messageWithNewlines);

        // Act & Assert
        var result = _validator.TestValidate(request);
        result.ShouldNotHaveValidationErrorFor(x => x.Message);
    }

    [Test]
    public void Should_NotHaveError_WhenConversationIdIsProvided()
    {
        // Arrange
        var request = new ProcessMessageRequest(
            "Test message",
            ConversationId: "conversation-123");

        // Act & Assert
        var result = _validator.TestValidate(request);
        result.ShouldNotHaveValidationErrorFor(x => x.ConversationId);
    }

    [Test]
    public void Should_NotHaveError_WhenConversationIdIsGuid()
    {
        // Arrange
        var request = new ProcessMessageRequest(
            "Test message",
            ConversationId: Guid.NewGuid().ToString());

        // Act & Assert
        var result = _validator.TestValidate(request);
        result.ShouldNotHaveValidationErrorFor(x => x.ConversationId);
    }

    #endregion
}