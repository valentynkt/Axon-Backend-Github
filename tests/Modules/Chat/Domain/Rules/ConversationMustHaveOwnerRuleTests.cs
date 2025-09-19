using Axon.Modules.Chat.Domain.Tests.Common;

namespace Axon.Modules.Chat.Domain.Tests.Rules;

/// <summary>
/// Tests for ConversationMustHaveOwnerRule that ensures conversations have a valid owner.
/// Tests owner validation and empty AxonUserId detection.
/// </summary>
[TestFixture]
public class ConversationMustHaveOwnerRuleTests : DomainTestBase
{
    [TestFixture]
    public class IsBrokenTests : ConversationMustHaveOwnerRuleTests
    {
        [Test]
        public void IsBroken_WithValidOwnerId_ShouldReturnFalse()
        {
            // Arrange
            var validOwnerId = TestConstants.Users.DefaultOwnerId;
            var rule = new ConversationMustHaveOwnerRule(validOwnerId);

            // Act
            var result = rule.IsBroken();

            // Assert
            result.ShouldBeFalse("Rule should not be broken with valid owner ID");
        }

        [Test]
        public void IsBroken_WithEmptyOwnerId_ShouldReturnTrue()
        {
            // Arrange
            var emptyOwnerId = new AxonUserId(Guid.Empty);
            var rule = new ConversationMustHaveOwnerRule(emptyOwnerId);

            // Act
            var result = rule.IsBroken();

            // Assert
            result.ShouldBeTrue("Rule should be broken with empty owner ID");
        }

        [Test]
        public void IsBroken_WithDefaultAxonUserId_ShouldReturnTrue()
        {
            // Arrange
            var defaultAxonUserId = default(AxonUserId);
            var rule = new ConversationMustHaveOwnerRule(defaultAxonUserId);

            // Act
            var result = rule.IsBroken();

            // Assert
            result.ShouldBeTrue("Rule should be broken with default AxonUserId");
        }

        [Test]
        public void IsBroken_WithMultipleValidOwnerIds_ShouldReturnFalse()
        {
            // Arrange
            var validOwnerIds = new[]
            {
                TestConstants.Users.DefaultOwnerId,
                TestConstants.Users.AlternativeOwnerId,
                TestConstants.Users.ThirdOwnerId,
                AxonUserId.New()
            };

            foreach (var ownerId in validOwnerIds)
            {
                var rule = new ConversationMustHaveOwnerRule(ownerId);

                // Act
                var result = rule.IsBroken();

                // Assert
                result.ShouldBeFalse($"Rule should not be broken with valid owner ID: {ownerId}");
            }
        }
    }

    [TestFixture]
    public class IsBrokenAsyncTests : ConversationMustHaveOwnerRuleTests
    {
        [Test]
        public async Task IsBrokenAsync_ShouldReturnSameResultAsIsBroken()
        {
            // Arrange
            var emptyOwnerId = new AxonUserId(Guid.Empty);
            var rule = new ConversationMustHaveOwnerRule(emptyOwnerId);

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
            var validOwnerId = TestConstants.Users.DefaultOwnerId;
            var rule = new ConversationMustHaveOwnerRule(validOwnerId);
            using var cts = new CancellationTokenSource();

            // Act & Assert
            var result = await rule.IsBrokenAsync(cts.Token);
            result.ShouldBeFalse("Rule should not be broken with valid owner ID");
        }
    }

    [TestFixture]
    public class BusinessRulePropertiesTests : ConversationMustHaveOwnerRuleTests
    {
        [Test]
        public void Code_ShouldHaveExpectedValue()
        {
            // Arrange
            var rule = new ConversationMustHaveOwnerRule(TestConstants.Users.DefaultOwnerId);

            // Act & Assert
            rule.Code.ShouldBe("CHAT.CONVERSATION.OWNER.REQUIRED");
        }

        [Test]
        public void Message_ShouldHaveExpectedValue()
        {
            // Arrange
            var rule = new ConversationMustHaveOwnerRule(TestConstants.Users.DefaultOwnerId);

            // Act & Assert
            rule.Message.ShouldBe("Conversation owner must be specified.");
        }
    }

