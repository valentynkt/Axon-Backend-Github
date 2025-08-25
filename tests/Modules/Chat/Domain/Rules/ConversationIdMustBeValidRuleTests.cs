// Test Infrastructure - Common Test Base Classes and Helpers
using Axon.Modules.Chat.Domain.Tests.Common;
using Axon.Modules.Chat.Domain.Tests.Builders;

namespace Axon.Modules.Chat.Domain.Tests.Rules;

[TestFixture]
public class ConversationIdMustBeValidRuleTests : DomainTestBase
{
    [TestFixture]
    public class IsBrokenTests : ConversationIdMustBeValidRuleTests
    {
        [Test]
        public void IsBroken_WithValidConversationId_ShouldReturnFalse()
        {
            // Arrange
            var conversationId = ConversationId.New();
            var rule = new ConversationIdMustBeValidRule(conversationId);

            // Act
            var result = rule.IsBroken();

            // Assert
            result.ShouldBeFalse("Rule should not be broken with valid ConversationId");
        }

        [Test]
        public void IsBroken_WithEmptyGuid_ShouldReturnTrue()
        {
            // Arrange
            var conversationId = new ConversationId(Guid.Empty);
            var rule = new ConversationIdMustBeValidRule(conversationId);

            // Act
            var result = rule.IsBroken();

            // Assert
            result.ShouldBeTrue("Rule should be broken with empty GUID");
        }
    }

    [TestFixture]
    public class IsBrokenAsyncTests : ConversationIdMustBeValidRuleTests
    {
        [Test]
        public async Task IsBrokenAsync_ShouldReturnSameResultAsIsBroken()
        {
            // Arrange
            var conversationId = new ConversationId(Guid.Empty);
            var rule = new ConversationIdMustBeValidRule(conversationId);

            // Act
            var syncResult = rule.IsBroken();
            var asyncResult = await rule.IsBrokenAsync();

            // Assert
            asyncResult.ShouldBe(syncResult, "IsBrokenAsync should return same result as IsBroken");
        }
    }

    [TestFixture]
    public class BusinessRulePropertiesTests : ConversationIdMustBeValidRuleTests
    {
        [Test]
        public void Code_ShouldHaveExpectedValue()
        {
            // Arrange
            var conversationId = ConversationId.New();
            var rule = new ConversationIdMustBeValidRule(conversationId);

            // Act & Assert
            rule.Code.ShouldBe("CHAT.ID.EMPTY");
        }

        [Test]
        public void Message_ShouldHaveExpectedValue()
        {
            // Arrange
            var conversationId = ConversationId.New();
            var rule = new ConversationIdMustBeValidRule(conversationId);

            // Act & Assert
            rule.Message.ShouldBe("ConversationId cannot be empty.");
        }
    }

    [TestFixture]
    public class EdgeCaseTests : ConversationIdMustBeValidRuleTests
    {
        [Test]
        public void IsBroken_WithNewlyGeneratedId_ShouldReturnFalse()
        {
            // Arrange
            var conversationId = new ConversationId(Guid.NewGuid());
            var rule = new ConversationIdMustBeValidRule(conversationId);

            // Act
            var result = rule.IsBroken();

            // Assert
            result.ShouldBeFalse("Rule should not be broken with newly generated GUID");
        }
    }

    [TestFixture]
    public class ConsistencyTests : ConversationIdMustBeValidRuleTests
    {
        [Test]
        public void IsBroken_CalledMultipleTimes_ShouldReturnConsistentResults()
        {
            // Arrange
            var conversationId = new ConversationId(Guid.Empty);
            var rule = new ConversationIdMustBeValidRule(conversationId);

            // Act & Assert
            BusinessRuleTestHelpers.AssertRuleIsConsistent(rule);
        }

        [Test]
        public void Rule_ShouldProvideMeaningfulErrorMessage()
        {
            // Arrange
            var conversationId = new ConversationId(Guid.Empty);
            var rule = new ConversationIdMustBeValidRule(conversationId);

            // Act & Assert
            BusinessRuleTestHelpers.AssertRuleHasMeaningfulError(rule);
        }
    }
}