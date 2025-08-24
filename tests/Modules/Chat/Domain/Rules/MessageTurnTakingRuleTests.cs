using Axon.Modules.Chat.Domain.Tests.Common;
using Axon.Modules.Chat.Domain.Tests.Builders;

namespace Axon.Modules.Chat.Domain.Tests.Rules;

/// <summary>
/// Tests for MessageTurnTakingRule that enforces strict alternation between user and assistant messages.
/// Tests turn-taking patterns, conversation start rules, and alternation violations.
/// </summary>
[TestFixture]
public class MessageTurnTakingRuleTests : DomainTestBase
{
    [TestFixture]
    public class ConversationStartRulesTests : MessageTurnTakingRuleTests
    {
        [Test]
        public void IsBroken_EmptyConversation_WithUserMessage_ShouldReturnFalse()
        {
            // Arrange
            var emptyMessages = new List<Message>();
            var rule = new MessageTurnTakingRule(emptyMessages, MessageRole.User);

            // Act
            var result = rule.IsBroken();

            // Assert
            result.ShouldBeFalse("Rule should not be broken when user starts an empty conversation");
        }

        [Test]
        public void IsBroken_EmptyConversation_WithAssistantMessage_ShouldReturnTrue()
        {
            // Arrange
            var emptyMessages = new List<Message>();
            var rule = new MessageTurnTakingRule(emptyMessages, MessageRole.Assistant);

            // Act
            var result = rule.IsBroken();

            // Assert
            result.ShouldBeTrue("Rule should be broken when assistant tries to start conversation (no assistant-first messages)");
        }
    }

    [TestFixture]
    public class AlternationRulesTests : MessageTurnTakingRuleTests
    {
        [Test]
        public void IsBroken_AfterUserMessage_WithAssistantMessage_ShouldReturnFalse()
        {
            // Arrange
            var userMessage = MessageBuilder.New()
                .AsUserMessage()
                .WithSequence(1)
                .Build();
                
            var messages = new List<Message> { userMessage };
            var rule = new MessageTurnTakingRule(messages, MessageRole.Assistant);

            // Act
            var result = rule.IsBroken();

            // Assert
            result.ShouldBeFalse("Rule should not be broken when assistant follows user message");
        }

        [Test]
        public void IsBroken_AfterUserMessage_WithAnotherUserMessage_ShouldReturnTrue()
        {
            // Arrange
            var userMessage = MessageBuilder.New()
                .AsUserMessage()
                .WithSequence(1)
                .Build();
                
            var messages = new List<Message> { userMessage };
            var rule = new MessageTurnTakingRule(messages, MessageRole.User);

            // Act
            var result = rule.IsBroken();

            // Assert
            result.ShouldBeTrue("Rule should be broken when user tries to send consecutive messages");
        }

        [Test]
        public void IsBroken_AfterAssistantMessage_WithUserMessage_ShouldReturnFalse()
        {
            // Arrange
            var assistantMessage = MessageBuilder.New()
                .AsAssistantMessage()
                .WithSequence(1)
                .WithAiResponseId(TestConstants.AiResponses.DefaultAiResponseId)
                .Build();
                
            var messages = new List<Message> { assistantMessage };
            var rule = new MessageTurnTakingRule(messages, MessageRole.User);

            // Act
            var result = rule.IsBroken();

            // Assert
            result.ShouldBeFalse("Rule should not be broken when user follows assistant message");
        }

        [Test]
        public void IsBroken_AfterAssistantMessage_WithAnotherAssistantMessage_ShouldReturnTrue()
        {
            // Arrange
            var assistantMessage = MessageBuilder.New()
                .AsAssistantMessage()
                .WithSequence(1)
                .WithAiResponseId(TestConstants.AiResponses.DefaultAiResponseId)
                .Build();
                
            var messages = new List<Message> { assistantMessage };
            var rule = new MessageTurnTakingRule(messages, MessageRole.Assistant);

            // Act
            var result = rule.IsBroken();

            // Assert
            result.ShouldBeTrue("Rule should be broken when assistant tries to send consecutive messages");
        }
    }