    [TestFixture]
    public class ConstructorTests : ConversationMustHaveOwnerRuleTests
    {
        [Test]
        public void Constructor_WithValidOwnerId_ShouldCreateRule()
        {
            // Arrange & Act
            var rule = new ConversationMustHaveOwnerRule(TestConstants.Users.DefaultOwnerId);

            // Assert
            rule.ShouldNotBeNull();
            rule.Code.ShouldBe("CHAT.CONVERSATION.OWNER.REQUIRED");
        }

        [Test]
        public void Constructor_WithEmptyOwnerId_ShouldCreateRule()
        {
            // Arrange & Act
            var rule = new ConversationMustHaveOwnerRule(new AxonUserId(Guid.Empty));

            // Assert
            rule.ShouldNotBeNull();
            rule.IsBroken().ShouldBeTrue("Rule should be broken with empty owner ID");
        }
    }

    [TestFixture]
    public class EdgeCaseTests : ConversationMustHaveOwnerRuleTests
    {
        [Test]
        public void IsBroken_WithNewlyGeneratedOwnerId_ShouldReturnFalse()
        {
            // Arrange
            var newOwnerId = AxonUserId.New();
            var rule = new ConversationMustHaveOwnerRule(newOwnerId);

            // Act
            var result = rule.IsBroken();

            // Assert
            result.ShouldBeFalse("Rule should not be broken with newly generated owner ID");
            newOwnerId.Value.ShouldNotBe(Guid.Empty, "Newly generated AxonUserId should not be empty");
        }
    }

    [TestFixture]
    public class ConsistencyTests : ConversationMustHaveOwnerRuleTests
    {
        [Test]
        public void IsBroken_CalledMultipleTimes_ShouldReturnConsistentResults()
        {
            // Arrange
            var emptyOwnerId = new AxonUserId(Guid.Empty);
            var rule = new ConversationMustHaveOwnerRule(emptyOwnerId);

            // Act & Assert
            BusinessRuleTestHelpers.AssertRuleIsConsistent(rule);
        }

        [Test]
        public void Rule_ShouldProvideMeaningfulErrorMessage()
        {
            // Arrange
            var emptyOwnerId = new AxonUserId(Guid.Empty);
            var rule = new ConversationMustHaveOwnerRule(emptyOwnerId);

            // Act & Assert
            BusinessRuleTestHelpers.AssertRuleHasMeaningfulError(rule);
        }
    }

    [TestFixture]
    public class BoundaryValueTests : ConversationMustHaveOwnerRuleTests
    {
        [Test]
        public void IsBroken_WithBoundaryValues_ShouldBehaveCorrectly()
        {
            // Test data
            var validCases = new[]
            {
                AxonUserId.New(),
                TestConstants.Users.DefaultOwnerId,
                new AxonUserId(Guid.NewGuid())
            };

            var invalidCases = new[]
            {
                new AxonUserId(Guid.Empty),
                default(AxonUserId)
            };

            // Test valid cases
            foreach (var validCase in validCases)
            {
                var rule = new ConversationMustHaveOwnerRule(validCase);
                BusinessRuleTestHelpers.AssertRuleIsNotBroken(rule, 
                    $"Rule should not be broken for valid owner ID: {validCase}");
            }

            // Test invalid cases
            foreach (var invalidCase in invalidCases)
            {
                var rule = new ConversationMustHaveOwnerRule(invalidCase);
                BusinessRuleTestHelpers.AssertRuleIsBroken(rule, 
                    $"Rule should be broken for invalid owner ID: {invalidCase}");
            }
        }
    }

    [TestFixture]
    public class UseCaseTests : ConversationMustHaveOwnerRuleTests
    {
        [Test]
        public void Rule_ShouldEnforceBusinessRequirement_ConversationsMustHaveOwner()
        {
            // This test validates the core business requirement:
            // Every conversation must have a valid owner to ensure proper access control

            // Arrange - Simulate creating conversation without owner
            var noOwnerRule = new ConversationMustHaveOwnerRule(new AxonUserId(Guid.Empty));
            
            // Act & Assert - Should prevent creation
            noOwnerRule.IsBroken().ShouldBeTrue(
                "Business rule should prevent creating conversation without valid owner");

            // Arrange - Simulate creating conversation with valid owner
            var validOwnerRule = new ConversationMustHaveOwnerRule(TestConstants.Users.DefaultOwnerId);
            
            // Act & Assert - Should allow creation
            validOwnerRule.IsBroken().ShouldBeFalse(
                "Business rule should allow creating conversation with valid owner");
        }
    }
}