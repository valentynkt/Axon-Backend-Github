using Axon.Modules.Identity.Domain.Tests.TestData;

namespace Axon.Modules.Identity.Domain.Tests.Rules;

[TestFixture]
public class MaxWalletsPerPrincipalRuleTests
{
    [TestCase(0, false, Description = "0 wallets is under limit")]
    [TestCase(5, false, Description = "5 wallets is under limit")]
    [TestCase(9, false, Description = "9 wallets is under limit")]
    [TestCase(10, true, Description = "10 wallets is at limit (max is 10, so >= 10 is broken)")]
    [TestCase(11, true, Description = "11 wallets exceeds limit")]
    [TestCase(20, true, Description = "20 wallets exceeds limit")]
    public void Rule_ValidatesWalletCount(int walletCount, bool shouldBeBroken)
    {
        // Arrange
        var rule = new MaxWalletsPerPrincipalRule(walletCount);
        
        // Act
        var isBroken = rule.IsBroken();
        
        // Assert
        isBroken.ShouldBe(shouldBeBroken);
    }
    
    [Test]
    public void Rule_HasCorrectErrorDetails()
    {
        // Arrange
        var rule = new MaxWalletsPerPrincipalRule(11);
        
        // Assert
        rule.Message.ShouldContain("10"); // Should mention the limit
        rule.Message.ShouldContain("11"); // Should mention current count
        rule.Code.ShouldBe("IDENTITY.WALLET.MAX_EXCEEDED");
    }
}