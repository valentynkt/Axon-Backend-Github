using Axon.Modules.Identity.Domain.Entities;
using Axon.Modules.Identity.Domain.Tests.Common;
using BuildingBlocks.Primitives.Ids;
using Shouldly;

namespace Axon.Modules.Identity.Domain.Tests.Entities;

/// <summary>
/// Comprehensive test suite for PrincipalChainDefault entity.
/// Tests creation, wallet updates, and entity behavior.
/// </summary>
[TestFixture]
public class PrincipalChainDefaultTests : IdentityTestBase
{
    #region Creation Tests

    [TestFixture]
    public class CreateTests : PrincipalChainDefaultTests
    {
        [Test]
        public void Create_WithValidParameters_Should_CreateDefaultWithCorrectProperties()
        {
            // Arrange
            var principalId = AxonId.New();
            var chainId = "ethereum-mainnet";
            var walletId = WalletId.New();

            // Act
            var chainDefault = PrincipalChainDefault.Create(principalId, chainId, walletId);

            // Assert
            chainDefault.ShouldSatisfyAllConditions(
                cd => cd.Id.ShouldNotBe(Guid.Empty),
                cd => cd.PrincipalId.ShouldBe(principalId),
                cd => cd.ChainId.ShouldBe(chainId),
                cd => cd.WalletId.ShouldBe(walletId),
                cd => cd.CreatedAt.ShouldBeLessThanOrEqualTo(DateTime.UtcNow),
                cd => cd.IsDeleted.ShouldBeFalse()
            );
        }

        [Test]
        public void Create_ShouldGenerateVersion7Guid()
        {
            // Arrange
            var principalId = AxonId.New();
            var chainId = "ethereum-mainnet";
            var walletId = WalletId.New();

            // Act
            var chainDefault = PrincipalChainDefault.Create(principalId, chainId, walletId);

            // Assert
            // Version 7 GUIDs have the version bits set to 0111 in the 13th position
            var guidBytes = chainDefault.Id.ToByteArray();
            var version = (guidBytes[7] & 0xF0) >> 4;
            version.ShouldBe(7); // Should be version 7 GUID
        }

        [Test]
        public void Create_ShouldGenerateUniqueIds()
        {
            // Arrange
            var principalId = AxonId.New();
            var chainId = "ethereum-mainnet";
            var walletId = WalletId.New();

            // Act
            var chainDefault1 = PrincipalChainDefault.Create(principalId, chainId, walletId);
            var chainDefault2 = PrincipalChainDefault.Create(principalId, chainId, walletId);

            // Assert
            chainDefault1.Id.ShouldNotBe(chainDefault2.Id);
        }

        [Test]
        [TestCase("ethereum-mainnet")]
        [TestCase("polygon-mainnet")]
        [TestCase("solana-mainnet")]
        [TestCase("arbitrum-one")]
        [TestCase("optimism-mainnet")]
        [TestCase("bitcoin-mainnet")]
        public void Create_WithDifferentChainIds_Should_AcceptAllValidChains(string chainId)
        {
            // Arrange
            var principalId = AxonId.New();
            var walletId = WalletId.New();

            // Act
            var chainDefault = PrincipalChainDefault.Create(principalId, chainId, walletId);

            // Assert
            chainDefault.ChainId.ShouldBe(chainId);
        }

        [Test]
        public void Create_WithEmptyChainId_Should_AllowEmptyValue()
        {
            // Arrange
            var principalId = AxonId.New();
            var chainId = "";
            var walletId = WalletId.New();

            // Act
            var chainDefault = PrincipalChainDefault.Create(principalId, chainId, walletId);

            // Assert
            chainDefault.ChainId.ShouldBe("");
        }

        [Test]
        public void Create_WithMultiplePrincipals_Should_AllowSameChainAndWallet()
        {
            // Different principals can have the same wallet as default for the same chain
            // This might represent shared wallets or multi-sig scenarios
            // Arrange
            var principalId1 = AxonId.New();
            var principalId2 = AxonId.New();
            var chainId = "ethereum-mainnet";
            var walletId = WalletId.New();

            // Act
            var chainDefault1 = PrincipalChainDefault.Create(principalId1, chainId, walletId);
            var chainDefault2 = PrincipalChainDefault.Create(principalId2, chainId, walletId);

            // Assert
            chainDefault1.ShouldNotBe(chainDefault2);
            chainDefault1.PrincipalId.ShouldNotBe(chainDefault2.PrincipalId);
            chainDefault1.ChainId.ShouldBe(chainDefault2.ChainId);
            chainDefault1.WalletId.ShouldBe(chainDefault2.WalletId);
        }
    }

