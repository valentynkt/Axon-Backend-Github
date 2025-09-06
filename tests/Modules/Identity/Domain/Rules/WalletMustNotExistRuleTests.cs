using Axon.Modules.Identity.Domain.Rules;
using Axon.Modules.Identity.Domain.ValueObjects;

namespace Axon.Modules.Identity.Domain.Tests.Rules;

[TestFixture]
public class WalletMustNotExistRuleTests
{
    [Test]
    public async Task IsBrokenAsync_WhenWalletDoesNotExist_ReturnsFalse()
    {
        // Arrange
        var chainId = ChainId.From("solana");
        var address = Address.From("11111111111111111111111111111111");
        
        // Wallet does not exist
        ValueTask<bool> walletExistsCheck(ChainId c, Address a) => ValueTask.FromResult(false);
        
        var rule = new WalletMustNotExistRule(chainId, address, walletExistsCheck);

        // Act
        var result = await rule.IsBrokenAsync();

        // Assert
        result.ShouldBeFalse();
    }

    [Test]
    public async Task IsBrokenAsync_WhenWalletExists_ReturnsTrue()
    {
        // Arrange
        var chainId = ChainId.From("ethereum");
        var address = Address.From("0x742d35Cc6634C0532925a3b844Bc9e7595f0bEb1");
        
        // Wallet already exists
        ValueTask<bool> walletExistsCheck(ChainId c, Address a) => ValueTask.FromResult(true);
        
        var rule = new WalletMustNotExistRule(chainId, address, walletExistsCheck);

        // Act
        var result = await rule.IsBrokenAsync();

        // Assert
        result.ShouldBeTrue();
    }

    [Test]
    public async Task IsBrokenAsync_ChecksCorrectChainAndAddress()
    {
        // Arrange
        var expectedChain = ChainId.From("polygon");
        var expectedAddress = Address.From("0x123456789012345678901234567890123456789a");
        ChainId? actualChain = null;
        Address? actualAddress = null;
        
        ValueTask<bool> walletExistsCheck(ChainId c, Address a)
        {
            actualChain = c;
            actualAddress = a;
            return ValueTask.FromResult(false);
        }
        
        var rule = new WalletMustNotExistRule(expectedChain, expectedAddress, walletExistsCheck);

        // Act
        await rule.IsBrokenAsync();

        // Assert
        actualChain.ShouldBe(expectedChain);
        actualAddress.ShouldBe(expectedAddress);
    }

    [Test]
    public void Message_ContainsExpectedText()
    {
        // Arrange
        var chainId = ChainId.From("solana");
        var address = Address.From("11111111111111111111111111111111");
        ValueTask<bool> walletExistsCheck(ChainId c, Address a) => ValueTask.FromResult(false);
        
        var rule = new WalletMustNotExistRule(chainId, address, walletExistsCheck);

        // Act & Assert
        rule.Message.ShouldBe("Wallet with this chain and address combination already exists.");
    }

    [Test]
    public void Code_ContainsExpectedValue()
    {
        // Arrange
        var chainId = ChainId.From("solana");
        var address = Address.From("11111111111111111111111111111111");
        ValueTask<bool> walletExistsCheck(ChainId c, Address a) => ValueTask.FromResult(false);
        
        var rule = new WalletMustNotExistRule(chainId, address, walletExistsCheck);

        // Act & Assert
        rule.Code.ShouldBe("WALLET.ALREADY_EXISTS");
    }

    [Test]
    public void IsBroken_AlwaysReturnsFalse()
    {
        // Arrange
        var chainId = ChainId.From("solana");
        var address = Address.From("11111111111111111111111111111111");
        ValueTask<bool> walletExistsCheck(ChainId c, Address a) => ValueTask.FromResult(true);
        
        var rule = new WalletMustNotExistRule(chainId, address, walletExistsCheck);

        // Act
        var result = rule.IsBroken();

        // Assert - synchronous path always returns false to force async usage
        result.ShouldBeFalse();
    }

    [TestCase("solana", "11111111111111111111111111111111", false)]
    [TestCase("ethereum", "0x742d35Cc6634C0532925a3b844Bc9e7595f0bEb1", true)]
    [TestCase("polygon", "0x0000000000000000000000000000000000000000", false)]
    public async Task IsBrokenAsync_WithVariousChains_ReturnsExpectedResult(
        string chain, string addressValue, bool walletExists)
    {
        // Arrange
        var chainId = ChainId.From(chain);
        var address = Address.From(addressValue);
        
        ValueTask<bool> walletExistsCheck(ChainId c, Address a) => ValueTask.FromResult(walletExists);
        
        var rule = new WalletMustNotExistRule(chainId, address, walletExistsCheck);

        // Act
        var result = await rule.IsBrokenAsync();

        // Assert
        result.ShouldBe(walletExists);
    }
}