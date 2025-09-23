using Axon.Modules.Identity.Domain.Aggregates.Wallet;
using Axon.Modules.Identity.Domain.Enums;
using Axon.Modules.Identity.Domain.Events;
using Axon.Modules.Identity.Domain.Tests.Common;
using Axon.Modules.Identity.Domain.Tests.TestData;
using Axon.Modules.Identity.Domain.ValueObjects;
using BuildingBlocks.Core.Diagnostics.Errors;
using BuildingBlocks.Primitives.Ids;
using CSharpFunctionalExtensions;
using Shouldly;

namespace Axon.Modules.Identity.Domain.Tests.Aggregates;

/// <summary>
/// Comprehensive test suite for Wallet aggregate root.
/// Tests creation, ownership linking/unlinking, validation, and domain events.
/// </summary>
[TestFixture]
public class WalletTests : IdentityTestBase
{
    #region Creation Tests

    [TestFixture]
    public class CreateTests : WalletTests
    {
        [Test]
        public void Create_WithValidParameters_Should_CreateWalletWithCorrectProperties()
        {
            // Arrange
            var walletId = WalletId.New();
            var chainId = "ethereum-mainnet";
            var address = Builders.EthereumAddress;
            var timestamp = DateTime.UtcNow;

            // Act
            var wallet = Wallet.Create(walletId, chainId, address, timestamp);

            // Assert
            wallet.ShouldSatisfyAllConditions(
                w => w.Id.ShouldBe(walletId),
                w => w.ChainId.ShouldBe(chainId),
                w => w.Address.ShouldBe(address),
                w => w.FirstSeenAt.ShouldBe(timestamp),
                w => w.LastSeenAt.ShouldBe(timestamp)
            );
        }

        [Test]
        public void Create_WithNullId_Should_GenerateNewId()
        {
            // Arrange
            var chainId = "ethereum-mainnet";
            var address = Builders.EthereumAddress;

            // Act
            var wallet = Wallet.Create(null, chainId, address);

            // Assert
            wallet.Id.Value.ShouldNotBe(Guid.Empty);
        }

        [Test]
        public void Create_WithNullTimestamp_Should_UseCurrentTime()
        {
            // Arrange
            var beforeCreation = DateTime.UtcNow;
            var chainId = "ethereum-mainnet";
            var address = Builders.EthereumAddress;

            // Act
            var wallet = Wallet.Create(null, chainId, address);

            // Assert
            var afterCreation = DateTime.UtcNow;
            wallet.FirstSeenAt.ShouldBeGreaterThanOrEqualTo(beforeCreation);
            wallet.FirstSeenAt.ShouldBeLessThanOrEqualTo(afterCreation);
            wallet.LastSeenAt.ShouldBe(wallet.FirstSeenAt);
        }

        [Test]
        public void Create_WithMultipleChainTypes_Should_HandleAllValidAddresses()
        {
            // Test cases for different chain types
            var testCases = new[]
            {
                ("ethereum-mainnet", Builders.EthereumAddress),
                ("solana-mainnet", Builders.SolanaAddress),
                ("polygon-mainnet", Address.From("0x742d35Cc6634C0532925a3b8D2aE39e7ec5B8e45")),
            };

            foreach (var (chainId, address) in testCases)
            {
                // Act
                var wallet = Wallet.Create(null, chainId, address);

                // Assert
                wallet.ChainId.ShouldBe(chainId);
                wallet.Address.ShouldBe(address);
            }
        }
    }

    #endregion

    #region Last Seen Update Tests

    [TestFixture]
    public class UpdateLastSeenTests : WalletTests
    {
        [Test]
        public void UpdateLastSeen_WithNewerTimestamp_Should_UpdateLastSeenAt()
        {
            // Arrange
            var initialTimestamp = DateTime.UtcNow.AddMinutes(-10);
            var wallet = CreateWallet();
            var newerTimestamp = DateTime.UtcNow;

            // Act
            wallet.UpdateLastSeen(newerTimestamp);

            // Assert
            wallet.LastSeenAt.ShouldBe(newerTimestamp);
            wallet.FirstSeenAt.ShouldBe(initialTimestamp); // Should remain unchanged
        }

