using Axon.Modules.Chat.Domain.Tests.Common;
using Axon.Modules.Chat.Domain.Tests.Builders;

namespace Axon.Modules.Chat.Domain.Tests.Rules;

/// <summary>
/// Tests for ConversationMustBeActiveRule that ensures only active conversations can accept new messages.
/// Tests status validation and business rule enforcement.
/// </summary>
[TestFixture]
public class ConversationMustBeActiveRuleTests : DomainTestBase
{
    [TestFixture]
    public class IsBrokenTests : ConversationMustBeActiveRuleTests
    {
        [Test]
        public void IsBroken_WithActiveConversation_ShouldReturnFalse()
        {
            // Arrange
            var conversation = ConversationBuilder.New()
                .WithOwner(TestConstants.Users.DefaultOwnerId)
                .Build();

            var rule = new ConversationMustBeActiveRule(conversation.Status);

            // Act
            var result = rule.IsBroken();

            // Assert
            result.ShouldBeFalse("Rule should not be broken when conversation is active");
            conversation.Status.ShouldBe(ConversationStatus.Active);
        }

        [Test]
        public void IsBroken_WithCompletedConversation_ShouldReturnTrue()
        {
            // Arrange
            var conversation = ConversationBuilder.New()
                .WithOwner(TestConstants.Users.DefaultOwnerId)
                .WithUserMessage("Test message")
                .ThatShouldBeCompleted()
                .Build();

            var rule = new ConversationMustBeActiveRule(conversation.Status);

            // Act
            var result = rule.IsBroken();

            // Assert
            result.ShouldBeTrue("Rule should be broken when conversation is completed");
            conversation.Status.ShouldBe(ConversationStatus.Completed);
        }
    }

    [TestFixture]
    public class IsBrokenAsyncTests : ConversationMustBeActiveRuleTests
    {
        [Test]
        public async Task IsBrokenAsync_ShouldReturnSameResultAsIsBroken()
        {
            // Arrange
            var conversation = ConversationBuilder.New()
                .WithOwner(TestConstants.Users.DefaultOwnerId)
                .WithUserMessage("Test message")
                .ThatShouldBeCompleted()
                .Build();

            var rule = new ConversationMustBeActiveRule(conversation.Status);

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
            var conversation = ConversationBuilder.New()
                .WithOwner(TestConstants.Users.DefaultOwnerId)
                .Build();

            var rule = new ConversationMustBeActiveRule(conversation.Status);
            using var cts = new CancellationTokenSource();

            // Act & Assert
            var result = await rule.IsBrokenAsync(cts.Token);
            result.ShouldBeFalse("Rule should not be broken with active conversation");
        }
    }

    [TestFixture]
    public class BusinessRulePropertiesTests : ConversationMustBeActiveRuleTests
    {
        [Test]
        public void Code_ShouldHaveExpectedValue()
        {
            // Arrange
            var conversation = ConversationBuilder.New().Build();
            var rule = new ConversationMustBeActiveRule(conversation.Status);

            // Act & Assert
            rule.Code.ShouldBe("CHAT.CONVERSATION.NOT.ACTIVE");
        }

        [Test]
        public void Message_ShouldHaveExpectedValue()
        {
            // Arrange
            var conversation = ConversationBuilder.New().Build();
            var rule = new ConversationMustBeActiveRule(conversation.Status);

            // Act & Assert
            rule.Message.ShouldBe("Conversation must be active to perform this operation.");
        }
    }

    [TestFixture]
    public class ConstructorTests : ConversationMustBeActiveRuleTests
    {
        [Test]
        public void Constructor_WithValidStatus_ShouldCreateRule()
        {
            // Act
            var rule = new ConversationMustBeActiveRule(ConversationStatus.Active);

            // Assert
            rule.ShouldNotBeNull();
            rule.Code.ShouldBe("CHAT.CONVERSATION.NOT.ACTIVE");
        }

        [Test]
        public void Constructor_WithValidConversation_ShouldCreateRule()
        {
            // Arrange
            var conversation = ConversationBuilder.New().Build();

            // Act
            var rule = new ConversationMustBeActiveRule(conversation.Status);

            // Assert
            rule.ShouldNotBeNull();
            rule.Code.ShouldBe("CHAT.CONVERSATION.NOT.ACTIVE");
        }
    }

