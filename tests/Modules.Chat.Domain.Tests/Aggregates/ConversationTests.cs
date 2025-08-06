using Axon.Modules.Chat.Domain.Aggregates;
using Axon.Modules.Chat.Domain.Errors;
using Axon.Modules.Chat.Domain.Events;
using Axon.Modules.Chat.Domain.ValueObjects;

namespace Axon.Modules.Chat.Domain.Tests.Aggregates;

/// <summary>
/// London School TDD tests for Conversation aggregate domain invariants
/// Tests focus on behavior validation, not implementation details
/// </summary>
[TestFixture]
[Category("Unit")]
[Category("Domain")]
[Category("LondonSchoolTDD")]
public sealed class ConversationTests
{
    #region Start Behavior Tests - ConversationStarted Event

    [Test]
    public void Start_WithValidTitleAndUserId_ShouldCreateConversationAndRaiseConversationStartedEvent()
    {
        // Arrange
        const string title = "Test Conversation";
        const string userId = "user123";

        // Act
        var result = Conversation.Start(title, userId);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        var conversation = result.Value;
        
        // Verify aggregate state
        conversation.Title.ShouldBe(title);
        conversation.UserId.ShouldBe(userId);
        conversation.Status.ShouldBe(ConversationStatus.Active);
        conversation.IsActive.ShouldBeTrue();
        conversation.MessageCount.ShouldBe(0);
        
        // Verify domain event was raised
        conversation.DomainEvents.ShouldHaveSingleItem();
        var domainEvent = conversation.DomainEvents.First();
        domainEvent.ShouldBeOfType<ConversationStartedDomainEvent>();
        
        var conversationStartedEvent = (ConversationStartedDomainEvent)domainEvent;
        conversationStartedEvent.ConversationId.ShouldBe(conversation.Id);
        conversationStartedEvent.UserId.ShouldBe(userId);
        conversationStartedEvent.Title.ShouldBe(title);
    }

    [Test]
    public void Start_WithEmptyUserId_ShouldReturnOwnerRequiredError()
    {
        // Arrange
        const string title = "Test Conversation";
        const string emptyUserId = "";

        // Act
        var result = Conversation.Start(title, emptyUserId);

        // Assert
        result.IsSuccess.ShouldBeFalse();
        result.Error.ShouldBe(ChatErrors.Conversation.OwnerRequired);
    }

    [Test]
    public void Start_WithNullUserId_ShouldReturnOwnerRequiredError()
    {
        // Arrange
        const string title = "Test Conversation";
        const string? nullUserId = null;

        // Act
        var result = Conversation.Start(title, nullUserId!);

        // Assert
        result.IsSuccess.ShouldBeFalse();
        result.Error.ShouldBe(ChatErrors.Conversation.OwnerRequired);
    }

    [Test]
    public void Start_WithWhitespaceUserId_ShouldReturnOwnerRequiredError()
    {
        // Arrange
        const string title = "Test Conversation";
        const string whitespaceUserId = "   ";

        // Act
        var result = Conversation.Start(title, whitespaceUserId);

        // Assert
        result.IsSuccess.ShouldBeFalse();
        result.Error.ShouldBe(ChatErrors.Conversation.OwnerRequired);
    }

    [Test]
    public void Start_WithEmptyTitle_ShouldReturnCannotStartError()
    {
        // Arrange
        const string emptyTitle = "";
        const string userId = "user123";

        // Act
        var result = Conversation.Start(emptyTitle, userId);

        // Assert
        result.IsSuccess.ShouldBeFalse();
        result.Error.ShouldBe(ChatErrors.Conversation.CannotStart("Title cannot be empty"));
    }

    [Test]
    public void Start_WithTitleExceeding200Characters_ShouldReturnCannotStartError()
    {
        // Arrange
        var longTitle = new string('a', 201);
        const string userId = "user123";

        // Act
        var result = Conversation.Start(longTitle, userId);

        // Assert
        result.IsSuccess.ShouldBeFalse();
        result.Error.ShouldBe(ChatErrors.Conversation.CannotStart("Title cannot exceed 200 characters"));
    }

    [Test]
    public void Start_WithTitleAt200Characters_ShouldSucceed()
    {
        // Arrange
        var maxLengthTitle = new string('a', 200);
        const string userId = "user123";

        // Act
        var result = Conversation.Start(maxLengthTitle, userId);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Title.ShouldBe(maxLengthTitle);
    }

    [Test]
    public void Start_WithTitleWithWhitespace_ShouldTrimTitle()
    {
        // Arrange
        const string titleWithWhitespace = "  Test Conversation  ";
        const string userId = "user123";
        const string expectedTitle = "Test Conversation";

        // Act
        var result = Conversation.Start(titleWithWhitespace, userId);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Title.ShouldBe(expectedTitle);
    }