        [Test]
        public void UpdateLastSeen_WithOlderTimestamp_Should_NotUpdateLastSeenAt()
        {
            // Arrange
            var recentTimestamp = DateTime.UtcNow;
            var wallet = CreateWallet();
            var olderTimestamp = recentTimestamp.AddMinutes(-5);

            // Act
            wallet.UpdateLastSeen(olderTimestamp);

            // Assert
            wallet.LastSeenAt.ShouldBe(recentTimestamp); // Should remain unchanged
        }

        [Test]
        public void UpdateLastSeen_WithSameTimestamp_Should_NotChange()
        {
            // Arrange
            var timestamp = DateTime.UtcNow;
            var wallet = CreateWallet();

            // Act
            wallet.UpdateLastSeen(timestamp);

            // Assert
            wallet.LastSeenAt.ShouldBe(timestamp);
        }
    }

    #endregion

    #region Owner Linking Tests

    [TestFixture]
    public class LinkToOwnerTests : WalletTests
    {
        [Test]
        public void LinkToOwner_WithValidParameters_Should_LinkOwnerAndRaiseEvent()
        {
            // Arrange
            var wallet = CreateWallet();
            var ownerId = AxonUserId.New();
            var accessMode = AccessMode.Signing;
            var status = OwnershipStatus.Verified;

            Func<WalletId, AccessMode, OwnershipStatus, Result<bool, Error>> noConflict =
                (_, _, _) => Result.Success<bool, Error>(false);

            wallet.ClearDomainEvents();

            // Act
            var result = wallet.LinkToOwner(ownerId, accessMode, status, noConflict);

            // Assert
            result.IsSuccess.ShouldBeTrue();

            wallet.DomainEvents.ShouldHaveSingleItem()
                .ShouldBeOfType<WalletChangedEvent>()
                .ShouldSatisfyAllConditions(
                    e => e.WalletId.ShouldBe(wallet.Id),
                    e => e.PropertyChanged.ShouldBe("ownership"),
                    e => e.OldValue.ShouldBe("none"),
                    e => e.NewValue.ShouldBe(ownerId.ToString()),
                    e => e.Metadata.ShouldNotBeNull().ShouldContainKeyAndValue("accessMode", accessMode.ToString()),
                    e => e.Metadata.ShouldNotBeNull().ShouldContainKeyAndValue("status", status.ToString())
                );
        }

        [Test]
        public void LinkToOwner_WithSameOwnerAndSettings_Should_BeIdempotent()
        {
            // Arrange
            var wallet = CreateWallet();
            var ownerId = AxonUserId.New();
            var accessMode = AccessMode.Signing;
            var status = OwnershipStatus.Verified;

            Func<WalletId, AccessMode, OwnershipStatus, Result<bool, Error>> noConflict =
                (_, _, _) => Result.Success<bool, Error>(false);

            // First link
            wallet.LinkToOwner(ownerId, accessMode, status, noConflict);
            wallet.ClearDomainEvents();

            // Act - Second link with same parameters
            var result = wallet.LinkToOwner(ownerId, accessMode, status, noConflict);

            // Assert
            result.IsSuccess.ShouldBeTrue();
            wallet.DomainEvents.ShouldBeEmpty(); // No event for idempotent operation
        }

        [Test]
        public void LinkToOwner_WithConflictingOwnership_Should_ReturnFailure()
        {
            // Arrange
            var wallet = CreateWallet();
            var ownerId = AxonUserId.New();

            Func<WalletId, AccessMode, OwnershipStatus, Result<bool, Error>> hasConflict =
                (_, _, _) => Result.Success<bool, Error>(true);

            // Act
            var result = wallet.LinkToOwner(ownerId, AccessMode.Signing, OwnershipStatus.Verified, hasConflict);

            // Assert
            result.IsFailure.ShouldBeTrue();
            result.Error.Type.ShouldBe(ErrorType.Conflict);
            wallet.DomainEvents.ShouldBeEmpty();
        }