    [TestFixture]
    public class ComplexScenarioTests : MessageTurnTakingRuleTests
    {
        [Test]
        public void IsBroken_WithValidAlternatingPattern_ShouldReturnFalse()
        {
            // Arrange - Create a valid alternating conversation: User -> Assistant -> User
            var userMessage1 = MessageBuilder.New()
                .AsUserMessage()
                .WithSequence(1)
                .WithContent("First user message")
                .Build();
                
            var assistantMessage = MessageBuilder.New()
                .AsAssistantMessage()
                .WithSequence(2)
                .WithAiResponseId(TestConstants.AiResponses.DefaultAiResponseId)
                .WithContent("Assistant response")
                .Build();
                
            var userMessage2 = MessageBuilder.New()
                .AsUserMessage()
                .WithSequence(3)
                .WithContent("Second user message")
                .Build();
                
            var messages = new List<Message> { userMessage1, assistantMessage, userMessage2 };
            var rule = new MessageTurnTakingRule(messages, MessageRole.Assistant);

            // Act
            var result = rule.IsBroken();

            // Assert
            result.ShouldBeFalse("Rule should not be broken when adding assistant message after valid alternating pattern");
        }

        [Test]
        public void IsBroken_WithLongValidConversation_ShouldReturnFalse()
        {
            // Arrange - Create a long alternating conversation ending with a user message
            var messages = new List<Message>();
            
            // Create 9 messages (not 10) to ensure the last one is a user message
            for (int i = 0; i < 9; i++)
            {
                if (i % 2 == 0) // Even indices are user messages
                {
                    var userMessage = MessageBuilder.New()
                        .AsUserMessage()
                        .WithSequence(i + 1)
                        .WithContent($"User message {i + 1}")
                        .Build();
                    messages.Add(userMessage);
                }
                else // Odd indices are assistant messages
                {
                    var assistantMessage = MessageBuilder.New()
                        .AsAssistantMessage()
                        .WithSequence(i + 1)
                        .WithAiResponseId(new AiResponseId($"ai-response-{i + 1}"))
                        .WithContent($"Assistant response {i + 1}")
                        .Build();
                    messages.Add(assistantMessage);
                }
            }
            
            // Verify the last message is a user message (i=8, even index)
            messages.Last().Role.IsUser.ShouldBeTrue("Last message should be from user");
            
            // Try to add an assistant message after the last user message (valid alternation)
            var rule = new MessageTurnTakingRule(messages, MessageRole.Assistant);

            // Act
            var result = rule.IsBroken();

            // Assert
            result.ShouldBeFalse("Rule should not be broken when adding assistant message after user message");
        }
    }

    [TestFixture]
    public class IsBrokenAsyncTests : MessageTurnTakingRuleTests
    {
        [Test]
        public async Task IsBrokenAsync_ShouldReturnSameResultAsIsBroken()
        {
            // Arrange
            var userMessage = MessageBuilder.New()
                .AsUserMessage()
                .WithSequence(1)
                .Build();
                
            var messages = new List<Message> { userMessage };
            var rule = new MessageTurnTakingRule(messages, MessageRole.User);

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
            var rule = new MessageTurnTakingRule(emptyMessages, MessageRole.User);
            using var cts = new CancellationTokenSource();

            // Act & Assert
            var result = await rule.IsBrokenAsync(cts.Token);
            result.ShouldBeFalse("Rule should not be broken when user starts conversation");
        }
    }

    [TestFixture]
    public class BusinessRulePropertiesTests : MessageTurnTakingRuleTests
    {
        [Test]
        public void Code_ShouldHaveExpectedValue()
        {
            // Arrange
            var emptyMessages = new List<Message>();
            var rule = new MessageTurnTakingRule(emptyMessages, MessageRole.User);

            // Act & Assert
            rule.Code.ShouldBe("CHAT.MESSAGE.TURN.VIOLATION");
        }

        [Test]
        public void Message_ShouldHaveExpectedValue()
        {
            // Arrange
            var emptyMessages = new List<Message>();
            var rule = new MessageTurnTakingRule(emptyMessages, MessageRole.User);

            // Act & Assert
            rule.Message.ShouldBe("Messages must alternate between user and assistant.");
        }
    }

    [TestFixture]
    public class ConstructorTests : MessageTurnTakingRuleTests
    {
        [Test]
        public void Constructor_WithNullMessages_ShouldCreateEmptyList()
        {
            // Act
            var rule = new MessageTurnTakingRule(null!, MessageRole.User);

            // Assert
            rule.ShouldNotBeNull();
            rule.IsBroken().ShouldBeFalse("Rule should handle null messages by treating as empty conversation");
        }