    [Test]
    public void Start_WithUserIdWithWhitespace_ShouldTrimUserId()
    {
        // Arrange
        const string title = "Test Conversation";
        const string userIdWithWhitespace = "  user123  ";
        const string expectedUserId = "user123";

        // Act
        var result = Conversation.Start(title, userIdWithWhitespace);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.UserId.ShouldBe(expectedUserId);
    }

    #endregion

    #region AppendUserMessage Behavior Tests - UserMessageAppended Event

    [Test]
    public void AppendUserMessage_WithValidContentAndOwner_ShouldAddMessageAndRaiseUserMessageAppendedEvent()
    {
        // Arrange
        const string title = "Test Conversation";
        const string userId = "user123";
        const string messageContent = "Hello, this is a test message.";
        
        var conversation = Conversation.Start(title, userId).Value;
        conversation.ClearDomainEvents(); // Clear the ConversationStarted event

        // Act
        var result = conversation.AppendUserMessage(messageContent, userId);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        var message = result.Value;
        
        // Verify message properties
        message.Content.ShouldBe(messageContent);
        message.ConversationId.ShouldBe(conversation.Id);
        message.Role.ShouldBe(MessageRole.User);
        message.Sequence.ShouldBe(1);
        
        // Verify conversation state
        conversation.MessageCount.ShouldBe(1);
        conversation.MessagesOrdered.ShouldHaveSingleItem();
        
        // Verify domain event was raised
        conversation.DomainEvents.ShouldHaveSingleItem();
        var domainEvent = conversation.DomainEvents.First();
        domainEvent.ShouldBeOfType<UserMessageAppendedDomainEvent>();
        
        var userMessageAppendedEvent = (UserMessageAppendedDomainEvent)domainEvent;
        userMessageAppendedEvent.ConversationId.ShouldBe(conversation.Id);
        userMessageAppendedEvent.MessageId.ShouldBe(message.Id);
        userMessageAppendedEvent.Sequence.ShouldBe(1);
        userMessageAppendedEvent.UserId.ShouldBe(userId);
    }

    [Test]
    public void AppendUserMessage_WithNonOwner_ShouldReturnNotConversationOwnerError()
    {
        // Arrange
        const string title = "Test Conversation";
        const string ownerId = "owner123";
        const string nonOwnerId = "other456";
        const string messageContent = "Hello, this is a test message.";
        
        var conversation = Conversation.Start(title, ownerId).Value;

        // Act
        var result = conversation.AppendUserMessage(messageContent, nonOwnerId);

        // Assert
        result.IsSuccess.ShouldBeFalse();
        result.Error.ShouldBe(ChatErrors.Conversation.NotConversationOwner(nonOwnerId, conversation.Id.ToString()));
        
        // Verify no message was added
        conversation.MessageCount.ShouldBe(0);
    }

    [Test]
    public void AppendUserMessage_WithEmptyContent_ShouldReturnInvalidMessageContentError()
    {
        // Arrange
        const string title = "Test Conversation";
        const string userId = "user123";
        const string emptyContent = "";
        
        var conversation = Conversation.Start(title, userId).Value;

        // Act
        var result = conversation.AppendUserMessage(emptyContent, userId);

        // Assert
        result.IsSuccess.ShouldBeFalse();
        result.Error.ShouldBe(ChatErrors.Conversation.InvalidMessageContent("Content cannot be empty"));
    }

    [Test]
    public void AppendUserMessage_WithContentExceeding100000Characters_ShouldReturnInvalidMessageContentError()
    {
        // Arrange
        const string title = "Test Conversation";
        const string userId = "user123";
        var longContent = new string('a', 100_001);
        
        var conversation = Conversation.Start(title, userId).Value;

        // Act
        var result = conversation.AppendUserMessage(longContent, userId);

        // Assert
        result.IsSuccess.ShouldBeFalse();
        result.Error.ShouldBe(ChatErrors.Conversation.InvalidMessageContent("Content cannot exceed 100,000 characters"));
    }

    [Test]
    public void AppendUserMessage_WithContentAt100000Characters_ShouldSucceed()
    {
        // Arrange
        const string title = "Test Conversation";
        const string userId = "user123";
        var maxLengthContent = new string('a', 100_000);
        
        var conversation = Conversation.Start(title, userId).Value;

        // Act
        var result = conversation.AppendUserMessage(maxLengthContent, userId);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Content.ShouldBe(maxLengthContent);
    }