        [Test]
        public void LinkToOwner_WhenConflictCheckFails_Should_ReturnFailure()
        {
            // Arrange
            var wallet = CreateWallet();
            var ownerId = AxonUserId.New();

            Func<WalletId, AccessMode, OwnershipStatus, Result<bool, Error>> errorCheck =
                (_, _, _) => Result.Failure<bool, Error>(Error.Failure("Database error", "DB_ERROR"));

            // Act
            var result = wallet.LinkToOwner(ownerId, AccessMode.Signing, OwnershipStatus.Verified, errorCheck);

            // Assert
            result.IsFailure.ShouldBeTrue();
            result.Error.Type.ShouldBe(ErrorType.Internal);
        }

        [Test]
        public void LinkToOwner_WithWatchOnlyAccess_Should_SkipConflictCheck()
        {
            // Arrange
            var wallet = CreateWallet();
            var ownerId = AxonUserId.New();

            // Mock that would fail if called for signing access
            Func<WalletId, AccessMode, OwnershipStatus, Result<bool, Error>> conflictCheck =
                (_, mode, status) => mode == AccessMode.Signing && status == OwnershipStatus.Verified
                    ? Result.Failure<bool, Error>(Error.Failure("Should not be called", "TEST_ERROR"))
                    : Result.Success<bool, Error>(false);

            // Act
            var result = wallet.LinkToOwner(ownerId, AccessMode.WatchOnly, OwnershipStatus.Verified, conflictCheck);

            // Assert
            result.IsSuccess.ShouldBeTrue(); // Should succeed without calling conflict check
        }

        [Test]
        public void LinkToOwner_WithPendingStatus_Should_SkipConflictCheck()
        {
            // Arrange
            var wallet = CreateWallet();
            var ownerId = AxonUserId.New();

            // Mock that would fail if called for verified signing
            Func<WalletId, AccessMode, OwnershipStatus, Result<bool, Error>> conflictCheck =
                (_, mode, status) => mode == AccessMode.Signing && status == OwnershipStatus.Verified
                    ? Result.Failure<bool, Error>(Error.Failure("Should not be called", "TEST_ERROR"))
                    : Result.Success<bool, Error>(false);

            // Act
            var result = wallet.LinkToOwner(ownerId, AccessMode.Signing, OwnershipStatus.Pending, conflictCheck);

            // Assert
            result.IsSuccess.ShouldBeTrue(); // Should succeed without calling conflict check
        }

        [Test]
        public void LinkToOwner_Should_UpdateLastSeenTimestamp()
        {
            // Arrange
            var initialTimestamp = DateTime.UtcNow.AddMinutes(-5);
            var wallet = CreateWallet();
            var ownerId = AxonUserId.New();

            Func<WalletId, AccessMode, OwnershipStatus, Result<bool, Error>> noConflict =
                (_, _, _) => Result.Success<bool, Error>(false);

            // Act
            var result = wallet.LinkToOwner(ownerId, AccessMode.Signing, OwnershipStatus.Verified, noConflict);

            // Assert
            result.IsSuccess.ShouldBeTrue();
            wallet.LastSeenAt.ShouldBeGreaterThan(initialTimestamp);
        }
    }

    #endregion

    #region Owner Unlinking Tests

