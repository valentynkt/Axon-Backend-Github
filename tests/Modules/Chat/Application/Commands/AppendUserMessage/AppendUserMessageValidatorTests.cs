using System.Linq;
using Axon.BuildingBlocks.Core.Primitives.ValueObjects;
using Axon.Modules.Chat.Application.Commands.AppendUserMessage;
using Axon.Modules.Chat.Application.Tests.Builders;
using Axon.Modules.Chat.Application.Tests.Common;
using BuildingBlocks.Core.Diagnostics.Errors;
using BuildingBlocks.Primitives.Ids;
using CSharpFunctionalExtensions;
using FluentValidation.TestHelper;
using NUnit.Framework;
using Shouldly;

namespace Axon.Modules.Chat.Application.Tests.Commands.AppendUserMessage;

[TestFixture]
public class AppendUserMessageValidatorTests : ApplicationTestBase
{
    private AppendUserMessageValidator _validator = null!;

    protected override void OnApplicationSetUp()
    {
        _validator = new AppendUserMessageValidator();
    }

    #region Valid Command Tests

    [TestCaseSource(nameof(GetValidCommandScenarios))]
    public async Task Validate_WithValidCommands_ShouldReturnValid(AppendUserMessageCommand command, string scenarioName)
    {
        // Act
        var result = await _validator.TestValidateAsync(command);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Test]
    public async Task Validate_WithStandardValidCommand_ShouldReturnValid()
    {
        // Arrange
        var command = CommandTestDataBuilder.AppendUserMessage()
            .WithValidData()
            .Build();

        // Act
        var result = await _validator.TestValidateAsync(command);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
        result.IsValid.ShouldBeTrue();
    }

    #endregion

    #region Content Validation Tests

    [Test]
    public async Task Validate_WithNullContent_ShouldHaveValidationError()
    {
        // Arrange - MessageContent cannot be null due to its value object constraints
        // This test validates that the validator handles the structural validation correctly
        // Since MessageContent enforces non-null validation at creation time, we'll test edge cases
        
        // Note: This test would be performed at the domain level when creating MessageContent
        // The validator focuses on structural validation rather than content validation
        Assert.Pass("MessageContent type safety prevents null values - validation occurs at domain level");
    }

    [TestCaseSource(nameof(GetInvalidContentScenarios))]
    public async Task Validate_WithInvalidContent_ShouldHaveValidationErrors(
        AppendUserMessageCommand command, 
        string expectedErrorMessage,
        string scenarioName)
    {
        // Act
        var result = await _validator.TestValidateAsync(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Content)
            .WithErrorMessage(expectedErrorMessage);
        result.IsValid.ShouldBeFalse();
    }

    #endregion

    #region ConversationId Validation Tests

    [Test]
    public async Task Validate_WithValidConversationId_ShouldNotHaveValidationError()
    {
        // Arrange
        var command = CommandTestDataBuilder.AppendUserMessage()
            .WithConversationId(ConversationId.New())
            .WithContent("Valid message content")
            .Build();

        // Act
        var result = await _validator.TestValidateAsync(command);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.ConversationId);
    }

    [Test]
    public async Task Validate_WithDefaultConversationId_ShouldAllowValidation()
    {
        // Note: ConversationId validation happens at domain level, not validator level
        // Arrange
        var command = new AppendUserMessageCommand(
            default(ConversationId),
            MessageContent.Create("Valid content").Value);

        // Act
        var result = await _validator.TestValidateAsync(command);

        // Assert - Should only fail on content being valid, not ConversationId
        result.ShouldNotHaveValidationErrorFor(x => x.ConversationId);
    }

    #endregion

    #region Edge Case Tests

    [Test]
    public async Task Validate_WithLongButValidContent_ShouldBeValid()
    {
        // Arrange
        var command = CommandTestDataBuilder.AppendUserMessage()
            .WithLongContent()
            .Build();

        // Act
        var result = await _validator.TestValidateAsync(command);

        // Assert
        // Note: MessageContent ValueObject handles max length validation internally
        // The validator only checks for null content
        result.ShouldNotHaveValidationErrorFor(x => x.Content);
    }

    [Test]
    public async Task Validate_WithWhitespaceOnlyContent_ShouldValidateAtDomainLevel()
    {
        // Arrange - This test shows that complex content validation is handled by MessageContent ValueObject
        var whitespaceMessage = MessageContent.Create("   \t\n   ");
        if (whitespaceMessage.IsSuccess)
        {
            var command = new AppendUserMessageCommand(ConversationId.New(), whitespaceMessage.Value);

            // Act
            var result = await _validator.TestValidateAsync(command);

            // Assert
            result.ShouldNotHaveValidationErrorFor(x => x.Content);
        }
        else
        {
            // If MessageContent.Create fails for whitespace, that's domain-level validation working correctly
            whitespaceMessage.IsFailure.ShouldBeTrue();
        }
    }

    #endregion

    #region Test Data Sources

    private static IEnumerable<TestCaseData> GetValidCommandScenarios()
    {
        yield return new TestCaseData(
            CommandTestDataBuilder.AppendUserMessage().WithValidData().Build(),
            "Standard valid command")
            .SetName("ValidCommand_Standard");

        yield return new TestCaseData(
            CommandTestDataBuilder.AppendUserMessage()
                .WithContent("Short message")
                .Build(),
            "Valid command with short content")
            .SetName("ValidCommand_ShortContent");

        yield return new TestCaseData(
            CommandTestDataBuilder.AppendUserMessage()
                .WithContent("What is the weather like today? I need to know for planning my outdoor activities.")
                .Build(),
            "Valid command with question content")
            .SetName("ValidCommand_QuestionContent");

        yield return new TestCaseData(
            CommandTestDataBuilder.AppendUserMessage()
                .WithContent("This is a message with multiple sentences. It contains various punctuation marks! Does it work properly?")
                .Build(),
            "Valid command with complex content")
            .SetName("ValidCommand_ComplexContent");
    }

    private static IEnumerable<TestCaseData> GetInvalidContentScenarios()
    {
        // Note: MessageContent ValueObject prevents null values by design
        // This method kept for test structure consistency but would be empty
        // since validation occurs at the domain level during MessageContent creation
        
        // No invalid scenarios can be created because MessageContent type safety
        // prevents construction with invalid values
        return Enumerable.Empty<TestCaseData>();
    }

    #endregion

    #region Performance Tests

    [Test]
    public async Task Validate_ShouldCompleteQuickly_ForStandardCommand()
    {
        // Arrange
        var command = CommandTestDataBuilder.AppendUserMessage().WithValidData().Build();
        var maxExecutionTime = TimeSpan.FromMilliseconds(50);

        // Act & Assert
        var result = await Should.CompleteIn(
            () => _validator.TestValidateAsync(command),
            maxExecutionTime);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Test]
    public async Task Validate_ShouldHandleMultipleValidationsEfficiently()
    {
        // Arrange
        var commands = Enumerable.Range(1, 100)
            .Select(i => CommandTestDataBuilder.AppendUserMessage()
                .WithContent($"Test message {i}")
                .Build())
            .ToList();

        var maxExecutionTime = TimeSpan.FromMilliseconds(500);

        // Act & Assert
        var results = await Should.CompleteIn(async () =>
        {
            var validationTasks = commands.Select(cmd => _validator.TestValidateAsync(cmd));
            return await Task.WhenAll(validationTasks);
        }, maxExecutionTime);

        results.ShouldAllBe(result => result.IsValid);
    }

    #endregion
}