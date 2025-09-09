using Axon.Modules.Identity.Domain.Aggregates.Wallet;
using Axon.Modules.Identity.Domain.ValueObjects;
using BuildingBlocks.Primitives.Ids;
using NUnit.Framework;
using Shouldly;

namespace Axon.Modules.Identity.Domain.Tests.Aggregates;

[TestFixture]
public class WalletTests
{
    [TestFixture]
    public class Create : WalletTests
    {
        [Test]
        public void WithValidData_ShouldSucceed()
        {
            // Arrange
            var walletId = WalletId.New();
            var chainId = "solana-mainnet";
            var address = Address.From("9WzDXwBbmkg8ZTbNMqUxvQRAyrZzDsGYdLVL9zYtAWWM");
            var timestamp = DateTime.UtcNow;

            // Act
            var wallet = Wallet.Create(walletId, chainId, address, timestamp);

            // Assert
            wallet.ShouldNotBeNull();
            wallet.Id.ShouldBe(walletId);
            wallet.ChainId.ShouldBe(chainId);
            wallet.Address.ShouldBe(address);
            wallet.FirstSeenAt.ShouldBe(timestamp);
            wallet.LastSeenAt.ShouldBe(timestamp);
        }

        [Test]
        public void WithoutWalletId_ShouldGenerateNewId()
        {
            // Arrange
            var chainId = "ethereum-mainnet";
            var address = Address.From("0x742d35Cc6634C0532925a3b8D2aE39e7ec5B8e4d");

            // Act
            var wallet = Wallet.Create(null, chainId, address);

            // Assert
            wallet.ShouldNotBeNull();
            wallet.Id.ShouldNotBe(default(WalletId));
            wallet.ChainId.ShouldBe(chainId);
            wallet.Address.ShouldBe(address);
            wallet.FirstSeenAt.ShouldBeGreaterThan(DateTime.UtcNow.AddMinutes(-1));
            wallet.LastSeenAt.ShouldBe(wallet.FirstSeenAt);
        }

        [Test]
        public void WithoutTimestamp_ShouldUseCurrentTime()
        {
            // Arrange
            var chainId = "solana-mainnet";
            var address = Address.From("9WzDXwBbmkg8ZTbNMqUxvQRAyrZzDsGYdLVL9zYtAWWM");
            var beforeCreate = DateTime.UtcNow;

            // Act
            var wallet = Wallet.Create(null, chainId, address);
            var afterCreate = DateTime.UtcNow;

            // Assert
            wallet.FirstSeenAt.ShouldBeGreaterThanOrEqualTo(beforeCreate);
            wallet.FirstSeenAt.ShouldBeLessThanOrEqualTo(afterCreate);
            wallet.LastSeenAt.ShouldBe(wallet.FirstSeenAt);
        }
    }

    [TestFixture]
    public class UpdateLastSeen : WalletTests
    {
        [Test]
        public void WithLaterTimestamp_ShouldUpdate()
        {
            // Arrange
            var wallet = CreateTestWallet();
            var originalLastSeen = wallet.LastSeenAt;
            var newLastSeen = originalLastSeen.AddHours(1);

            // Act
            wallet.UpdateLastSeen(newLastSeen);

            // Assert
            wallet.LastSeenAt.ShouldBe(newLastSeen);
            wallet.FirstSeenAt.ShouldBe(originalLastSeen); // Should remain unchanged
        }

        [Test]
        public void WithEarlierTimestamp_ShouldNotUpdate()
        {
            // Arrange
            var wallet = CreateTestWallet();
            var originalLastSeen = wallet.LastSeenAt;
            var earlierTime = originalLastSeen.AddHours(-1);

            // Act
            wallet.UpdateLastSeen(earlierTime);

            // Assert
            wallet.LastSeenAt.ShouldBe(originalLastSeen); // Should remain unchanged
        }

        [Test]
        public void WithSameTimestamp_ShouldNotUpdate()
        {
            // Arrange
            var wallet = CreateTestWallet();
            var originalLastSeen = wallet.LastSeenAt;

            // Act
            wallet.UpdateLastSeen(originalLastSeen);

            // Assert
            wallet.LastSeenAt.ShouldBe(originalLastSeen);
        }
    }

    private static Wallet CreateTestWallet()
    {
        return Wallet.Create(
            WalletId.New(),
            "solana-mainnet",
            Address.From("9WzDXwBbmkg8ZTbNMqUxvQRAyrZzDsGYdLVL9zYtAWWM"),
            DateTime.UtcNow);
    }
}