    [TestFixture]
    public class EdgeCaseTests : ConversationMustBeActiveRuleTests
    {
        [Test]
        public void IsBroken_WithNewlyCreatedConversation_ShouldReturnFalse()
        {
            // Arrange
            var conversation = ConversationBuilder.New()
                .WithOwner(TestConstants.Users.DefaultOwnerId)
                .WithTitle(null) // No title, no messages
                .Build();

            var rule = new ConversationMustBeActiveRule(conversation.Status);

            // Act
            var result = rule.IsBroken();

            // Assert
            result.ShouldBeFalse("Rule should not be broken for newly created active conversation");
            conversation.IsActive.ShouldBeTrue();
        }

        [Test]
        public void IsBroken_WithActiveConversationWithMessages_ShouldReturnFalse()
        {
            // Arrange
            var conversation = ConversationBuilder.New()
                .WithOwner(TestConstants.Users.DefaultOwnerId)
                .WithUserMessage("First message")
                .WithAssistantMessage("First response")
                .Build();

            var rule = new ConversationMustBeActiveRule(conversation.Status);

            // Act
            var result = rule.IsBroken();

            // Assert
            result.ShouldBeFalse("Rule should not be broken for active conversation with messages");
            conversation.IsActive.ShouldBeTrue();
        }
    }

    [TestFixture]
    public class ConsistencyTests : ConversationMustBeActiveRuleTests
    {
        [Test]
        public void IsBroken_CalledMultipleTimes_ShouldReturnConsistentResults()
        {
            // Arrange
            var conversation = ConversationBuilder.New()
                .WithUserMessage("Test message")
                .ThatShouldBeCompleted()
                .Build();

            var rule = new ConversationMustBeActiveRule(conversation.Status);

            // Act & Assert
            BusinessRuleTestHelpers.AssertRuleIsConsistent(rule);
        }

        [Test]
        public void Rule_ShouldProvideMeaningfulErrorMessage()
        {
            // Arrange
            var conversation = ConversationBuilder.New()
                .WithUserMessage("Test message")
                .ThatShouldBeCompleted()
                .Build();

            var rule = new ConversationMustBeActiveRule(conversation.Status);

            // Act & Assert
            BusinessRuleTestHelpers.AssertRuleHasMeaningfulError(rule);
        }
    }

    [TestFixture]
    public class ConversationStatusTests : ConversationMustBeActiveRuleTests
    {
        [TestCase(ConversationStatus.Active, false, Description = "Active conversation should not break rule")]
        [TestCase(ConversationStatus.Completed, true, Description = "Completed conversation should break rule")]
        public void IsBroken_WithDifferentStatuses_ShouldBehaveCorrectly(
            ConversationStatus status, 
            bool shouldBeBroken)
        {
            // Arrange
            var conversationBuilder = ConversationBuilder.New()
                .WithOwner(TestConstants.Users.DefaultOwnerId);

            if (status == ConversationStatus.Completed)
            {
                conversationBuilder = conversationBuilder
                    .WithUserMessage("Test message")
                    .ThatShouldBeCompleted();
            }

            var conversation = conversationBuilder.Build();
            var rule = new ConversationMustBeActiveRule(conversation.Status);

            // Act
            var result = rule.IsBroken();

            // Assert
            result.ShouldBe(shouldBeBroken, 
                $"Rule for conversation with status {status} should be {(shouldBeBroken ? "broken" : "not broken")}");
        }
    }

    [TestFixture]
    public class IntegrationTests : ConversationMustBeActiveRuleTests
    {
        [Test]
        public void Rule_ShouldWorkWithConversationIsActiveProperty()
        {
            // Arrange
            var activeConversation = ConversationBuilder.New()
                .WithOwner(TestConstants.Users.DefaultOwnerId)
                .Build();

            var completedConversation = ConversationBuilder.New()
                .WithOwner(TestConstants.Users.DefaultOwnerId)
                .WithUserMessage("Test message")
                .ThatShouldBeCompleted()
                .Build();

            var activeRule = new ConversationMustBeActiveRule(activeConversation.Status);
            var completedRule = new ConversationMustBeActiveRule(completedConversation.Status);

            // Act & Assert
            activeConversation.IsActive.ShouldBeTrue();
            activeRule.IsBroken().ShouldBeFalse();

            completedConversation.IsActive.ShouldBeFalse();
            completedRule.IsBroken().ShouldBeTrue();
        }
    }
}