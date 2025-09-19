// Test Infrastructure - Common Test Base Classes and Helpers
using Axon.Modules.Chat.Domain.Tests.Common;
using Axon.Modules.Chat.Domain.Tests.Builders;

namespace Axon.Modules.Chat.Domain.Tests.Rules;

[TestFixture]
public class ConversationMustBelongToOwnerRuleTests : DomainTestBase
{
    [TestFixture]
    public class IsBrokenTests : ConversationMustBelongToOwnerRuleTests
    {
        [Test]
        public void IsBroken_WithConversationBelongingToOwner_ShouldReturnFalse()
        {
            // Arrange
            var ownerId = TestConstants.Users.DefaultOwnerId;
            var conversation = ConversationBuilder.New()
                .WithOwner(ownerId)
                .Build();
            var rule = new ConversationMustBelongToOwnerRule(conversation, ownerId);

            // Act
            var result = rule.IsBroken();

            // Assert
            result.ShouldBeFalse("Rule should not be broken when conversation belongs to owner");
        }

        [Test]
        public void IsBroken_WithConversationNotBelongingToOwner_ShouldReturnTrue()
        {
            // Arrange
            var actualOwnerId = TestConstants.Users.DefaultOwnerId;
            var differentOwnerId = AxonUserId.New();
            var conversation = ConversationBuilder.New()
                .WithOwner(actualOwnerId)
                .Build();
            var rule = new ConversationMustBelongToOwnerRule(conversation, differentOwnerId);

            // Act
            var result = rule.IsBroken();

            // Assert
            result.ShouldBeTrue("Rule should be broken when conversation does not belong to expected owner");
        }
    }

    [TestFixture]
    public class IsBrokenAsyncTests : ConversationMustBelongToOwnerRuleTests
    {
        [Test]
        public async Task IsBrokenAsync_ShouldReturnSameResultAsIsBroken()
        {
            // Arrange
            var actualOwnerId = TestConstants.Users.DefaultOwnerId;
            var differentOwnerId = AxonUserId.New();
            var conversation = ConversationBuilder.New()
                .WithOwner(actualOwnerId)
                .Build();
            var rule = new ConversationMustBelongToOwnerRule(conversation, differentOwnerId);

            // Act
            var syncResult = rule.IsBroken();
            var asyncResult = await rule.IsBrokenAsync();

            // Assert
            asyncResult.ShouldBe(syncResult, "IsBrokenAsync should return same result as IsBroken");
        }
    }

    [TestFixture]
    public class BusinessRulePropertiesTests : ConversationMustBelongToOwnerRuleTests
    {
        [Test]
        public void Code_ShouldHaveExpectedValue()
        {
            // Arrange
            var conversation = ConversationBuilder.New().Build();
            var ownerId = AxonUserId.New();
            var rule = new ConversationMustBelongToOwnerRule(conversation, ownerId);

            // Act & Assert
            rule.Code.ShouldBe("CHAT.CONVERSATION.ACCESS_DENIED");
        }

        [Test]
        public void Message_ShouldHaveExpectedValue()
        {
            // Arrange
            var conversation = ConversationBuilder.New().Build();
            var ownerId = AxonUserId.New();
            var rule = new ConversationMustBelongToOwnerRule(conversation, ownerId);

            // Act & Assert
            rule.Message.ShouldBe("Conversation does not belong to the current user.");
        }
    }

    [TestFixture]
    public class EdgeCaseTests : ConversationMustBelongToOwnerRuleTests
    {
        [Test]
        public void IsBroken_WithCompletedConversationBelongingToOwner_ShouldReturnFalse()
        {
            // Arrange
            var ownerId = TestConstants.Users.DefaultOwnerId;
            var conversation = ConversationBuilder.New()
                .WithOwner(ownerId)
                .WithUserMessage("Test message")
                .ThatShouldBeCompleted()
                .Build();
            var rule = new ConversationMustBelongToOwnerRule(conversation, ownerId);

            // Act
            var result = rule.IsBroken();

            // Assert
            result.ShouldBeFalse("Rule should not be broken for completed conversation owned by user");
        }