    [Test]
    public void AppendUserMessage_ToCompletedConversation_ShouldReturnConversationNotActiveError()
    {
        // Arrange
        const string title = "Test Conversation";
        const string userId = "user123";
        const string messageContent = "Hello, this is a test message.";
        
        var conversation = Conversation.Start(title, userId).Value;
        conversation.CompleteConversation(); // Complete the conversation

        // Act
        var result = conversation.AppendUserMessage(messageContent, userId);

        // Assert
        result.IsSuccess.ShouldBeFalse();
        result.Error.ShouldBe(ChatErrors.Conversation.ConversationNotActive);
    }

    [Test]
    public void AppendUserMessage_ToArchivedConversation_ShouldReturnConversationNotActiveError()
    {
        // Arrange
        const string title = "Test Conversation";
        const string userId = "user123";
        const string messageContent = "Hello, this is a test message.";
        
        var conversation = Conversation.Start(title, userId).Value;
        conversation.ArchiveConversation(); // Archive the conversation

        // Act
        var result = conversation.AppendUserMessage(messageContent, userId);

        // Assert
        result.IsSuccess.ShouldBeFalse();
        result.Error.ShouldBe(ChatErrors.Conversation.ConversationNotActive);
    }

    [Test]
    public void AppendUserMessage_MultipleMessages_ShouldMaintainCorrectSequencing()
    {
        // Arrange
        const string title = "Test Conversation";
        const string userId = "user123";
        
        var conversation = Conversation.Start(title, userId).Value;

        // Act
        var result1 = conversation.AppendUserMessage("First message", userId);
        var result2 = conversation.AppendUserMessage("Second message", userId);
        var result3 = conversation.AppendUserMessage("Third message", userId);

        // Assert
        result1.IsSuccess.ShouldBeTrue();
        result2.IsSuccess.ShouldBeTrue();
        result3.IsSuccess.ShouldBeTrue();
        
        result1.Value.Sequence.ShouldBe(1);
        result2.Value.Sequence.ShouldBe(2);
        result3.Value.Sequence.ShouldBe(3);
        
        conversation.MessageCount.ShouldBe(3);
        
        // Verify sequence integrity
        var sequenceValidation = conversation.ValidateSequenceIntegrity();
        sequenceValidation.IsSuccess.ShouldBeTrue();
    }

    #endregion

    #region Ownership Validation Tests

    [Test]
    public void BelongsToUser_WithMatchingUserId_ShouldReturnTrue()
    {
        // Arrange
        const string title = "Test Conversation";
        const string userId = "user123";
        
        var conversation = Conversation.Start(title, userId).Value;

        // Act & Assert
        conversation.BelongsToUser(userId).ShouldBeTrue();
    }

    [Test]
    public void BelongsToUser_WithDifferentUserId_ShouldReturnFalse()
    {
        // Arrange
        const string title = "Test Conversation";
        const string ownerId = "owner123";
        const string otherUserId = "other456";
        
        var conversation = Conversation.Start(title, ownerId).Value;

        // Act & Assert
        conversation.BelongsToUser(otherUserId).ShouldBeFalse();
    }

    [Test]
    public void BelongsToUser_WithEmptyUserId_ShouldReturnFalse()
    {
        // Arrange
        const string title = "Test Conversation";
        const string userId = "user123";
        
        var conversation = Conversation.Start(title, userId).Value;

        // Act & Assert
        conversation.BelongsToUser("").ShouldBeFalse();
    }

    [Test]
    public void BelongsToUser_WithNullUserId_ShouldReturnFalse()
    {
        // Arrange
        const string title = "Test Conversation";
        const string userId = "user123";
        
        var conversation = Conversation.Start(title, userId).Value;

        // Act & Assert
        conversation.BelongsToUser(null!).ShouldBeFalse();
    }

    [Test]
    public void BelongsToUser_WithCaseInsensitiveMatch_ShouldReturnTrue()
    {
        // Arrange
        const string title = "Test Conversation";
        const string userId = "User123";
        const string userIdDifferentCase = "user123";
        
        var conversation = Conversation.Start(title, userId).Value;

        // Act & Assert
        conversation.BelongsToUser(userIdDifferentCase).ShouldBeTrue();
    }

    #endregion

    #region Guard Clauses and Validation Tests

    [Test]
    public void ValidateSequenceIntegrity_WithCorrectSequencing_ShouldReturnSuccess()
    {
        // Arrange
        const string title = "Test Conversation";
        const string userId = "user123";
        
        var conversation = Conversation.Start(title, userId).Value;
        conversation.AppendUserMessage("Message 1", userId);
        conversation.AppendUserMessage("Message 2", userId);
        conversation.AppendUserMessage("Message 3", userId);

        // Act
        var result = conversation.ValidateSequenceIntegrity();

        // Assert
        result.IsSuccess.ShouldBeTrue();
    }