    [TestFixture]
    public class UnlinkFromOwnerTests : WalletTests
    {
        [Test]
        public void UnlinkFromOwner_WithCorrectOwner_Should_UnlinkAndRaiseEvent()
        {
            // Arrange
            var wallet = CreateWallet();
            var ownerId = AxonUserId.New();

            Func<WalletId, AccessMode, OwnershipStatus, Result<bool, Error>> noConflict =
                (_, _, _) => Result.Success<bool, Error>(false);

            wallet.LinkToOwner(ownerId, AccessMode.Signing, OwnershipStatus.Verified, noConflict);
            wallet.ClearDomainEvents();

            // Act
            var result = wallet.UnlinkFromOwner(ownerId);

            // Assert
            result.IsSuccess.ShouldBeTrue();

            wallet.DomainEvents.ShouldHaveSingleItem()
                .ShouldBeOfType<WalletChangedEvent>()
                .ShouldSatisfyAllConditions(
                    e => e.WalletId.ShouldBe(wallet.Id),
                    e => e.PropertyChanged.ShouldBe("ownership"),
                    e => e.OldValue.ShouldBe(ownerId.ToString()),
                    e => e.NewValue.ShouldBe("none"),
                    e => e.Metadata.ShouldBeEmpty()
                );
        }

        [Test]
        public void UnlinkFromOwner_WithWrongOwner_Should_ReturnFailure()
        {
            // Arrange
            var wallet = CreateWallet();
            var actualOwnerId = AxonUserId.New();
            var wrongOwnerId = AxonUserId.New();

            Func<WalletId, AccessMode, OwnershipStatus, Result<bool, Error>> noConflict =
                (_, _, _) => Result.Success<bool, Error>(false);

            wallet.LinkToOwner(actualOwnerId, AccessMode.Signing, OwnershipStatus.Verified, noConflict);

            // Act
            var result = wallet.UnlinkFromOwner(wrongOwnerId);

            // Assert
            result.IsFailure.ShouldBeTrue();
            result.Error.Type.ShouldBe(ErrorType.NotFound);
        }

        [Test]
        public void UnlinkFromOwner_WithUnownedWallet_Should_ReturnFailure()
        {
            // Arrange
            var wallet = CreateWallet();
            var ownerId = AxonUserId.New();

            // Act
            var result = wallet.UnlinkFromOwner(ownerId);

            // Assert
            result.IsFailure.ShouldBeTrue();
            result.Error.Type.ShouldBe(ErrorType.NotFound);
        }

        [Test]
        public void UnlinkFromOwner_Should_UpdateLastSeenTimestamp()
        {
            // Arrange
            var initialTimestamp = DateTime.UtcNow.AddMinutes(-5);
            var wallet = CreateWallet();
            var ownerId = AxonUserId.New();

            Func<WalletId, AccessMode, OwnershipStatus, Result<bool, Error>> noConflict =
                (_, _, _) => Result.Success<bool, Error>(false);

            wallet.LinkToOwner(ownerId, AccessMode.Signing, OwnershipStatus.Verified, noConflict);

            // Act
            var result = wallet.UnlinkFromOwner(ownerId);

            // Assert
            result.IsSuccess.ShouldBeTrue();
            wallet.LastSeenAt.ShouldBeGreaterThan(initialTimestamp);
        }
    }

    #endregion

    #region Ownership Status Update Tests

    [TestFixture]
    public class UpdateOwnershipStatusTests : WalletTests
    {
        [Test]
        public void UpdateOwnershipStatus_WithOwnedWallet_Should_UpdateStatusAndRaiseEvent()
        {
            // Arrange
            var wallet = CreateWallet();
            var ownerId = AxonUserId.New();

            Func<WalletId, AccessMode, OwnershipStatus, Result<bool, Error>> noConflict =
                (_, _, _) => Result.Success<bool, Error>(false);

            wallet.LinkToOwner(ownerId, AccessMode.Signing, OwnershipStatus.Pending, noConflict);
            wallet.ClearDomainEvents();

            // Act
            var result = wallet.UpdateOwnershipStatus(OwnershipStatus.Verified);

            // Assert
            result.IsSuccess.ShouldBeTrue();

            wallet.DomainEvents.ShouldHaveSingleItem()
                .ShouldBeOfType<WalletChangedEvent>()
                .ShouldSatisfyAllConditions(
                    e => e.WalletId.ShouldBe(wallet.Id),
                    e => e.PropertyChanged.ShouldBe("ownershipStatus"),
                    e => e.OldValue.ShouldBe("Pending"),
                    e => e.NewValue.ShouldBe("Verified")
                );
        }