    #endregion

    #region Wallet Update Tests

    [TestFixture]
    public class UpdateWalletTests : PrincipalChainDefaultTests
    {
        [Test]
        public void UpdateWallet_WithNewWalletId_Should_UpdateWalletId()
        {
            // Arrange
            var principalId = AxonId.New();
            var chainId = "ethereum-mainnet";
            var originalWalletId = WalletId.New();
            var newWalletId = WalletId.New();

            var chainDefault = PrincipalChainDefault.Create(principalId, chainId, originalWalletId);

            // Act
            chainDefault.UpdateWallet(newWalletId);

            // Assert
            chainDefault.WalletId.ShouldBe(newWalletId);
            chainDefault.PrincipalId.ShouldBe(principalId); // Should remain unchanged
            chainDefault.ChainId.ShouldBe(chainId); // Should remain unchanged
        }

        [Test]
        public void UpdateWallet_WithSameWalletId_Should_AllowIdempotentUpdate()
        {
            // Arrange
            var principalId = AxonId.New();
            var chainId = "ethereum-mainnet";
            var walletId = WalletId.New();

            var chainDefault = PrincipalChainDefault.Create(principalId, chainId, walletId);

            // Act
            chainDefault.UpdateWallet(walletId);

            // Assert
            chainDefault.WalletId.ShouldBe(walletId);
        }

        [Test]
        public void UpdateWallet_MultipleUpdates_Should_KeepLatestValue()
        {
            // Arrange
            var principalId = AxonId.New();
            var chainId = "ethereum-mainnet";
            var originalWalletId = WalletId.New();
            var walletId1 = WalletId.New();
            var walletId2 = WalletId.New();
            var finalWalletId = WalletId.New();

            var chainDefault = PrincipalChainDefault.Create(principalId, chainId, originalWalletId);

            // Act
            chainDefault.UpdateWallet(walletId1);
            chainDefault.UpdateWallet(walletId2);
            chainDefault.UpdateWallet(finalWalletId);

            // Assert
            chainDefault.WalletId.ShouldBe(finalWalletId);
        }

        [Test]
        public void UpdateWallet_Should_NotAffectOtherProperties()
        {
            // Arrange
            var principalId = AxonId.New();
            var chainId = "ethereum-mainnet";
            var originalWalletId = WalletId.New();
            var newWalletId = WalletId.New();

            var chainDefault = PrincipalChainDefault.Create(principalId, chainId, originalWalletId);
            var originalId = chainDefault.Id;
            var originalPrincipalId = chainDefault.PrincipalId;
            var originalChainId = chainDefault.ChainId;
            var originalCreatedAt = chainDefault.CreatedAt;

            // Act
            chainDefault.UpdateWallet(newWalletId);

            // Assert
            chainDefault.ShouldSatisfyAllConditions(
                cd => cd.Id.ShouldBe(originalId),
                cd => cd.PrincipalId.ShouldBe(originalPrincipalId),
                cd => cd.ChainId.ShouldBe(originalChainId),
                cd => cd.CreatedAt.ShouldBe(originalCreatedAt),
                cd => cd.WalletId.ShouldBe(newWalletId)
            );
        }
    }

    #endregion

    #region Edge Cases and Validation Tests

    [TestFixture]
    public class EdgeCasesTests : PrincipalChainDefaultTests
    {
        [Test]
        public void Create_WithSpecialCharactersInChainId_Should_HandleCorrectly()
        {
            // Arrange
            var principalId = AxonId.New();
            var chainId = "test-chain-123_special.chars@domain";
            var walletId = WalletId.New();

            // Act
            var chainDefault = PrincipalChainDefault.Create(principalId, chainId, walletId);

            // Assert
            chainDefault.ChainId.ShouldBe(chainId);
        }

