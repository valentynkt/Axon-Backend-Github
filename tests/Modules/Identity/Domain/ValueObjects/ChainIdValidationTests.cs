using Axon.Modules.Identity.Domain.ValueObjects;
using NUnit.Framework;
using Shouldly;

namespace Axon.Modules.Identity.Domain.Tests.ValueObjects;

[TestFixture]
public class ChainIdValidationTests
{
    [TestFixture]
    public class SupportedChainValidationTests : ChainIdValidationTests
    {
        // Test ID: 1.3-UNIT-018 - P1
        [TestCase("solana-mainnet")]
        [TestCase("solana-devnet")]
        [TestCase("solana-testnet")]
        [TestCase("ethereum-mainnet")]
        [TestCase("ethereum-goerli")]
        [TestCase("ethereum-sepolia")]
        [TestCase("polygon-mainnet")]
        [TestCase("polygon-mumbai")]
        [TestCase("arbitrum-one")]
        [TestCase("arbitrum-goerli")]
        public void From_WithSupportedChainId_ShouldCreateChainId(string supportedChainId)
        {
            // Act - When creating ChainId from supported chain
            var result = ChainId.From(supportedChainId);

            // Assert - Then should create successfully
            result.ShouldNotBeNull();
            result.Value.ShouldBe(supportedChainId);
        }

        [TestCase("bitcoin-mainnet")] // Unsupported chain
        [TestCase("cardano-mainnet")] // Unsupported chain
        [TestCase("custom-chain")] // Custom chain
        [TestCase("solana-localnet")] // Unsupported network
        [TestCase("ethereum-kovan")] // Deprecated network
        public void From_WithUnsupportedChainId_ShouldThrowArgumentException(string unsupportedChainId)
        {
            // Act & Assert - When creating ChainId from unsupported chain
            Should.Throw<ArgumentException>(() => ChainId.From(unsupportedChainId));
        }
    }

    [TestFixture]
    public class ChainIdFormatValidationTests : ChainIdValidationTests
    {
        [TestCase("ethereum-mainnet")] // Standard format
        [TestCase("solana-devnet")] // Standard format
        [TestCase("polygon-mumbai")] // Standard format
        [TestCase("arbitrum-one")] // Special case with number
        public void From_WithValidFormat_ShouldCreateChainId(string validChainId)
        {
            // Act - When creating ChainId with valid format
            var result = ChainId.From(validChainId);

            // Assert - Then should create successfully
            result.ShouldNotBeNull();
            result.Value.ShouldBe(validChainId);
        }

        [TestCase("")] // Empty string
        [TestCase(" ")] // Whitespace
        [TestCase("ethereum_mainnet")] // Wrong separator
        [TestCase("ethereum.mainnet")] // Wrong separator
        [TestCase("ETHEREUM-MAINNET")] // Wrong case
        [TestCase("ethereum-")] // Missing network
        [TestCase("-mainnet")] // Missing blockchain
        [TestCase("ethereum--mainnet")] // Double separator
        [TestCase("ethereum-mainnet-extra")] // Too many parts
        public void From_WithInvalidFormat_ShouldThrowArgumentException(string invalidChainId)
        {
            // Act & Assert - When creating ChainId with invalid format
            Should.Throw<ArgumentException>(() => ChainId.From(invalidChainId));
        }
    }

    [TestFixture]
    public class NullAndEmptyValidationTests : ChainIdValidationTests
    {
        [Test]
        public void From_WithNullChainId_ShouldThrowArgumentNullException()
        {
            // Act & Assert - When creating ChainId from null
            Should.Throw<ArgumentNullException>(() => ChainId.From(null!));
        }

        [Test]
        public void From_WithEmptyChainId_ShouldThrowArgumentException()
        {
            // Act & Assert - When creating ChainId from empty string
            Should.Throw<ArgumentException>(() => ChainId.From(""));
        }

        [Test]
        public void From_WithWhitespaceChainId_ShouldThrowArgumentException()
        {
            // Act & Assert - When creating ChainId from whitespace
            Should.Throw<ArgumentException>(() => ChainId.From("   "));
        }
    }

    [TestFixture]
    public class EqualityTests : ChainIdValidationTests
    {
        [Test]
        public void Equals_WithSameChainId_ShouldReturnTrue()
        {
            // Arrange - Given two ChainIds with same value
            var chainId1 = ChainId.From("ethereum-mainnet");
            var chainId2 = ChainId.From("ethereum-mainnet");

            // Act & Assert - When comparing equal ChainIds
            chainId1.ShouldBe(chainId2);
            chainId1.Equals(chainId2).ShouldBeTrue();
            (chainId1 == chainId2).ShouldBeTrue();
            (chainId1 != chainId2).ShouldBeFalse();
        }

        [Test]
        public void Equals_WithDifferentChainId_ShouldReturnFalse()
        {
            // Arrange - Given two ChainIds with different values
            var chainId1 = ChainId.From("ethereum-mainnet");
            var chainId2 = ChainId.From("solana-mainnet");

            // Act & Assert - When comparing different ChainIds
            chainId1.ShouldNotBe(chainId2);
            chainId1.Equals(chainId2).ShouldBeFalse();
            (chainId1 == chainId2).ShouldBeFalse();
            (chainId1 != chainId2).ShouldBeTrue();
        }