        [Test]
        public void IsBroken_WithConversationWithMessagesNotBelongingToOwner_ShouldReturnTrue()
        {
            // Arrange
            var actualOwnerId = TestConstants.Users.DefaultOwnerId;
            var differentOwnerId = AxonUserId.New();
            var conversation = ConversationBuilder.New()
                .WithOwner(actualOwnerId)
                .WithUserMessage("First message")
                .WithAssistantMessage("First response")
                .Build();
            var rule = new ConversationMustBelongToOwnerRule(conversation, differentOwnerId);

            // Act
            var result = rule.IsBroken();

            // Assert
            result.ShouldBeTrue("Rule should be broken even for conversation with messages not belonging to expected owner");
        }
    }

    [TestFixture]
    public class ConsistencyTests : ConversationMustBelongToOwnerRuleTests
    {
        [Test]
        public void IsBroken_CalledMultipleTimes_ShouldReturnConsistentResults()
        {
            // Arrange
            var actualOwnerId = TestConstants.Users.DefaultOwnerId;
            var differentOwnerId = AxonUserId.New();
            var conversation = ConversationBuilder.New()
                .WithOwner(actualOwnerId)
                .Build();
            var rule = new ConversationMustBelongToOwnerRule(conversation, differentOwnerId);

            // Act & Assert
            BusinessRuleTestHelpers.AssertRuleIsConsistent(rule);
        }

        [Test]
        public void Rule_ShouldProvideMeaningfulErrorMessage()
        {
            // Arrange
            var actualOwnerId = TestConstants.Users.DefaultOwnerId;
            var differentOwnerId = AxonUserId.New();
            var conversation = ConversationBuilder.New()
                .WithOwner(actualOwnerId)
                .Build();
            var rule = new ConversationMustBelongToOwnerRule(conversation, differentOwnerId);

            // Act & Assert
            BusinessRuleTestHelpers.AssertRuleHasMeaningfulError(rule);
        }
    }

    [TestFixture]
    public class OwnershipScenarioTests : ConversationMustBelongToOwnerRuleTests
    {
        [Test]
        public void IsBroken_WithSameOwnerIds_ShouldReturnFalse()
        {
            // Arrange
            var ownerId = AxonUserId.New();
            var conversation = ConversationBuilder.New()
                .WithOwner(ownerId)
                .Build();
            var rule = new ConversationMustBelongToOwnerRule(conversation, ownerId);

            // Act
            var result = rule.IsBroken();

            // Assert
            result.ShouldBeFalse("Rule should not be broken when owner IDs match exactly");
        }

        [Test]
        public void IsBroken_WithDifferentOwnerIds_ShouldReturnTrue()
        {
            // Arrange
            var owner1 = AxonUserId.New();
            var owner2 = AxonUserId.New();
            var conversation = ConversationBuilder.New()
                .WithOwner(owner1)
                .Build();
            var rule = new ConversationMustBelongToOwnerRule(conversation, owner2);

            // Act
            var result = rule.IsBroken();

            // Assert
            result.ShouldBeTrue("Rule should be broken when owner IDs are different");
        }
    }

    [TestFixture]
    public class IntegrationTests : ConversationMustBelongToOwnerRuleTests
    {
        [Test]
        public void Rule_ShouldWorkWithConversationBelongsToMethod()
        {
            // Arrange
            var ownerId = TestConstants.Users.DefaultOwnerId;
            var differentOwnerId = AxonUserId.New();
            var conversation = ConversationBuilder.New()
                .WithOwner(ownerId)
                .Build();

            var validRule = new ConversationMustBelongToOwnerRule(conversation, ownerId);
            var invalidRule = new ConversationMustBelongToOwnerRule(conversation, differentOwnerId);

            // Act & Assert
            conversation.BelongsTo(ownerId).ShouldBeTrue();
            validRule.IsBroken().ShouldBeFalse();

            conversation.BelongsTo(differentOwnerId).ShouldBeFalse();
            invalidRule.IsBroken().ShouldBeTrue();
        }
    }
}