        [Test]
        public void Constructor_WithValidParameters_ShouldCreateRule()
        {
            // Arrange
            var messages = new List<Message>();

            // Act
            var rule = new MessageTurnTakingRule(messages, MessageRole.User);

            // Assert
            rule.ShouldNotBeNull();
            rule.Code.ShouldBe("CHAT.MESSAGE.TURN.VIOLATION");
        }
    }

    [TestFixture]
    public class EdgeCaseTests : MessageTurnTakingRuleTests
    {
        [Test]
        public void IsBroken_WithOnlyUserMessages_AddingUser_ShouldReturnTrue()
        {
            // Arrange
            var userMessage1 = MessageBuilder.New()
                .AsUserMessage()
                .WithSequence(1)
                .Build();
                
            var userMessage2 = MessageBuilder.New()
                .AsUserMessage()
                .WithSequence(2)
                .Build();
                
            var messages = new List<Message> { userMessage1, userMessage2 };
            var rule = new MessageTurnTakingRule(messages, MessageRole.User);

            // Act
            var result = rule.IsBroken();

            // Assert
            result.ShouldBeTrue("Rule should be broken when trying to add third consecutive user message");
        }

        [Test]
        public void IsBroken_WithOnlyAssistantMessages_AddingAssistant_ShouldReturnTrue()
        {
            // Arrange
            var assistantMessage1 = MessageBuilder.New()
                .AsAssistantMessage()
                .WithSequence(1)
                .WithAiResponseId(TestConstants.AiResponses.DefaultAiResponseId)
                .Build();
                
            var assistantMessage2 = MessageBuilder.New()
                .AsAssistantMessage()
                .WithSequence(2)
                .WithAiResponseId(TestConstants.AiResponses.AlternativeAiResponseId)
                .Build();
                
            var messages = new List<Message> { assistantMessage1, assistantMessage2 };
            var rule = new MessageTurnTakingRule(messages, MessageRole.Assistant);

            // Act
            var result = rule.IsBroken();

            // Assert
            result.ShouldBeTrue("Rule should be broken when trying to add third consecutive assistant message");
        }
    }

    [TestFixture]
    public class ConsistencyTests : MessageTurnTakingRuleTests
    {
        [Test]
        public void IsBroken_CalledMultipleTimes_ShouldReturnConsistentResults()
        {
            // Arrange
            var userMessage = MessageBuilder.New()
                .AsUserMessage()
                .WithSequence(1)
                .Build();
                
            var messages = new List<Message> { userMessage };
            var rule = new MessageTurnTakingRule(messages, MessageRole.User);

            // Act & Assert
            BusinessRuleTestHelpers.AssertRuleIsConsistent(rule);
        }

        [Test]
        public void Rule_ShouldProvideMeaningfulErrorMessage()
        {
            // Arrange
            var userMessage = MessageBuilder.New()
                .AsUserMessage()
                .WithSequence(1)
                .Build();
                
            var messages = new List<Message> { userMessage };
            var rule = new MessageTurnTakingRule(messages, MessageRole.User);

            // Act & Assert
            BusinessRuleTestHelpers.AssertRuleHasMeaningfulError(rule);
        }
    }

    [TestFixture]
    public class TestCaseValidationTests : MessageTurnTakingRuleTests
    {
        [TestCase(true, MessageRole.UserValue, false, Description = "User starts conversation - valid")]
        [TestCase(true, MessageRole.AssistantValue, true, Description = "Assistant starts conversation - invalid")]
        [TestCase(false, MessageRole.UserValue, true, Description = "User after user - invalid")]
        [TestCase(false, MessageRole.AssistantValue, false, Description = "Assistant after user - valid")]
        public void IsBroken_WithVariousScenarios_ShouldBehaveCorrectly(
            bool isEmptyConversation, 
            string newMessageRoleValue, 
            bool shouldBeBroken)
        {
            // Arrange
            var messages = isEmptyConversation 
                ? new List<Message>() 
                : new List<Message> 
                { 
                    MessageBuilder.New()
                        .AsUserMessage()
                        .WithSequence(1)
                        .Build()
                };

            var newMessageRole = MessageRole.From(newMessageRoleValue);
            var rule = new MessageTurnTakingRule(messages, newMessageRole);

            // Act
            var result = rule.IsBroken();

            // Assert
            result.ShouldBe(shouldBeBroken, 
                $"Rule for {(isEmptyConversation ? "empty" : "non-empty")} conversation with {newMessageRole} message should be {(shouldBeBroken ? "broken" : "not broken")}");
        }
    }
}