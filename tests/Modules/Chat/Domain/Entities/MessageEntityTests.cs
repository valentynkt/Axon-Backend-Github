using Axon.Modules.Chat.Domain.Tests.Builders;
using Axon.Modules.Chat.Domain.Tests.Common;
using Axon.BuildingBlocks.Core.Primitives.ValueObjects;
using Axon.Modules.Chat.Domain.Tests.Extensions;

namespace Axon.Modules.Chat.Domain.Tests.Entities;

/// <summary>
/// Tests for Message entity creation, validation, and business rules.
/// Focuses on clean, maintainable tests using parameterized approaches.
/// </summary>
[TestFixture]
public class MessageEntityTests : DomainTestBase
{
    [Test]
    public void CreateUserMessage_WithValidData_ShouldCreateSuccessfully()
    {
        // Act
        var message = MessageBuilder.NewUserMessage().Build();

        // Assert
        message.ShouldBeUserMessage();
        message.ShouldHaveSequence(1);
        message.Id.ShouldNotBe(default(MessageId));
    }

    [Test]
    public void CreateAssistantMessage_WithValidData_ShouldCreateSuccessfully()
    {
        // Arrange
        var aiResponseId = CreateAiResponseId();

        // Act
        var message = MessageBuilder.NewAssistantMessage()
            .WithAiResponseId(aiResponseId)
            .Build();

        // Assert
        message.ShouldBeAssistantMessage(aiResponseId);
        message.ShouldHaveSequence(1);
        message.Id.ShouldNotBe(default(MessageId));
    }

    [TestCase(0)]
    [TestCase(-1)]
    [TestCase(-100)]
    [TestCase(int.MinValue)]
    public void CreateMessage_WithInvalidSequence_ShouldThrowException(int invalidSequence)
    {
        // Act & Assert
        Should.Throw<InvalidOperationException>(() => 
            MessageBuilder.NewUserMessage()
                .WithSequence(invalidSequence)
                .Build())
            .Message.ShouldContain("Message sequence must be a positive integer");
    }

    [TestCase(1)]
    [TestCase(5)]
    [TestCase(100)]
    [TestCase(int.MaxValue)]
    public void CreateMessage_WithValidSequence_ShouldSucceed(int sequence)
    {
        // Act
        var message = MessageBuilder.NewUserMessage()
            .WithSequence(sequence)
            .Build();

        // Assert
        message.ShouldHaveSequence(sequence);
    }

    [Test]
    public void CreateAssistantMessage_WithoutAiResponseId_ShouldThrowException()
    {
        // Act & Assert
        Should.Throw<InvalidOperationException>(() => 
            MessageBuilder.NewAssistantMessage()
                .WithoutAiResponseId()
                .Build())
            .Message.ShouldContain("Assistant messages must have an AI response ID");
    }

    [Test]
    public void UserMessage_WithAiResponseId_ShouldViolateInvariant()
    {
        // Act & Assert
        Should.Throw<Exception>(() => 
            MessageBuilder.WithInvalidAiResponseIdConsistency().Build())
            .Message.ShouldContain("User messages cannot have an AI response ID");
    }

    [TestCase("", "Content cannot be empty")]
    [TestCase("   ", "Content cannot be empty")]
    public void CreateMessage_WithInvalidContent_ShouldFail(string content, string expectedError)
    {
        // Act
        var result = MessageBuilder.NewUserMessage()
            .WithContent(content)
            .BuildResult();

        // Assert
        result.ShouldFailWithMessage(expectedError);
    }

    [Test]
    public void CreateMessage_WithTooLongContent_ShouldFail()
    {
        // Act
        var result = MessageBuilder.NewUserMessage()
            .WithTooLongContent()
            .BuildResult();

        // Assert
        result.ShouldFailWithMessage("cannot exceed");
    }

    [Test]
    public void CreateMessage_WithMaxLengthContent_ShouldSucceed()
    {
        // Act
        var result = MessageBuilder.NewUserMessage()
            .WithMaxLengthContent()
            .BuildResult();

        // Assert
        result.ShouldBeSuccess();
    }

    [Test]
    public void CreateMessage_WithContentRequiringTrim_ShouldTrimAndSucceed()
    {
        // Act
        var message = MessageBuilder.NewUserMessage()
            .WithContentRequiringTrim()
            .Build();

        // Assert
        message.Content.Value.ShouldBe(TestConstants.Messages.ExpectedTrimmedMessage);
    }

    [Test]
    public void CreateMessage_ShouldGenerateUniqueId()
    {
        // Act
        var message1 = MessageBuilder.NewUserMessage().Build();
        var message2 = MessageBuilder.NewUserMessage().Build();

        // Assert
        message1.Id.ShouldNotBe(default(MessageId));
        message2.Id.ShouldNotBe(default(MessageId));
        message1.Id.ShouldNotBe(message2.Id);
    }
}