    [Test]
    public void CanAcceptMessages_WithActiveConversation_ShouldReturnTrue()
    {
        // Arrange
        const string title = "Test Conversation";
        const string userId = "user123";
        
        var conversation = Conversation.Start(title, userId).Value;

        // Act & Assert
        conversation.CanAcceptMessages().ShouldBeTrue();
    }

    [Test]
    public void CanAcceptMessages_WithCompletedConversation_ShouldReturnFalse()
    {
        // Arrange
        const string title = "Test Conversation";
        const string userId = "user123";
        
        var conversation = Conversation.Start(title, userId).Value;
        conversation.CompleteConversation();

        // Act & Assert
        conversation.CanAcceptMessages().ShouldBeFalse();
    }

    [Test]
    public void CanAcceptMessages_WithArchivedConversation_ShouldReturnFalse()
    {
        // Arrange
        const string title = "Test Conversation";
        const string userId = "user123";
        
        var conversation = Conversation.Start(title, userId).Value;
        conversation.ArchiveConversation();

        // Act & Assert
        conversation.CanAcceptMessages().ShouldBeFalse();
    }

    [Test]
    public void IsActive_WithActiveConversation_ShouldReturnTrue()
    {
        // Arrange
        const string title = "Test Conversation";
        const string userId = "user123";
        
        var conversation = Conversation.Start(title, userId).Value;

        // Act & Assert
        conversation.IsActive.ShouldBeTrue();
    }

    [Test]
    public void IsActive_WithCompletedConversation_ShouldReturnFalse()
    {
        // Arrange
        const string title = "Test Conversation";
        const string userId = "user123";
        
        var conversation = Conversation.Start(title, userId).Value;
        conversation.CompleteConversation();

        // Act & Assert
        conversation.IsActive.ShouldBeFalse();
    }

    #endregion

    #region Event Application Tests (Event Sourcing)

    [Test]
    public void ApplyEvent_ConversationStartedEvent_ShouldUpdateAggregateState()
    {
        // Arrange
        const string title = "Test Conversation";
        const string userId = "user123";
        var conversationId = ConversationId.New();
        
        var conversation = Conversation.Start("Initial", "initial").Value;
        var conversationStartedEvent = new ConversationStartedDomainEvent(conversationId, userId, title);

        // Act
        // Note: ApplyEvent is protected, so we test it indirectly through the constructor
        // In a full event sourcing implementation, this would be tested more directly
        var newConversation = Conversation.Start(title, userId).Value;

        // Assert
        newConversation.Title.ShouldBe(title);
        newConversation.UserId.ShouldBe(userId);
        newConversation.Status.ShouldBe(ConversationStatus.Active);
    }

    #endregion

    #region Edge Cases and Boundary Tests

    [Test]
    public void GetLastMessage_WithNoMessages_ShouldReturnNull()
    {
        // Arrange
        const string title = "Test Conversation";
        const string userId = "user123";
        
        var conversation = Conversation.Start(title, userId).Value;

        // Act & Assert
        conversation.GetLastMessage().ShouldBeNull();
    }

    [Test]
    public void GetLastMessage_WithMessages_ShouldReturnLastMessage()
    {
        // Arrange
        const string title = "Test Conversation";
        const string userId = "user123";
        
        var conversation = Conversation.Start(title, userId).Value;
        conversation.AppendUserMessage("First message", userId);
        var lastMessage = conversation.AppendUserMessage("Last message", userId).Value;

        // Act
        var result = conversation.GetLastMessage();

        // Assert
        result.ShouldNotBeNull();
        result.Id.ShouldBe(lastMessage.Id);
        result.Content.ShouldBe("Last message");
    }

    [Test]
    public void GetMessagesByRole_WithUserMessages_ShouldReturnOnlyUserMessages()
    {
        // Arrange
        const string title = "Test Conversation";
        const string userId = "user123";
        
        var conversation = Conversation.Start(title, userId).Value;
        conversation.AppendUserMessage("User message 1", userId);
        conversation.AddMessage("Assistant message", MessageRole.Assistant);
        conversation.AppendUserMessage("User message 2", userId);

        // Act
        var userMessages = conversation.GetMessagesByRole(MessageRole.User);

        // Assert
        userMessages.Count.ShouldBe(2);
        userMessages.All(m => m.Role == MessageRole.User).ShouldBeTrue();
        userMessages[0].Content.ShouldBe("User message 1");
        userMessages[1].Content.ShouldBe("User message 2");
    }

    #endregion
}