        [Test]
        public void Create_WithUnicodeChainId_Should_HandleCorrectly()
        {
            // Arrange
            var principalId = AxonId.New();
            var chainId = "测试链-🚀";
            var walletId = WalletId.New();

            // Act
            var chainDefault = PrincipalChainDefault.Create(principalId, chainId, walletId);

            // Assert
            chainDefault.ChainId.ShouldBe(chainId);
        }

        [Test]
        public void Create_WithVeryLongChainId_Should_HandleCorrectly()
        {
            // Arrange
            var principalId = AxonId.New();
            var chainId = new string('a', 1000);
            var walletId = WalletId.New();

            // Act
            var chainDefault = PrincipalChainDefault.Create(principalId, chainId, walletId);

            // Assert
            chainDefault.ChainId.ShouldBe(chainId);
        }

        [Test]
        public void UpdateWallet_ConcurrentUpdates_Should_HandleCorrectly()
        {
            // This test simulates concurrent updates to verify behavior
            // Arrange
            var principalId = AxonId.New();
            var chainId = "ethereum-mainnet";
            var originalWalletId = WalletId.New();
            var chainDefault = PrincipalChainDefault.Create(principalId, chainId, originalWalletId);

            var walletIds = new[]
            {
                WalletId.New(),
                WalletId.New(),
                WalletId.New(),
                WalletId.New(),
                WalletId.New()
            };

            // Act - Simulate concurrent updates
            var tasks = new List<Task>();
            foreach (var walletId in walletIds)
            {
                tasks.Add(Task.Run(() => chainDefault.UpdateWallet(walletId)));
            }

            Task.WaitAll(tasks.ToArray());

            // Assert - Should have one of the wallet IDs (last one to complete)
            walletIds.ShouldContain(chainDefault.WalletId);
        }

        [Test]
        public void Create_WithTestnetChains_Should_SupportTestEnvironments()
        {
            var testnetChains = new[]
            {
                "ethereum-goerli",
                "ethereum-sepolia",
                "polygon-mumbai",
                "solana-devnet",
                "arbitrum-goerli",
                "optimism-goerli"
            };

            foreach (var chainId in testnetChains)
            {
                // Arrange
                var principalId = AxonId.New();
                var walletId = WalletId.New();

                // Act
                var chainDefault = PrincipalChainDefault.Create(principalId, chainId, walletId);

                // Assert
                chainDefault.ChainId.ShouldBe(chainId);
            }
        }
    }

    #endregion

    #region Equality and Comparison Tests

    [TestFixture]
    public class EqualityTests : PrincipalChainDefaultTests
    {
        [Test]
        public void ChainDefaults_WithSamePrincipalAndChain_Should_BeDifferentEntities()
        {
            // Even with same principal and chain, they are different entities with different IDs
            // This represents the case where defaults might be updated over time
            // Arrange
            var principalId = AxonId.New();
            var chainId = "ethereum-mainnet";
            var walletId1 = WalletId.New();
            var walletId2 = WalletId.New();

            // Act
            var chainDefault1 = PrincipalChainDefault.Create(principalId, chainId, walletId1);
            var chainDefault2 = PrincipalChainDefault.Create(principalId, chainId, walletId2);

            // Assert
            chainDefault1.ShouldNotBe(chainDefault2);
            chainDefault1.Id.ShouldNotBe(chainDefault2.Id);
            chainDefault1.PrincipalId.ShouldBe(chainDefault2.PrincipalId);
            chainDefault1.ChainId.ShouldBe(chainDefault2.ChainId);
            chainDefault1.WalletId.ShouldNotBe(chainDefault2.WalletId);
        }

        [Test]
        public void ChainDefaults_WithDifferentPrincipals_Should_BeDifferentEntities()
        {
            // Arrange
            var principalId1 = AxonId.New();
            var principalId2 = AxonId.New();
            var chainId = "ethereum-mainnet";
            var walletId = WalletId.New();

            // Act
            var chainDefault1 = PrincipalChainDefault.Create(principalId1, chainId, walletId);
            var chainDefault2 = PrincipalChainDefault.Create(principalId2, chainId, walletId);

            // Assert
            chainDefault1.ShouldNotBe(chainDefault2);
            chainDefault1.PrincipalId.ShouldNotBe(chainDefault2.PrincipalId);
        }