        [Test]
        public void UpdateOwnershipStatus_WithUnownedWallet_Should_ReturnFailure()
        {
            // Arrange
            var wallet = CreateWallet();

            // Act
            var result = wallet.UpdateOwnershipStatus(OwnershipStatus.Verified);

            // Assert
            result.IsFailure.ShouldBeTrue();
            result.Error.Type.ShouldBe(ErrorType.NotFound);
        }

        [Test]
        public void UpdateOwnershipStatus_WithSameStatus_Should_BeIdempotent()
        {
            // Arrange
            var wallet = CreateWallet();
            var ownerId = AxonUserId.New();

            Func<WalletId, AccessMode, OwnershipStatus, Result<bool, Error>> noConflict =
                (_, _, _) => Result.Success<bool, Error>(false);

            wallet.LinkToOwner(ownerId, AccessMode.Signing, OwnershipStatus.Verified, noConflict);
            wallet.ClearDomainEvents();

            // Act
            var result = wallet.UpdateOwnershipStatus(OwnershipStatus.Verified);

            // Assert
            result.IsSuccess.ShouldBeTrue();
            wallet.DomainEvents.ShouldBeEmpty(); // No event for idempotent operation
        }

        [Test]
        [TestCase(OwnershipStatus.Pending)]
        [TestCase(OwnershipStatus.Verified)]
        [TestCase(OwnershipStatus.Revoked)]
        public void UpdateOwnershipStatus_Should_AcceptAllValidStatuses(OwnershipStatus newStatus)
        {
            // Arrange
            var wallet = CreateWallet();
            var ownerId = AxonUserId.New();

            Func<WalletId, AccessMode, OwnershipStatus, Result<bool, Error>> noConflict =
                (_, _, _) => Result.Success<bool, Error>(false);

            wallet.LinkToOwner(ownerId, AccessMode.Signing, OwnershipStatus.Pending, noConflict);

            // Act
            var result = wallet.UpdateOwnershipStatus(newStatus);

            // Assert
            result.IsSuccess.ShouldBeTrue();
        }
    }

    #endregion

    #region Access Mode Update Tests

    [TestFixture]
    public class UpdateAccessModeTests : WalletTests
    {
        [Test]
        public void UpdateAccessMode_WithOwnedWallet_Should_UpdateModeAndRaiseEvent()
        {
            // Arrange
            var wallet = CreateWallet();
            var ownerId = AxonUserId.New();

            Func<WalletId, AccessMode, OwnershipStatus, Result<bool, Error>> noConflict =
                (_, _, _) => Result.Success<bool, Error>(false);

            wallet.LinkToOwner(ownerId, AccessMode.Signing, OwnershipStatus.Verified, noConflict);
            wallet.ClearDomainEvents();

            // Act
            var result = wallet.UpdateAccessMode(AccessMode.WatchOnly);

            // Assert
            result.IsSuccess.ShouldBeTrue();

            wallet.DomainEvents.ShouldHaveSingleItem()
                .ShouldBeOfType<WalletChangedEvent>()
                .ShouldSatisfyAllConditions(
                    e => e.WalletId.ShouldBe(wallet.Id),
                    e => e.PropertyChanged.ShouldBe("accessMode"),
                    e => e.OldValue.ShouldBe("Signing"),
                    e => e.NewValue.ShouldBe("WatchOnly")
                );
        }

        [Test]
        public void UpdateAccessMode_WithUnownedWallet_Should_ReturnFailure()
        {
            // Arrange
            var wallet = CreateWallet();

            // Act
            var result = wallet.UpdateAccessMode(AccessMode.Signing);

            // Assert
            result.IsFailure.ShouldBeTrue();
            result.Error.Type.ShouldBe(ErrorType.NotFound);
        }

