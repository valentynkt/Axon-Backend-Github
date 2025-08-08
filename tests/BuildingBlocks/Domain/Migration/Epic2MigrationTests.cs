using Xunit;
using FluentAssertions;
using BuildingBlocks.Core.Domain.Migration;
using BuildingBlocks.Core.Domain.Rules;
using BuildingBlocks.Core.Domain.Examples;
using BuildingBlocks.Core.Functional;
using BuildingBlocks.Core.Functional.Results;
using BuildingBlocks.Core.Functional.Validation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using BuildingBlocks.Application.Configuration;
using MediatR;
using Axon.Modules.Chat.Domain.Conversation;
using Axon.Modules.Chat.Domain.Conversation.ValueObjects;
using Axon.Modules.Chat.Domain.Rules;

namespace Tests.BuildingBlocks.Domain.Migration;

/// <summary>
/// Comprehensive tests for Epic 2 migration paths and integration.
/// Validates that migration from old patterns to Epic 2 patterns works correctly.
/// </summary>
public class Epic2MigrationTests
{
    #region Backward Compatibility Tests

    [Fact]
    public void BackwardCompatibilityAdapter_Should_Work_With_Existing_Aggregates()
    {
        // Arrange - simulate old aggregate using adapter
        var oldAggregate = new TestAggregateUsingAdapter();

        // Act - use old method names
        oldAggregate.TestAddDomainEvent(new TestDomainEvent());
        var events = oldAggregate.TestGetDomainEvents();

        // Assert - should work without issues
        events.Should().HaveCount(1);
        events.First().Should().BeOfType<TestDomainEvent>();
    }

    [Fact]
    public void ResultMigrationExtensions_Should_Convert_Between_Patterns()
    {
        // Arrange - old Result pattern
        var oldResult = Result<string>.Success("test value");
        
        // Act - convert to new Validation pattern
        var validation = oldResult.ToValidation();
        
        // Assert - conversion should work
        validation.IsValid.Should().BeTrue();
        validation.Value.Should().Be("test value");
        
        // Act - convert back to Result
        var newResult = validation.ToLegacyResult();
        
        // Assert - round-trip should work
        newResult.IsSuccess.Should().BeTrue();
        newResult.Value.Should().Be("test value");
    }

    [Fact]
    public void LegacyCommandAdapter_Should_Bridge_Old_And_New_Patterns()
    {
        // Arrange
        var legacyCommand = new TestLegacyCommand { TestProperty = "test" };
        
        // Act - validate using new domain validation pattern
        var domainValidation = legacyCommand.ValidateDomainRules();
        
        // Assert
        domainValidation.IsValid.Should().BeTrue();
        legacyCommand.GetAggregateType().Should().Be(typeof(object)); // Default
    }

    #endregion

    #region Business Rules Migration Tests

    [Fact]
    public void BusinessRuleMigrationHelper_Should_Create_Rules_From_Conditions()
    {
        // Arrange
        var testValue = 5;
        
        // Act - create business rule from condition (old pattern)
        var rule = BusinessRuleMigrationHelper.CreateRule(
            "TEST_RULE", 
            "Test value must be positive", 
            () => testValue > 0);
        
        // Assert
        rule.Code.Should().Be("TEST_RULE");
        rule.Message.Should().Be("Test value must be positive");
        rule.IsBroken().Should().BeFalse();
    }