        [Test]
        public void ChainDefaults_WithDifferentChains_Should_BeDifferentEntities()
        {
            // Arrange
            var principalId = AxonId.New();
            var walletId = WalletId.New();

            // Act
            var ethereumDefault = PrincipalChainDefault.Create(principalId, "ethereum-mainnet", walletId);
            var polygonDefault = PrincipalChainDefault.Create(principalId, "polygon-mainnet", walletId);

            // Assert
            ethereumDefault.ShouldNotBe(polygonDefault);
            ethereumDefault.ChainId.ShouldNotBe(polygonDefault.ChainId);
        }
    }

    #endregion

    #region Inheritance and Base Class Tests

    [TestFixture]
    public class InheritanceTests : PrincipalChainDefaultTests
    {
        [Test]
        public void PrincipalChainDefault_Should_InheritFromAuditableDeletableEntity()
        {
            // Arrange & Act
            var chainDefault = PrincipalChainDefault.Create(AxonId.New(), "ethereum-mainnet", WalletId.New());

            // Assert - Verify auditable properties are available
            var now = DateTimeOffset.UtcNow;
            chainDefault.ShouldSatisfyAllConditions(
                cd => cd.CreatedAt.ShouldBeLessThanOrEqualTo(now),
                cd => cd.UpdatedAt.ShouldNotBeNull().ShouldBeLessThanOrEqualTo(now),
                cd => cd.IsDeleted.ShouldBeFalse(),
                cd => cd.DeletedAt.ShouldBeNull()
            );
        }

        [Test]
        public void PrincipalChainDefault_Should_UseGuidAsId()
        {
            // Arrange & Act
            var chainDefault = PrincipalChainDefault.Create(AxonId.New(), "ethereum-mainnet", WalletId.New());

            // Assert
            chainDefault.Id.ShouldBeOfType<Guid>();
            chainDefault.Id.ShouldNotBe(Guid.Empty);
        }
    }

    #endregion

    #region Business Logic Tests

    [TestFixture]
    public class BusinessLogicTests : PrincipalChainDefaultTests
    {
        [Test]
        public void ChainDefault_Scenario_PrincipalChangesDefaultWallet()
        {
            // Scenario: A principal initially sets one wallet as default for Ethereum,
            // then later changes to a different wallet

            // Arrange
            var principalId = AxonId.New();
            var chainId = "ethereum-mainnet";
            var initialWalletId = WalletId.New();
            var newWalletId = WalletId.New();

            // Act
            var chainDefault = PrincipalChainDefault.Create(principalId, chainId, initialWalletId);

            // Verify initial state
            chainDefault.WalletId.ShouldBe(initialWalletId);

            // Update to new wallet
            chainDefault.UpdateWallet(newWalletId);

            // Assert
            chainDefault.WalletId.ShouldBe(newWalletId);
            chainDefault.PrincipalId.ShouldBe(principalId);
            chainDefault.ChainId.ShouldBe(chainId);
        }

        [Test]
        public void ChainDefault_Scenario_MultipleChainsForSamePrincipal()
        {
            // Scenario: A principal has default wallets for multiple chains

            // Arrange
            var principalId = AxonId.New();
            var ethereumWalletId = WalletId.New();
            var polygonWalletId = WalletId.New();
            var solanaWalletId = WalletId.New();

            // Act
            var ethereumDefault = PrincipalChainDefault.Create(principalId, "ethereum-mainnet", ethereumWalletId);
            var polygonDefault = PrincipalChainDefault.Create(principalId, "polygon-mainnet", polygonWalletId);
            var solanaDefault = PrincipalChainDefault.Create(principalId, "solana-mainnet", solanaWalletId);

            // Assert - All defaults should be for the same principal but different chains and wallets
            var defaults = new[] { ethereumDefault, polygonDefault, solanaDefault };
            defaults.ShouldAllBe(d => d.PrincipalId == principalId);
            defaults.Select(d => d.ChainId).ShouldBeUnique();
            defaults.Select(d => d.WalletId).ShouldBeUnique();
        }
    }

    #endregion
}