        [Test]
        public void GetHashCode_WithSameChainId_ShouldReturnSameHash()
        {
            // Arrange - Given two ChainIds with same value
            var chainId1 = ChainId.From("ethereum-mainnet");
            var chainId2 = ChainId.From("ethereum-mainnet");

            // Act & Assert - When getting hash codes
            chainId1.GetHashCode().ShouldBe(chainId2.GetHashCode());
        }
    }

    [TestFixture]
    public class ToStringTests : ChainIdValidationTests
    {
        [Test]
        public void ToString_ShouldReturnChainIdValue()
        {
            // Arrange - Given ChainId
            var chainId = ChainId.From("ethereum-mainnet");

            // Act - When converting to string
            var stringValue = chainId.ToString();

            // Assert - Then should return ChainId value
            stringValue.ShouldBe("ethereum-mainnet");
        }
    }

    [TestFixture]
    public class BlockchainDetectionTests : ChainIdValidationTests
    {
        [TestCase("solana-mainnet", "solana")]
        [TestCase("solana-devnet", "solana")]
        [TestCase("ethereum-mainnet", "ethereum")]
        [TestCase("ethereum-goerli", "ethereum")]
        [TestCase("polygon-mainnet", "polygon")]
        [TestCase("arbitrum-one", "arbitrum")]
        public void GetBlockchain_ShouldExtractBlockchainName(string chainIdValue, string expectedBlockchain)
        {
            // Arrange - Given ChainId
            var chainId = ChainId.From(chainIdValue);

            // Act - When extracting blockchain name
            var blockchain = ChainIdHelpers.GetBlockchainName(chainId);

            // Assert - Then should extract correct blockchain
            blockchain.ShouldBe(expectedBlockchain);
        }

        [TestCase("solana-mainnet", "mainnet")]
        [TestCase("solana-devnet", "devnet")]
        [TestCase("ethereum-mainnet", "mainnet")]
        [TestCase("ethereum-goerli", "goerli")]
        [TestCase("polygon-mumbai", "mumbai")]
        [TestCase("arbitrum-one", "one")]
        public void GetNetwork_ShouldExtractNetworkName(string chainIdValue, string expectedNetwork)
        {
            // Arrange - Given ChainId
            var chainId = ChainId.From(chainIdValue);

            // Act - When extracting network name
            var network = ChainIdHelpers.GetNetworkName(chainId);

            // Assert - Then should extract correct network
            network.ShouldBe(expectedNetwork);
        }
    }

    // Helper methods moved outside of nested classes
    internal static class ChainIdHelpers
    {
        internal static string GetBlockchainName(ChainId chainId)
        {
            return chainId.Value.Split('-')[0];
        }

        internal static string GetNetworkName(ChainId chainId)
        {
            return chainId.Value.Split('-')[1];
        }
    }

    [TestFixture]
    public class SecurityValidationTests : ChainIdValidationTests
    {
        [TestCase("javascript:alert('xss')")] // Script injection attempt
        [TestCase("<script>alert('xss')</script>")] // HTML injection attempt
        [TestCase("'; DROP TABLE users; --")] // SQL injection attempt
        [TestCase("../../../etc/passwd")] // Path traversal attempt
        public void From_WithMaliciousInput_ShouldRejectSafely(string maliciousInput)
        {
            // Act & Assert - When creating ChainId from malicious input
            Should.Throw<ArgumentException>(() => ChainId.From(maliciousInput));
        }

        [Test]
        public void From_WithExtremelyLongInput_ShouldRejectSafely()
        {
            // Arrange - Given extremely long input (potential DoS)
            var longInput = new string('a', 1000);

            // Act & Assert - When creating ChainId from long input
            Should.Throw<ArgumentException>(() => ChainId.From(longInput));
        }

        [Test]
        public void From_WithSpecialCharacters_ShouldRejectSafely()
        {
            // Arrange - Given input with special characters
            var specialCharsInput = "ethereum-main@net";

            // Act & Assert - When creating ChainId with special characters
            Should.Throw<ArgumentException>(() => ChainId.From(specialCharsInput));
        }

        [Test]
        public void From_WithUnicodeCharacters_ShouldRejectSafely()
        {
            // Arrange - Given input with Unicode characters
            var unicodeInput = "ethereum-mainnet€";

            // Act & Assert - When creating ChainId with Unicode
            Should.Throw<ArgumentException>(() => ChainId.From(unicodeInput));
        }
    }

    [TestFixture]
    public class ChainCompatibilityTests : ChainIdValidationTests
    {
        [TestCase("solana-mainnet", "solana-devnet", true)]
        [TestCase("solana-mainnet", "ethereum-mainnet", false)]
        [TestCase("ethereum-mainnet", "ethereum-goerli", true)]
        [TestCase("ethereum-mainnet", "polygon-mainnet", false)]
        public void IsCompatibleWith_ShouldDetermineChainCompatibility(string chainId1, string chainId2, bool expectedCompatible)
        {
            // Arrange - Given two ChainIds
            var firstChain = ChainId.From(chainId1);
            var secondChain = ChainId.From(chainId2);

            // Act - When checking compatibility
            var compatible = IsCompatible(firstChain, secondChain);

            // Assert - Then should determine correct compatibility
            compatible.ShouldBe(expectedCompatible);
        }

        private static bool IsCompatible(ChainId chain1, ChainId chain2)
        {
            // Simple compatibility check based on blockchain type
            return ChainIdHelpers.GetBlockchainName(chain1) == ChainIdHelpers.GetBlockchainName(chain2);
        }
    }
}