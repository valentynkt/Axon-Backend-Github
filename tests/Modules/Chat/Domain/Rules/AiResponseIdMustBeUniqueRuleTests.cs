using Axon.Modules.Chat.Domain.Tests.Common;
using Axon.Modules.Chat.Domain.Tests.Builders;

namespace Axon.Modules.Chat.Domain.Tests.Rules;

/// <summary>
/// Tests for AiResponseIdMustBeUniqueRule that validates AI Response ID uniqueness within a conversation.
/// Tests uniqueness constraints, duplicate detection, and edge cases.
/// </summary>
[TestFixture]
public class AiResponseIdMustBeUniqueRuleTests : DomainTestBase
{
    private AiResponseId _testAiResponseId;
    private AiResponseId _alternativeAiResponseId;

    protected override void OnSetUp()
    {
        _testAiResponseId = TestConstants.AiResponses.DefaultAiResponseId;
        _alternativeAiResponseId = TestConstants.AiResponses.AlternativeAiResponseId;
    }

    [TestFixture]
    public class IsBrokenTests : AiResponseIdMustBeUniqueRuleTests
    {
        [Test]
        public void IsBroken_WithEmptyMessageList_ShouldReturnFalse()
        {
            // Arrange
            var emptyMessages = new List<Message>();
            var rule = new AiResponseIdMustBeUniqueRule(_testAiResponseId, emptyMessages);

            // Act
            var result = rule.IsBroken();

            // Assert
            result.ShouldBeFalse("Rule should not be broken when there are no existing messages");
        }

        [Test]
        public void IsBroken_WithOnlyUserMessages_ShouldReturnFalse()
        {
            // Arrange
            var userMessage = MessageBuilder.New()
                .AsUserMessage()
                .WithContent(TestConstants.Messages.DefaultUserMessage)
                .Build();
                
            var messages = new List<Message> { userMessage };
            var rule = new AiResponseIdMustBeUniqueRule(_testAiResponseId, messages);

            // Act
            var result = rule.IsBroken();

            // Assert
            result.ShouldBeFalse("Rule should not be broken when there are only user messages");
        }

        [Test]
        public void IsBroken_WithDifferentAiResponseIds_ShouldReturnFalse()
        {
            // Arrange
            var assistantMessage = MessageBuilder.New()
                .AsAssistantMessage()
                .WithAiResponseId(_alternativeAiResponseId)
                .WithContent(TestConstants.Messages.DefaultAssistantMessage)
                .Build();
                
            var messages = new List<Message> { assistantMessage };
            var rule = new AiResponseIdMustBeUniqueRule(_testAiResponseId, messages);

            // Act
            var result = rule.IsBroken();

            // Assert
            result.ShouldBeFalse("Rule should not be broken when AI response IDs are different");
        }

        [Test]
        public void IsBroken_WithSameAiResponseId_ShouldReturnTrue()
        {
            // Arrange
            var assistantMessage = MessageBuilder.New()
                .AsAssistantMessage()
                .WithAiResponseId(_testAiResponseId)
                .WithContent(TestConstants.Messages.DefaultAssistantMessage)
                .Build();
                
            var messages = new List<Message> { assistantMessage };
            var rule = new AiResponseIdMustBeUniqueRule(_testAiResponseId, messages);

            // Act
            var result = rule.IsBroken();

            // Assert
            result.ShouldBeTrue("Rule should be broken when AI response ID already exists");
        }

        [Test]
        public void IsBroken_WithMultipleMessagesIncludingDuplicate_ShouldReturnTrue()
        {
            // Arrange
            var userMessage = MessageBuilder.New()
                .AsUserMessage()
                .WithContent("User message")
                .Build();
                
            var assistantMessage1 = MessageBuilder.New()
                .AsAssistantMessage()
                .WithAiResponseId(_alternativeAiResponseId)
                .WithContent("First assistant response")
                .Build();
                
            var assistantMessage2 = MessageBuilder.New()
                .AsAssistantMessage()
                .WithAiResponseId(_testAiResponseId)
                .WithContent("Second assistant response")
                .Build();
                
            var messages = new List<Message> { userMessage, assistantMessage1, assistantMessage2 };
            var rule = new AiResponseIdMustBeUniqueRule(_testAiResponseId, messages);

            // Act
            var result = rule.IsBroken();

            // Assert
            result.ShouldBeTrue("Rule should be broken when AI response ID exists among multiple messages");
        }

