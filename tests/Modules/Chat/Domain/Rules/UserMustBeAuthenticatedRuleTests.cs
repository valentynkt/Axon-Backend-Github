// Test Infrastructure - Common Test Base Classes and Helpers
using Axon.Modules.Chat.Domain.Tests.Common;
using Axon.Modules.Chat.Domain.Tests.Builders;

namespace Axon.Modules.Chat.Domain.Tests.Rules;

[TestFixture]
public class UserMustBeAuthenticatedRuleTests : DomainTestBase
{
    [TestFixture]
    public class IsBrokenTests : UserMustBeAuthenticatedRuleTests
    {
        [Test]
        public void IsBroken_WithAuthenticatedUserAndValidUserId_ShouldReturnFalse()
        {
            // Arrange
            var userId = Guid.NewGuid().ToString();
            var rule = new UserMustBeAuthenticatedRule(isAuthenticated: true, userId);

            // Act
            var result = rule.IsBroken();

            // Assert
            result.ShouldBeFalse("Rule should not be broken when user is authenticated with valid ID");
        }

        [Test]
        public void IsBroken_WithUnauthenticatedUser_ShouldReturnTrue()
        {
            // Arrange
            var userId = Guid.NewGuid().ToString();
            var rule = new UserMustBeAuthenticatedRule(isAuthenticated: false, userId);

            // Act
            var result = rule.IsBroken();

            // Assert
            result.ShouldBeTrue("Rule should be broken when user is not authenticated");
        }

        [Test]
        public void IsBroken_WithAuthenticatedUserButNullUserId_ShouldReturnTrue()
        {
            // Arrange
            var rule = new UserMustBeAuthenticatedRule(isAuthenticated: true, userId: null);

            // Act
            var result = rule.IsBroken();

            // Assert
            result.ShouldBeTrue("Rule should be broken when authenticated but userId is null");
        }

        [Test]
        public void IsBroken_WithAuthenticatedUserButEmptyUserId_ShouldReturnTrue()
        {
            // Arrange
            var rule = new UserMustBeAuthenticatedRule(isAuthenticated: true, userId: string.Empty);

            // Act
            var result = rule.IsBroken();

            // Assert
            result.ShouldBeTrue("Rule should be broken when authenticated but userId is empty");
        }

        [Test]
        public void IsBroken_WithAuthenticatedUserButWhitespaceUserId_ShouldReturnTrue()
        {
            // Arrange
            var rule = new UserMustBeAuthenticatedRule(isAuthenticated: true, userId: "   ");

            // Act
            var result = rule.IsBroken();

            // Assert
            result.ShouldBeTrue("Rule should be broken when authenticated but userId is whitespace");
        }
    }

    [TestFixture]
    public class IsBrokenAsyncTests : UserMustBeAuthenticatedRuleTests
    {
        [Test]
        public async Task IsBrokenAsync_ShouldReturnSameResultAsIsBroken()
        {
            // Arrange
            var rule = new UserMustBeAuthenticatedRule(isAuthenticated: false, userId: null);

            // Act
            var syncResult = rule.IsBroken();
            var asyncResult = await rule.IsBrokenAsync();

            // Assert
            asyncResult.ShouldBe(syncResult, "IsBrokenAsync should return same result as IsBroken");
        }
    }

    [TestFixture]
    public class BusinessRulePropertiesTests : UserMustBeAuthenticatedRuleTests
    {
        [Test]
        public void Code_ShouldHaveExpectedValue()
        {
            // Arrange
            var rule = new UserMustBeAuthenticatedRule(isAuthenticated: false, userId: null);

            // Act & Assert
            rule.Code.ShouldBe(ChatDomainErrors.Authentication.UnauthenticatedCode);
        }

        [Test]
        public void Message_ShouldHaveExpectedValue()
        {
            // Arrange
            var rule = new UserMustBeAuthenticatedRule(isAuthenticated: false, userId: null);

            // Act & Assert
            rule.Message.ShouldBe(ChatDomainErrors.Authentication.UnauthenticatedMessage);
        }
    }

    [TestFixture]
    public class EdgeCaseTests : UserMustBeAuthenticatedRuleTests
    {
        [Test]
        public void IsBroken_WithBothUnauthenticatedAndNullUserId_ShouldReturnTrue()
        {
            // Arrange
            var rule = new UserMustBeAuthenticatedRule(isAuthenticated: false, userId: null);

            // Act
            var result = rule.IsBroken();

            // Assert
            result.ShouldBeTrue("Rule should be broken when both authentication and userId are invalid");
        }

        [Test]
        public void IsBroken_WithValidGuidUserId_ShouldReturnFalse()
        {
            // Arrange
            var userId = Guid.NewGuid().ToString();
            var rule = new UserMustBeAuthenticatedRule(isAuthenticated: true, userId);

            // Act
            var result = rule.IsBroken();

            // Assert
            result.ShouldBeFalse("Rule should not be broken with valid GUID format userId");
        }
    }

    [TestFixture]
    public class ConsistencyTests : UserMustBeAuthenticatedRuleTests
    {
        [Test]
        public void IsBroken_CalledMultipleTimes_ShouldReturnConsistentResults()
        {
            // Arrange
            var rule = new UserMustBeAuthenticatedRule(isAuthenticated: false, userId: null);

            // Act & Assert
            BusinessRuleTestHelpers.AssertRuleIsConsistent(rule);
        }

        [Test]
        public void Rule_ShouldProvideMeaningfulErrorMessage()
        {
            // Arrange
            var rule = new UserMustBeAuthenticatedRule(isAuthenticated: false, userId: null);

            // Act & Assert
            BusinessRuleTestHelpers.AssertRuleHasMeaningfulError(rule);
        }
    }

    [TestFixture]
    public class AuthenticationScenarioTests : UserMustBeAuthenticatedRuleTests
    {
        [TestCase(true, "valid-user-id", false, Description = "Authenticated with valid user ID should pass")]
        [TestCase(false, "valid-user-id", true, Description = "Unauthenticated should fail")]
        [TestCase(true, null, true, Description = "Authenticated but null user ID should fail")]
        [TestCase(true, "", true, Description = "Authenticated but empty user ID should fail")]
        [TestCase(false, null, true, Description = "Unauthenticated and null user ID should fail")]
        public void IsBroken_WithDifferentAuthenticationScenarios_ShouldBehaveCorrectly(
            bool isAuthenticated,
            string? userId,
            bool shouldBeBroken)
        {
            // Arrange
            var rule = new UserMustBeAuthenticatedRule(isAuthenticated, userId);

            // Act
            var result = rule.IsBroken();

            // Assert
            result.ShouldBe(shouldBeBroken,
                $"Rule with isAuthenticated={isAuthenticated} and userId='{userId}' should be {(shouldBeBroken ? "broken" : "not broken")}");
        }
    }
}