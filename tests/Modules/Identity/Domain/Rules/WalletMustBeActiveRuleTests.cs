using Axon.Modules.Identity.Domain.Aggregates.Wallet;
using Axon.Modules.Identity.Domain.Rules;
using Axon.Modules.Identity.Domain.ValueObjects;
using BuildingBlocks.Primitives.Ids;
using System.Reflection;

namespace Axon.Modules.Identity.Domain.Tests.Rules;

[TestFixture]
public class WalletMustBeActiveRuleTests
{
    private static Wallet CreateWallet(bool isDeleted = false)
    {
        // Use reflection to create a wallet instance
        var walletConstructor = typeof(Wallet).GetConstructor(
            BindingFlags.NonPublic | BindingFlags.Instance, 
            null,
            Type.EmptyTypes,
            null);
        
        var wallet = (Wallet)walletConstructor!.Invoke(null);
        
        // Set required properties using reflection
        var idProperty = typeof(Wallet).GetProperty("Id");
        idProperty!.SetValue(wallet, WalletId.New());
        
        if (isDeleted)
        {
            // Call SoftDelete method via reflection to set IsDeleted to true
            wallet.SoftDelete();
        }
        
        return wallet;
    }

    [Test]
    public void IsBroken_WhenWalletIsActive_ReturnsFalse()
    {
        // Arrange
        var wallet = CreateWallet(isDeleted: false);
        var rule = new WalletMustBeActiveRule(wallet);

        // Act
        var result = rule.IsBroken();

        // Assert
        result.ShouldBeFalse();
    }

    [Test]
    public void IsBroken_WhenWalletIsDeleted_ReturnsTrue()
    {
        // Arrange
        var wallet = CreateWallet(isDeleted: true);
        var rule = new WalletMustBeActiveRule(wallet);

        // Act
        var result = rule.IsBroken();

        // Assert
        result.ShouldBeTrue();
    }

    [Test]
    public async Task IsBrokenAsync_WhenWalletIsActive_ReturnsFalse()
    {
        // Arrange
        var wallet = CreateWallet(isDeleted: false);
        var rule = new WalletMustBeActiveRule(wallet);

        // Act
        var result = await rule.IsBrokenAsync();

        // Assert
        result.ShouldBeFalse();
    }

    [Test]
    public async Task IsBrokenAsync_WhenWalletIsDeleted_ReturnsTrue()
    {
        // Arrange
        var wallet = CreateWallet(isDeleted: true);
        var rule = new WalletMustBeActiveRule(wallet);

        // Act
        var result = await rule.IsBrokenAsync();

        // Assert
        result.ShouldBeTrue();
    }

    [Test]
    public void Message_ContainsExpectedText()
    {
        // Arrange
        var wallet = CreateWallet();
        var rule = new WalletMustBeActiveRule(wallet);

        // Act & Assert
        rule.Message.ShouldNotBeNullOrWhiteSpace();
        rule.Message.ShouldContain("deleted");
    }

    [Test]
    public void Code_ContainsExpectedValue()
    {
        // Arrange
        var wallet = CreateWallet();
        var rule = new WalletMustBeActiveRule(wallet);

        // Act & Assert
        rule.Code.ShouldNotBeNullOrWhiteSpace();
        rule.Code.ShouldContain("WALLET");
    }
}