        [Test]
        public void IsBroken_WithMultipleUniqueAiResponseIds_ShouldReturnFalse()
        {
            // Arrange
            var assistantMessage1 = MessageBuilder.New()
                .AsAssistantMessage()
                .WithAiResponseId(_alternativeAiResponseId)
                .WithContent("First assistant response")
                .Build();
                
            var assistantMessage2 = MessageBuilder.New()
                .AsAssistantMessage()
                .WithAiResponseId(TestConstants.AiResponses.ThirdAiResponseId)
                .WithContent("Second assistant response")
                .Build();
                
            var messages = new List<Message> { assistantMessage1, assistantMessage2 };
            var rule = new AiResponseIdMustBeUniqueRule(_testAiResponseId, messages);

            // Act
            var result = rule.IsBroken();

            // Assert
            result.ShouldBeFalse("Rule should not be broken when all AI response IDs are unique");
        }
    }

    [TestFixture]
    public class IsBrokenAsyncTests : AiResponseIdMustBeUniqueRuleTests
    {
        [Test]
        public async Task IsBrokenAsync_ShouldReturnSameResultAsIsBroken()
        {
            // Arrange
            var assistantMessage = MessageBuilder.New()
                .AsAssistantMessage()
                .WithAiResponseId(_testAiResponseId)
                .Build();
                
            var messages = new List<Message> { assistantMessage };
            var rule = new AiResponseIdMustBeUniqueRule(_testAiResponseId, messages);

            // Act
            var syncResult = rule.IsBroken();
            var asyncResult = await rule.IsBrokenAsync();

            // Assert
            asyncResult.ShouldBe(syncResult, "IsBrokenAsync should return same result as IsBroken");
        }

        [Test]
        public async Task IsBrokenAsync_WithCancellationToken_ShouldComplete()
        {
            // Arrange
            var emptyMessages = new List<Message>();
            var rule = new AiResponseIdMustBeUniqueRule(_testAiResponseId, emptyMessages);
            using var cts = new CancellationTokenSource();

            // Act & Assert
            var result = await rule.IsBrokenAsync(cts.Token);
            result.ShouldBeFalse("Rule should not be broken with empty message list");
        }
    }

    [TestFixture]
    public class BusinessRulePropertiesTests : AiResponseIdMustBeUniqueRuleTests
    {
        [Test]
        public void Code_ShouldHaveExpectedValue()
        {
            // Arrange
            var emptyMessages = new List<Message>();
            var rule = new AiResponseIdMustBeUniqueRule(_testAiResponseId, emptyMessages);

            // Act & Assert
            rule.Code.ShouldBe("CHAT.AI.RESPONSE.ID.DUPLICATE");
        }

        [Test]
        public void Message_ShouldHaveExpectedValue()
        {
            // Arrange
            var emptyMessages = new List<Message>();
            var rule = new AiResponseIdMustBeUniqueRule(_testAiResponseId, emptyMessages);

            // Act & Assert
            rule.Message.ShouldBe("AI Response ID must be unique within the conversation to prevent duplicate responses.");
        }
    }

    [TestFixture]
    public class ConstructorTests : AiResponseIdMustBeUniqueRuleTests
    {
        [Test]
        public void Constructor_WithNullExistingMessages_ShouldThrowArgumentNullException()
        {
            // Act & Assert
            Should.Throw<ArgumentNullException>(() => 
                new AiResponseIdMustBeUniqueRule(_testAiResponseId, null!))
                .ParamName.ShouldBe("existingMessages");
        }