        [Test]
        public void UpdateAccessMode_WithSameMode_Should_BeIdempotent()
        {
            // Arrange
            var wallet = CreateWallet();
            var ownerId = AxonUserId.New();

            Func<WalletId, AccessMode, OwnershipStatus, Result<bool, Error>> noConflict =
                (_, _, _) => Result.Success<bool, Error>(false);

            wallet.LinkToOwner(ownerId, AccessMode.Signing, OwnershipStatus.Verified, noConflict);
            wallet.ClearDomainEvents();

            // Act
            var result = wallet.UpdateAccessMode(AccessMode.Signing);

            // Assert
            result.IsSuccess.ShouldBeTrue();
            wallet.DomainEvents.ShouldBeEmpty(); // No event for idempotent operation
        }

        [Test]
        [TestCase(AccessMode.Signing)]
        [TestCase(AccessMode.WatchOnly)]
        public void UpdateAccessMode_Should_AcceptAllValidModes(AccessMode newMode)
        {
            // Arrange
            var wallet = CreateWallet();
            var ownerId = AxonUserId.New();

            Func<WalletId, AccessMode, OwnershipStatus, Result<bool, Error>> noConflict =
                (_, _, _) => Result.Success<bool, Error>(false);

            wallet.LinkToOwner(ownerId, AccessMode.WatchOnly, OwnershipStatus.Verified, noConflict);

            // Act
            var result = wallet.UpdateAccessMode(newMode);

            // Assert
            result.IsSuccess.ShouldBeTrue();
        }
    }

    #endregion

    #region Address Validation Tests

    [TestFixture]
    public class ValidateAndNormalizeAddressTests : WalletTests
    {
        [Test]
        [TestCase("")]
        [TestCase("   ")]
        [TestCase(null)]
        public void ValidateAndNormalizeAddress_WithEmptyAddress_Should_ReturnFailure(string? address)
        {
            // Act
            var result = Wallet.ValidateAndNormalizeAddress(address!, "ethereum");

            // Assert
            result.IsFailure.ShouldBeTrue();
            result.Error.Type.ShouldBe(ErrorType.Validation);
            result.Error.Code.ShouldBe("WALLET.ADDRESS.EMPTY");
        }

        [Test]
        [TestCase("0x742d35Cc6634C0532925a3b8D2aE39e7ec5B8e45", "ethereum", "0x742d35cc6634c0532925a3b8d2ae39e7ec5b8e45")]
        [TestCase("0X742D35CC6634C0532925A3B8D2AE39E7EC5B8E45", "ethereum", "0x742d35cc6634c0532925a3b8d2ae39e7ec5b8e45")]
        [TestCase("0x742d35Cc6634C0532925a3b8D2aE39e7ec5B8e45", "polygon", "0x742d35cc6634c0532925a3b8d2ae39e7ec5b8e45")]
        [TestCase("0x742d35Cc6634C0532925a3b8D2aE39e7ec5B8e45", "arbitrum", "0x742d35cc6634c0532925a3b8d2ae39e7ec5b8e45")]
        public void ValidateAndNormalizeAddress_WithValidEvmAddress_Should_NormalizeToLowercase(
            string address, string chainId, string expectedNormalized)
        {
            // Act
            var result = Wallet.ValidateAndNormalizeAddress(address, chainId);

            // Assert
            result.IsSuccess.ShouldBeTrue();
            result.Value.ShouldBe(expectedNormalized);
        }

        [Test]
        [TestCase("0x742d35Cc6634C0532925a3b8D2aE39e7ec5B8e4", "ethereum")] // Too short
        [TestCase("0x742d35Cc6634C0532925a3b8D2aE39e7ec5B8e456", "ethereum")] // Too long
        [TestCase("742d35Cc6634C0532925a3b8D2aE39e7ec5B8e45", "ethereum")] // Missing 0x
        [TestCase("0xG42d35Cc6634C0532925a3b8D2aE39e7ec5B8e45", "ethereum")] // Invalid hex
        public void ValidateAndNormalizeAddress_WithInvalidEvmAddress_Should_ReturnFailure(string address, string chainId)
        {
            // Act
            var result = Wallet.ValidateAndNormalizeAddress(address, chainId);

            // Assert
            result.IsFailure.ShouldBeTrue();
            result.Error.Type.ShouldBe(ErrorType.Validation);
        }

