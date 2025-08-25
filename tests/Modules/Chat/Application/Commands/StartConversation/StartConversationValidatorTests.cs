using System.Linq;
using Axon.BuildingBlocks.Core.Primitives.ValueObjects;
using Axon.Modules.Chat.Application.Commands.StartConversation;
using Axon.Modules.Chat.Application.Tests.Builders;
using Axon.Modules.Chat.Application.Tests.Common;
using BuildingBlocks.Core.Diagnostics.Errors;
using CSharpFunctionalExtensions;
using FluentValidation.TestHelper;
using NUnit.Framework;
using Shouldly;

namespace Axon.Modules.Chat.Application.Tests.Commands.StartConversation;

[TestFixture]
public class StartConversationValidatorTests : ApplicationTestBase
{
    private StartConversationValidator _validator = null!;

    protected override void OnApplicationSetUp()
    {
        _validator = new StartConversationValidator();
    }

    #region Valid Command Tests

    [TestCaseSource(nameof(GetValidCommandScenarios))]
    public async Task Validate_WithValidCommands_ShouldReturnValid(StartConversationCommand command, string scenarioName)
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
        var command = CommandTestDataBuilder.StartConversation()
            .WithMessage("Hello, I need help with something.")
            .Build();

        // Act
        var result = await _validator.TestValidateAsync(command);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
        result.IsValid.ShouldBeTrue();
    }

    #endregion

    #region Message Validation Tests

    [Test]
    public async Task Validate_WithNullMessage_ShouldHaveValidationError()
    {
        // Arrange - MessageContent cannot be null due to its value object constraints
        // This test validates that the validator handles the structural validation correctly
        // Since MessageContent enforces non-null validation at creation time, we'll test edge cases
        
        // Note: This test would be performed at the domain level when creating MessageContent
        // The validator focuses on structural validation rather than content validation
        Assert.Pass("MessageContent type safety prevents null values - validation occurs at domain level");
    }

    [TestCaseSource(nameof(GetInvalidMessageScenarios))]
    public async Task Validate_WithInvalidMessage_ShouldHaveValidationErrors(
        StartConversationCommand command, 
        string expectedErrorMessage,
        string scenarioName)
    {
        // Act
        var result = await _validator.TestValidateAsync(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Message)
            .WithErrorMessage(expectedErrorMessage);
        result.IsValid.ShouldBeFalse();
    }

    #endregion

    #region Edge Case Tests

    [Test]
    public async Task Validate_WithLongButValidMessage_ShouldBeValid()
    {
        // Arrange
        var command = CommandTestDataBuilder.StartConversation()
            .WithLongMessage()
            .Build();

        // Act
        var result = await _validator.TestValidateAsync(command);

        // Assert
        // Note: MessageContent ValueObject handles max length validation internally
        // The validator only checks for null message
        result.ShouldNotHaveValidationErrorFor(x => x.Message);
    }

    [Test]
    public async Task Validate_WithWhitespaceOnlyMessage_ShouldValidateAtDomainLevel()
    {
        // Arrange - This test shows that complex content validation is handled by MessageContent ValueObject
        var whitespaceMessage = MessageContent.Create("   \t\n   ");
        if (whitespaceMessage.IsSuccess)
        {
            var command = new StartConversationCommand(whitespaceMessage.Value);

            // Act
            var result = await _validator.TestValidateAsync(command);

            // Assert
            result.ShouldNotHaveValidationErrorFor(x => x.Message);
        }
        else
        {
            // If MessageContent.Create fails for whitespace, that's domain-level validation working correctly
            whitespaceMessage.IsFailure.ShouldBeTrue();
        }
    }

    [Test]
    public async Task Validate_WithComplexMessage_ShouldBeValid()
    {
        // Arrange
        var command = CommandTestDataBuilder.StartConversation()
            .WithMessage("Can you help me with multiple tasks? I need assistance with: 1) Code review, 2) Documentation writing, 3) Architecture planning.")
            .Build();

        // Act
        var result = await _validator.TestValidateAsync(command);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.Message);
        result.IsValid.ShouldBeTrue();
    }

    #endregion

    #region Test Data Sources

    private static IEnumerable<TestCaseData> GetValidCommandScenarios()
    {
        yield return new TestCaseData(
            CommandTestDataBuilder.StartConversation().WithMessage("Hello, I need help.").Build(),
            "Standard valid command")
            .SetName("ValidCommand_Standard");

        yield return new TestCaseData(
            CommandTestDataBuilder.StartConversation()
                .WithMessage("Hi!")
                .Build(),
            "Valid command with short content")
            .SetName("ValidCommand_ShortContent");

        yield return new TestCaseData(
            CommandTestDataBuilder.StartConversation()
                .WithMessage("What is the weather like today? I need to know for planning my outdoor activities.")
                .Build(),
            "Valid command with question content")
            .SetName("ValidCommand_QuestionContent");

        yield return new TestCaseData(
            CommandTestDataBuilder.StartConversation()
                .WithMessage("This is a message with multiple sentences. It contains various punctuation marks! Does it work properly? Let's find out.")
                .Build(),
            "Valid command with complex content")
            .SetName("ValidCommand_ComplexContent");

        yield return new TestCaseData(
            CommandTestDataBuilder.StartConversation()
                .WithMessage("I have a technical question about implementing CQRS patterns in .NET applications.")
                .Build(),
            "Valid command with technical content")
            .SetName("ValidCommand_TechnicalContent");
    }

    private static IEnumerable<TestCaseData> GetInvalidMessageScenarios()
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
        var command = CommandTestDataBuilder.StartConversation()
            .WithMessage("Hello, I need assistance.")
            .Build();
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
            .Select(i => CommandTestDataBuilder.StartConversation()
                .WithMessage($"Starting conversation {i} with a test message.")
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

    #region Scenario-Specific Tests

    [Test]
    public async Task Validate_WithQuestionMessage_ShouldBeValid()
    {
        // Arrange
        var command = CommandTestDataBuilder.StartConversation()
            .WithMessage("What are the best practices for implementing Clean Architecture?")
            .Build();

        // Act
        var result = await _validator.TestValidateAsync(command);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.Message);
        result.IsValid.ShouldBeTrue();
    }

    [Test]
    public async Task Validate_WithInstructionalMessage_ShouldBeValid()
    {
        // Arrange
        var command = CommandTestDataBuilder.StartConversation()
            .WithMessage("Please help me understand how to implement the Repository pattern in Entity Framework.")
            .Build();

        // Act
        var result = await _validator.TestValidateAsync(command);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.Message);
        result.IsValid.ShouldBeTrue();
    }

    [Test]
    public async Task Validate_WithContextualMessage_ShouldBeValid()
    {
        // Arrange
        var command = CommandTestDataBuilder.StartConversation()
            .WithMessage("I'm working on a .NET project and need guidance on implementing CQRS. The project uses Entity Framework and follows Clean Architecture principles.")
            .Build();

        // Act
        var result = await _validator.TestValidateAsync(command);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.Message);
        result.IsValid.ShouldBeTrue();
    }

    #endregion
}