        [Test]
        public void Constructor_WithValidParameters_ShouldCreateRule()
        {
            // Arrange
            var messages = new List<Message>();

            // Act
            var rule = new AiResponseIdMustBeUniqueRule(_testAiResponseId, messages);

            // Assert
            rule.ShouldNotBeNull();
            rule.Code.ShouldBe("CHAT.AI.RESPONSE.ID.DUPLICATE");
        }
    }

    [TestFixture]
    public class EdgeCaseTests : AiResponseIdMustBeUniqueRuleTests
    {
        [Test]
        public void Build_WithAssistantMessageHavingNullAiResponseId_ShouldFail()
        {
            // Arrange & Act
            var result = MessageBuilder.New()
                .AsAssistantMessage()
                .WithoutAiResponseId()
                .WithContent(TestConstants.Messages.DefaultAssistantMessage)
                .BuildResult();

            // Assert
            result.IsFailure.ShouldBeTrue("Assistant messages must have an AI response ID");
            result.Error.Message.ShouldContain("Assistant messages must have an AI response ID");
        }

        [Test]
        public void IsBroken_WithMixedUserAndAssistantMessages_ShouldOnlyCheckAssistantMessages()
        {
            // Arrange
            var userMessage = MessageBuilder.New()
                .AsUserMessage()
                .WithContent("User message")
                .Build();
                
            var assistantMessage = MessageBuilder.New()
                .AsAssistantMessage()
                .WithAiResponseId(_testAiResponseId)
                .WithContent("Assistant message")
                .Build();
                
            var messages = new List<Message> { userMessage, assistantMessage };
            var rule = new AiResponseIdMustBeUniqueRule(_testAiResponseId, messages);

            // Act
            var result = rule.IsBroken();

            // Assert
            result.ShouldBeTrue("Rule should be broken when assistant message has duplicate AI response ID, regardless of user messages");
        }
    }

    [TestFixture]
    public class ConsistencyTests : AiResponseIdMustBeUniqueRuleTests
    {
        [Test]
        public void IsBroken_CalledMultipleTimes_ShouldReturnConsistentResults()
        {
            // Arrange
            var assistantMessage = MessageBuilder.New()
                .AsAssistantMessage()
                .WithAiResponseId(_testAiResponseId)
                .Build();
                
            var messages = new List<Message> { assistantMessage };
            var rule = new AiResponseIdMustBeUniqueRule(_testAiResponseId, messages);

            // Act & Assert
            BusinessRuleTestHelpers.AssertRuleIsConsistent(rule);
        }

        [Test]
        public void Rule_ShouldProvideMeaningfulErrorMessage()
        {
            // Arrange
            var assistantMessage = MessageBuilder.New()
                .AsAssistantMessage()
                .WithAiResponseId(_testAiResponseId)
                .Build();
                
            var messages = new List<Message> { assistantMessage };
            var rule = new AiResponseIdMustBeUniqueRule(_testAiResponseId, messages);

            // Act & Assert
            BusinessRuleTestHelpers.AssertRuleHasMeaningfulError(rule);
        }
    }

    [TestFixture]
    public class PerformanceTests : AiResponseIdMustBeUniqueRuleTests
    {
        [Test]
        public void IsBroken_WithLargeNumberOfMessages_ShouldPerformWell()
        {
            // Arrange - Create many assistant messages with unique AI response IDs
            var messages = new List<Message>();
            for (int i = 0; i < 1000; i++)
            {
                var aiResponseId = new AiResponseId($"perf-test-{i}");
                var message = MessageBuilder.New()
                    .AsAssistantMessage()
                    .WithAiResponseId(aiResponseId)
                    .WithContent($"Assistant message {i}")
                    .Build();
                messages.Add(message);
            }
            
            var rule = new AiResponseIdMustBeUniqueRule(_testAiResponseId, messages);

            // Act & Assert - Should complete quickly
            var startTime = DateTime.UtcNow;
            var result = rule.IsBroken();
            var duration = DateTime.UtcNow - startTime;
            
            result.ShouldBeFalse("Rule should not be broken with unique AI response IDs");
            duration.ShouldBeLessThan(TimeSpan.FromSeconds(1), "Rule evaluation should complete quickly with many messages");
        }
    }
}