        [Test]
        [TestCase("9WzDXwBbmkg8ZTbNMqUxvQRAyrZzDsGYdLVL9zYtAWWM", "solana")] // Valid base58
        [TestCase("2WDq7wSs9zYrpx2kbHDA4RUTRch2CCTP6ZWaH4GNHnR", "solana")] // Valid base58, shorter
        public void ValidateAndNormalizeAddress_WithValidSolanaAddress_Should_ReturnSuccess(string address, string chainId)
        {
            // Act
            var result = Wallet.ValidateAndNormalizeAddress(address, chainId);

            // Assert
            result.IsSuccess.ShouldBeTrue();
            result.Value.ShouldBe(address); // Solana addresses are not normalized
        }

        [Test]
        [TestCase("9WzDXwBbmkg8ZTbNMqUxvQRAyrZzDsGYdLVL9zYtAWWMTooLong", "solana")] // Too long
        [TestCase("9WzDXwBbmkg8ZTbNMqUxvQRA", "solana")] // Too short
        [TestCase("9WzDXwBbmkg8ZTbNMqUxvQRAyrZzDsGYdLVL9zYtAWW0", "solana")] // Contains 0 (invalid base58)
        [TestCase("9WzDXwBbmkg8ZTbNMqUxvQRAyrZzDsGYdLVL9zYtAWWO", "solana")] // Contains O (invalid base58)
        public void ValidateAndNormalizeAddress_WithInvalidSolanaAddress_Should_ReturnFailure(string address, string chainId)
        {
            // Act
            var result = Wallet.ValidateAndNormalizeAddress(address, chainId);

            // Assert
            result.IsFailure.ShouldBeTrue();
            result.Error.Type.ShouldBe(ErrorType.Validation);
        }

        [Test]
        [TestCase("bc1qw508d6qejxtdg4y5r3zarvary0c5xw7kv8f3t4", "bitcoin")] // Valid Bitcoin address
        [TestCase("cosmos1depk54cuajgkzea6zpgkq36tnjwdzv4afc3d27", "cosmos")] // Valid Cosmos address
        public void ValidateAndNormalizeAddress_WithUnknownChain_Should_UseGenericValidation(string address, string chainId)
        {
            // Act
            var result = Wallet.ValidateAndNormalizeAddress(address, chainId);

            // Assert
            result.IsSuccess.ShouldBeTrue();
            result.Value.ShouldBe(address);
        }

        [Test]
        [TestCase("abc", "unknown")] // Too short
        [TestCase("abbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb", "unknown")] // Too long (200+ chars)
        public void ValidateAndNormalizeAddress_WithGenericInvalidLength_Should_ReturnFailure(string address, string chainId)
        {
            // Act
            var result = Wallet.ValidateAndNormalizeAddress(address, chainId);

            // Assert
            result.IsFailure.ShouldBeTrue();
            result.Error.Type.ShouldBe(ErrorType.Validation);
            result.Error.Code.ShouldBe("WALLET.ADDRESS.INVALID_LENGTH");
        }

        [Test]
        public void ValidateAndNormalizeAddress_Should_TrimWhitespace()
        {
            // Arrange
            var address = "  0x742d35Cc6634C0532925a3b8D2aE39e7ec5B8e45  ";

            // Act
            var result = Wallet.ValidateAndNormalizeAddress(address, "ethereum");

            // Assert
            result.IsSuccess.ShouldBeTrue();
            result.Value.ShouldBe("0x742d35cc6634c0532925a3b8d2ae39e7ec5b8e45");
        }
    }

    #endregion
}