    [Fact]
    public void RuleBuilder_Should_Work_With_Multiple_Conditions()
    {
        // Arrange
        var testString = "valid test";
        var testNumber = 42;
        
        // Act - use Epic 2 RuleBuilder pattern
        var rules = new RuleBuilder()
            .NotEmpty(testString, nameof(testString))
            .Must(testNumber > 0, "POSITIVE_NUMBER", "Number must be positive")
            .Must(testString.Length < 100, "STRING_LENGTH", "String too long")
            .Build();
        
        // Assert
        rules.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void RuleBuilder_Should_Fail_With_Multiple_Errors()
    {
        // Arrange
        var emptyString = "";
        var negativeNumber = -5;
        
        // Act
        var rules = new RuleBuilder()
            .NotEmpty(emptyString, nameof(emptyString))
            .Must(negativeNumber > 0, "POSITIVE_NUMBER", "Number must be positive")
            .Build();
        
        // Assert
        rules.IsFailure.Should().BeTrue();
        rules.Error.Should().BeOfType<Error.Multiple>();
        
        var multipleError = (Error.Multiple)rules.Error;
        multipleError.Errors.Should().HaveCount(2);
    }

    #endregion

    #region Chat Domain Migration Tests

    [Fact]
    public void Conversation_Should_Use_Epic2_Patterns_For_Creation()
    {
        // Arrange
        var userId = "test-user-123";
        var title = "Test Conversation";
        
        // Act - create using Epic 2 factory pattern
        var conversationResult = Conversation.Start(title, userId);
        
        // Assert
        conversationResult.IsSuccess.Should().BeTrue();
        
        var conversation = conversationResult.Value;
        conversation.Title.Should().Be(title);
        conversation.OwnerId.Value.Should().Be(userId);
        conversation.Status.Should().Be(ConversationStatus.Active);
        
        // Verify domain event was raised
        var events = conversation.GetUncommittedEvents();
        events.Should().HaveCount(1);
        events.First().Should().BeOfType<ConversationStartedDomainEvent>();
    }

    [Fact]
    public void Conversation_Should_Validate_Business_Rules_Before_Adding_Message()
    {
        // Arrange
        var conversation = CreateTestConversation();
        var userId = conversation.OwnerId.Value;
        var longMessage = new string('a', MessageContent.MaxLength + 1);
        
        // Act - try to add message that violates business rules
        var result = conversation.AppendUserMessage(longMessage, userId);
        
        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Contain("MESSAGE_CONTENT_TOO_LONG");
    }

    [Fact]
    public void Conversation_Should_Prevent_Non_Owner_From_Adding_Messages()
    {
        // Arrange
        var conversation = CreateTestConversation();
        var differentUserId = "different-user-123";
        
        // Act - try to add message from different user
        var result = conversation.AppendUserMessage("Test message", differentUserId);
        
        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Contain("CONVERSATION_ACCESS_DENIED");
    }

    [Fact]
    public void MessageContent_Should_Validate_And_Sanitize_Content()
    {
        // Arrange
        var contentWithExtraSpaces = "  Test   message   with   extra   spaces  ";
        
        // Act - create message content using Epic 2 value object
        var result = MessageContent.Create(contentWithExtraSpaces);
        
        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Value.Should().Be("Test message with extra spaces");
        result.Value.Length.Should().Be(32);
    }

    [Fact]
    public void MessageContent_Should_Detect_Suspicious_Content()
    {
        // Arrange
        var suspiciousContent = new string('a', 1000); // Excessive repetition
        
        // Act
        var result = MessageContent.Create(suspiciousContent);
        
        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Contain("MESSAGE_CONTENT_SUSPICIOUS");
    }

    [Fact]
    public void MessageContent_Should_Provide_Preview_And_Metrics()
    {
        // Arrange
        var longContent = "This is a test message that is longer than the preview length to test the preview functionality.";
        var messageContent = MessageContent.Create(longContent).Value;
        
        // Act
        var preview = messageContent.GetPreview(50);
        var metrics = messageContent.GetMetrics();
        
        // Assert
        preview.Should().EndWith("...");
        preview.Length.Should().BeLessThan(longContent.Length);
        
        metrics.WordCount.Should().BeGreaterThan(0);
        metrics.CharacterCount.Should().Be(longContent.Length);
        metrics.LineCount.Should().Be(1);
    }

    #endregion

    #region Business Rules Validation Tests

    [Fact]
    public void ConversationBusinessRules_Should_Enforce_Owner_Requirement()
    {
        // Arrange
        var rule = new ConversationMustHaveOwnerRule(null);
        
        // Act & Assert
        rule.IsBroken().Should().BeTrue();
        rule.Code.Should().Be("CONVERSATION_MUST_HAVE_OWNER");
    }

    [Fact]
    public void ConversationBusinessRules_Should_Enforce_Message_Limit()
    {
        // Arrange
        var rule = new ConversationMessageLimitRule(currentMessageCount: 5000, maxMessages: 1000);
        
        // Act & Assert
        rule.IsBroken().Should().BeTrue();
        rule.Code.Should().Be("CONVERSATION_MESSAGE_LIMIT_EXCEEDED");
    }

    [Fact]
    public void ConversationBusinessRules_Should_Validate_Title()
    {
        // Arrange - title too long
        var longTitle = new string('a', 250);
        var rule = new ConversationTitleValidationRule(longTitle);
        
        // Act & Assert
        rule.IsBroken().Should().BeTrue();
        rule.Code.Should().Be("CONVERSATION_INVALID_TITLE");
    }

    [Fact]
    public void ConversationBusinessRules_Should_Enforce_Ownership_Access()
    {
        // Arrange
        var ownerId = UserId.Create("owner-123").Value;
        var requesterId = UserId.Create("requester-456").Value;
        var rule = new ConversationOwnershipRule(ownerId, requesterId);
        
        // Act & Assert
        rule.IsBroken().Should().BeTrue();
        rule.Code.Should().Be("CONVERSATION_ACCESS_DENIED");
    }

    [Fact]
    public void DuplicateMessagePreventionRule_Should_Detect_Duplicates()
    {
        // Arrange
        var existingMessage = CreateTestMessage("Test message");
        var existingMessages = new List<Message> { existingMessage };
        var rule = new DuplicateMessagePreventionRule(existingMessages, "Test message");
        
        // Act & Assert
        rule.IsBroken().Should().BeTrue();
        rule.Code.Should().Be("DUPLICATE_MESSAGE_DETECTED");
    }

    #endregion

    #region Migration Checklist Tests

    [Fact]
    public void MigrationChecklist_Should_Track_Migration_Progress()
    {
        // Act
        MigrationChecklistHelper.MarkAsInProgress<TestAggregate>();
        MigrationChecklistHelper.MarkAsMigrated<TestValueObject>();
        
        // Assert
        MigrationChecklistHelper.IsMigrated<TestAggregate>().Should().BeFalse();
        MigrationChecklistHelper.IsMigrated<TestValueObject>().Should().BeTrue();
        
        var report = MigrationChecklistHelper.GetMigrationReport();
        report.Should().ContainKey("TestAggregate");
        report.Should().ContainKey("TestValueObject");
        report["TestAggregate"].Should().Be(MigrationStatus.InProgress);
        report["TestValueObject"].Should().Be(MigrationStatus.Completed);
    }

    [Fact]
    public void MigrationChecklist_Should_Validate_Critical_Types()
    {
        // Arrange
        MigrationChecklistHelper.MarkAsMigrated<TestAggregate>();
        
        // Act & Assert
        var action = () => MigrationChecklistHelper.ValidateCriticalMigration(typeof(TestAggregate), typeof(TestValueObject));
        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*TestValueObject*");
    }

    #endregion

    #region Integration Tests

    [Fact]
    public async Task Epic2Epic5Pipeline_Should_Process_Commands_With_All_Behaviors()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddEpic2Epic5Pipeline();
        services.AddEpic2DomainServices();
        
        // Add test handlers and validators
        services.AddScoped<IRequestHandler<TestDomainCommand, Result<Unit>>, TestDomainCommandHandler>();
        services.AddScoped<IValidator<TestDomainCommand>, TestDomainCommandValidator>();
        
        var serviceProvider = services.BuildServiceProvider();
        var mediator = serviceProvider.GetRequiredService<IMediator>();
        
        // Act
        var command = new TestDomainCommand { TestValue = "valid" };
        var result = await mediator.Send(command);
        
        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    #endregion

    #region Helper Methods and Classes

    private Conversation CreateTestConversation()
    {
        return Conversation.Start("Test Conversation", "test-user-123").Value;
    }

    private Message CreateTestMessage(string content)
    {
        var messageContent = MessageContent.Create(content).Value;
        return Message.Create(
            messageContent,
            ConversationId.New(),
            MessageRole.User,
            1
        ).Value;
    }

    // Test classes for migration testing
    private class TestAggregateUsingAdapter : BaseAggregateAdapter<TestId>
    {
        public void TestAddDomainEvent(IDomainEvent domainEvent)
        {
            AddDomainEvent(domainEvent);
        }

        public IReadOnlyList<IDomainEvent> TestGetDomainEvents()
        {
            return GetDomainEvents();
        }

        protected override IEnumerable<IBusinessRule> GetInvariants()
        {
            yield break;
        }
    }

    private record TestId : IStrongId<Guid>
    {
        public Guid Value { get; }
        
        private TestId(Guid value)
        {
            Value = value;
        }
        
        public static TestId New() => new(Guid.NewGuid());
        public object GetValue() => Value;
        public Type GetValueType() => typeof(Guid);
        
        public static implicit operator Guid(TestId id) => id.Value;
        public static implicit operator TestId(Guid value) => new(value);
    }

    private record TestDomainEvent : DomainEvent;

    private record TestLegacyCommand : LegacyCommandAdapter<Unit>
    {
        public string TestProperty { get; init; } = string.Empty;

        public override Result ValidateLegacy()
        {
            return string.IsNullOrEmpty(TestProperty) 
                ? Result.Failure(Error.Validation("TestProperty is required"))
                : Result.Success();
        }
    }

    private record TestDomainCommand : DomainCommandBase
    {
        public string TestValue { get; init; } = string.Empty;

        public override Type GetAggregateType() => typeof(TestAggregate);

        public override Validation<Unit> ValidateDomainRules()
        {
            var rules = new RuleBuilder()
                .NotEmpty(TestValue, nameof(TestValue))
                .Build();

            return rules.IsSuccess 
                ? Validation<Unit>.Valid(Unit.Value)
                : Validation<Unit>.Invalid(rules.Error);
        }
    }

    private class TestDomainCommandValidator : AbstractValidator<TestDomainCommand>
    {
        public TestDomainCommandValidator()
        {
            RuleFor(x => x.TestValue).NotEmpty();
        }
    }

    private class TestDomainCommandHandler : IRequestHandler<TestDomainCommand, Result<Unit>>
    {
        public Task<Result<Unit>> Handle(TestDomainCommand request, CancellationToken cancellationToken)
        {
            return Task.FromResult(Result<Unit>.Success(Unit.Value));
        }
    }

    private class TestAggregate
    {
        // Test aggregate for migration tracking
    }

    private class TestValueObject
    {
        // Test value object for migration tracking
    }

    #endregion
}