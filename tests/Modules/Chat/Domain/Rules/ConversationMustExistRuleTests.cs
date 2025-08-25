// Test Infrastructure - Common Test Base Classes and Helpers
using Axon.Modules.Chat.Domain.Tests.Common;
using Axon.Modules.Chat.Domain.Tests.Builders;

namespace Axon.Modules.Chat.Domain.Tests.Rules;

[TestFixture]
public class ConversationMustExistRuleTests : DomainTestBase
{
    [TestFixture]
    public class IsBrokenTests : ConversationMustExistRuleTests
    {
        [Test]
        public void IsBroken_WithExistingConversation_ShouldReturnFalse()
        {
            // Arrange
            var conversation = ConversationBuilder.New()
                .WithOwner(TestConstants.Users.DefaultOwnerId)
                .Build();
            var rule = new ConversationMustExistRule(conversation, conversation.Id);

            // Act
            var result = rule.IsBroken();

            // Assert
            result.ShouldBeFalse("Rule should not be broken when conversation exists");
        }

        [Test]
        public void IsBroken_WithNullConversation_ShouldReturnTrue()
        {
            // Arrange
            var conversationId = ConversationId.New();
            var rule = new ConversationMustExistRule(null, conversationId);

            // Act
            var result = rule.IsBroken();

            // Assert
            result.ShouldBeTrue("Rule should be broken when conversation is null");
        }
    }

    [TestFixture]
    public class IsBrokenAsyncTests : ConversationMustExistRuleTests
    {
        [Test]
        public async Task IsBrokenAsync_ShouldReturnSameResultAsIsBroken()
        {
            // Arrange
            var conversationId = ConversationId.New();
            var rule = new ConversationMustExistRule(null, conversationId);

            // Act
            var syncResult = rule.IsBroken();
            var asyncResult = await rule.IsBrokenAsync();

            // Assert
            asyncResult.ShouldBe(syncResult, "IsBrokenAsync should return same result as IsBroken");
        }
    }

    [TestFixture]
    public class BusinessRulePropertiesTests : ConversationMustExistRuleTests
    {
        [Test]
        public void Code_ShouldHaveExpectedValue()
        {
            // Arrange
            var conversationId = ConversationId.New();
            var rule = new ConversationMustExistRule(null, conversationId);

            // Act & Assert
            rule.Code.ShouldBe(ChatDomainErrors.Conversation.NotFoundCode);
        }

        [Test]
        public void Message_ShouldContainConversationId()
        {
            // Arrange
            var conversationId = ConversationId.New();
            var rule = new ConversationMustExistRule(null, conversationId);

            // Act & Assert
            rule.Message.ShouldContain(conversationId.Value.ToString());
        }
    }

    [TestFixture]
    public class EdgeCaseTests : ConversationMustExistRuleTests
    {
        [Test]
        public void IsBroken_WithCompletedConversation_ShouldReturnFalse()
        {
            // Arrange
            var conversation = ConversationBuilder.New()
                .WithOwner(TestConstants.Users.DefaultOwnerId)
                .WithUserMessage("Test message")
                .ThatShouldBeCompleted()
                .Build();
            var rule = new ConversationMustExistRule(conversation, conversation.Id);

            // Act
            var result = rule.IsBroken();

            // Assert
            result.ShouldBeFalse("Rule should not be broken for existing completed conversation");
        }

        [Test]
        public void IsBroken_WithConversationHavingMessages_ShouldReturnFalse()
        {
            // Arrange
            var conversation = ConversationBuilder.New()
                .WithOwner(TestConstants.Users.DefaultOwnerId)
                .WithUserMessage("First message")
                .WithAssistantMessage("First response")
                .Build();
            var rule = new ConversationMustExistRule(conversation, conversation.Id);

            // Act
            var result = rule.IsBroken();

            // Assert
            result.ShouldBeFalse("Rule should not be broken for existing conversation with messages");
        }
    }

    [TestFixture]
    public class ConsistencyTests : ConversationMustExistRuleTests
    {
        [Test]
        public void IsBroken_CalledMultipleTimes_ShouldReturnConsistentResults()
        {
            // Arrange
            var conversationId = ConversationId.New();
            var rule = new ConversationMustExistRule(null, conversationId);

            // Act & Assert
            BusinessRuleTestHelpers.AssertRuleIsConsistent(rule);
        }

        [Test]
        public void Rule_ShouldProvideMeaningfulErrorMessage()
        {
            // Arrange
            var conversationId = ConversationId.New();
            var rule = new ConversationMustExistRule(null, conversationId);

            // Act & Assert
            BusinessRuleTestHelpers.AssertRuleHasMeaningfulError(rule);
        }
    }

    [TestFixture]
    public class MessageContentTests : ConversationMustExistRuleTests
    {
        [Test]
        public void Message_ShouldFollowExpectedFormat()
        {
            // Arrange
            var conversationId = ConversationId.New();
            var rule = new ConversationMustExistRule(null, conversationId);

            // Act & Assert
            rule.Message.ShouldStartWith($"Conversation {conversationId.Value}");
            rule.Message.ShouldEndWith("not found.");
        }
    }
}