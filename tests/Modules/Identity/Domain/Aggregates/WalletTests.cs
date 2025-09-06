using Axon.Modules.Identity.Domain.Aggregates.Wallet;
using Axon.Modules.Identity.Domain.Errors;
using Axon.Modules.Identity.Domain.Events;
using Axon.Modules.Identity.Domain.ValueObjects;
using Axon.Modules.Identity.Domain.Tests.TestData;
using BuildingBlocks.Primitives.Ids;

namespace Axon.Modules.Identity.Domain.Tests.Aggregates;

[TestFixture]
public class WalletTests
{
    private readonly TimeProvider _timeProvider = TimeProvider.System;

    [TestFixture]
    public class RegisterAsync : WalletTests
    {
        [Test]
        public async Task WithValidSolanaAddress_ShouldSucceed()
        {
            // Arrange
            var chainId = Builders.SolanaChain;
            var rawAddress = TestConstants.ValidSolanaAddress;
            var firstSeenAt = DateTimeOffset.UtcNow;
            var provider = Builders.DynamicProvider;
            var displayName = "Test Wallet";

            // Mock wallet existence check (no existing wallet)
            ValueTask<bool> WalletExistsCheck(ChainId chain, Address address) => ValueTask.FromResult(false);

            // Act
            var result = await Wallet.RegisterAsync(
                chainId, rawAddress, firstSeenAt, WalletExistsCheck, provider, displayName);

            // Should
            result.IsSuccess.ShouldBeTrue();
            var wallet = result.Value;
            wallet.Chain.ShouldBe(chainId);
            wallet.Address.Value.ShouldBe(rawAddress); // Should be normalized
            wallet.FirstSeenAt.ShouldBe(firstSeenAt);
            wallet.LastSeenAt.ShouldBe(firstSeenAt);
            wallet.Profile.Provider.ShouldBe(provider);
            wallet.Profile.DisplayName.ShouldBe(displayName);
            wallet.Tags.ShouldBeEmpty();
            wallet.IsDeleted.ShouldBeFalse();
        }

        [Test]
        public async Task WithValidEthereumAddress_ShouldSucceed()
        {
            // Arrange
            var chainId = Builders.EthereumChain;
            var rawAddress = TestConstants.ValidEthAddress;
            var firstSeenAt = DateTimeOffset.UtcNow;

            ValueTask<bool> WalletExistsCheck(ChainId chain, Address address) => ValueTask.FromResult(false);

            // Act
            var result = await Wallet.RegisterAsync(
                chainId, rawAddress, firstSeenAt, WalletExistsCheck);

            // Should
            result.IsSuccess.ShouldBeTrue();
            var wallet = result.Value;
            wallet.Chain.ShouldBe(chainId);
            wallet.Address.Value.ShouldBe(rawAddress.ToLowerInvariant()); // Ethereum addresses normalized to lowercase
        }

        [Test]
        public async Task ShouldRaiseWalletRegisteredEvent()
        {
            // Arrange
            var chainId = Builders.SolanaChain;
            var rawAddress = TestConstants.ValidSolanaAddress;
            var firstSeenAt = DateTimeOffset.UtcNow;

            ValueTask<bool> WalletExistsCheck(ChainId chain, Address address) => ValueTask.FromResult(false);

            // Act
            var result = await Wallet.RegisterAsync(
                chainId, rawAddress, firstSeenAt, WalletExistsCheck);

            // Should
            result.IsSuccess.ShouldBeTrue();
            var wallet = result.Value;
            var events = wallet.DomainEvents;
            events.ShouldHaveCount(1);
            var registeredEvent = events.First().ShouldBeOfType<WalletRegisteredEvent>().Subject;
            registeredEvent.WalletId.ShouldBe(wallet.Id.Value.ToString());
            registeredEvent.ChainId.ShouldBe(chainId.Value);
            registeredEvent.Address.ShouldBe(wallet.Address.Value);
            registeredEvent.FirstSeenAt.ShouldBe(firstSeenAt);
        }

        [Test]
        public async Task WithDuplicateWallet_ShouldFail()
        {
            // Arrange
            var chainId = Builders.SolanaChain;
            var rawAddress = TestConstants.ValidSolanaAddress;
            var firstSeenAt = DateTimeOffset.UtcNow;

            // Mock wallet existence check (wallet already exists)
            ValueTask<bool> WalletExistsCheck(ChainId chain, Address address) => ValueTask.FromResult(true);

            // Act
            var result = await Wallet.RegisterAsync(
                chainId, rawAddress, firstSeenAt, WalletExistsCheck);

            // Should
            result.IsFailure.ShouldBeTrue();
            result.Error.Code.ShouldContain("WALLET_ALREADY_EXISTS");
        }

        [Test]
        public async Task WithInvalidAddress_ShouldFail()
        {
            // Arrange
            var chainId = Builders.SolanaChain;
            var invalidAddress = "invalid-address-format";
            var firstSeenAt = DateTimeOffset.UtcNow;

            ValueTask<bool> WalletExistsCheck(ChainId chain, Address address) => ValueTask.FromResult(false);

            // Act
            var result = await Wallet.RegisterAsync(
                chainId, invalidAddress, firstSeenAt, WalletExistsCheck);

            // Should
            result.IsFailure.ShouldBeTrue();
            result.Error.Code.ShouldContain("INVALID");
        }

        [Test]
        public async Task WithDefaultProvider_ShouldUseUnknown()
        {
            // Arrange
            var chainId = Builders.SolanaChain;
            var rawAddress = TestConstants.ValidSolanaAddress;
            var firstSeenAt = DateTimeOffset.UtcNow;

            ValueTask<bool> WalletExistsCheck(ChainId chain, Address address) => ValueTask.FromResult(false);

            // Act
            var result = await Wallet.RegisterAsync(
                chainId, rawAddress, firstSeenAt, WalletExistsCheck);

            // Should
            result.IsSuccess.ShouldBeTrue();
            var wallet = result.Value;
            wallet.Profile.Provider.Value.ShouldBe("unknown");
        }
    }

    [TestFixture]
    public class TouchSeen : WalletTests
    {
        [Test]
        public async Task WithLaterTimestamp_ShouldUpdateLastSeen()
        {
            // Arrange
            var wallet = await CreateTestWallet();
            var originalLastSeen = wallet.LastSeenAt;
            var newObservationTime = originalLastSeen.AddHours(1);

            // Act
            var result = wallet.TouchSeen(newObservationTime);

            // Should
            result.IsSuccess.ShouldBeTrue();
            wallet.LastSeenAt.ShouldBe(newObservationTime);
            wallet.LastSeenAt.ShouldBeGreaterThan(originalLastSeen);
        }

        [Test]
        public async Task WithEarlierTimestamp_ShouldIgnoreUpdate()
        {
            // Arrange
            var wallet = await CreateTestWallet();
            var originalLastSeen = wallet.LastSeenAt;
            var earlierTime = originalLastSeen.AddHours(-1);

            // Act
            var result = wallet.TouchSeen(earlierTime);

            // Should
            result.IsSuccess.ShouldBeTrue();
            wallet.LastSeenAt.ShouldBe(originalLastSeen); // Should remain unchanged
        }

        [Test]
        public async Task ShouldRaiseWalletTouchedEvent_WhenTimestampChanges()
        {
            // Arrange
            var wallet = await CreateTestWallet();
            wallet.ClearDomainEvents(); // Clear registration event
            var newObservationTime = wallet.LastSeenAt.AddMinutes(30);

            // Act
            var result = wallet.TouchSeen(newObservationTime);

            // Should
            result.IsSuccess.ShouldBeTrue();
            var events = wallet.DomainEvents;
            events.ShouldHaveCount(1);
            var touchedEvent = events.First().ShouldBeOfType<WalletTouchedEvent>().Subject;
            touchedEvent.WalletId.ShouldBe(wallet.Id);
            touchedEvent.LastSeenAt.ShouldBe(newObservationTime);
        }

        [Test]
        public async Task ShouldNotRaiseEvent_WhenTimestampDoesNotChange()
        {
            // Arrange
            var wallet = await CreateTestWallet();
            wallet.ClearDomainEvents();
            var sameTime = wallet.LastSeenAt;

            // Act
            var result = wallet.TouchSeen(sameTime);

            // Should
            result.IsSuccess.ShouldBeTrue();
            wallet.DomainEvents.ShouldBeEmpty();
        }
    }

    [TestFixture]
    public class UpdateProfile : WalletTests
    {
        [Test]
        public async Task WithValidData_ShouldSucceed()
        {
            // Arrange
            var wallet = await CreateTestWallet();
            var newProvider = Builders.SiwsProvider;
            var newDisplayName = "Updated Wallet Name";

            // Act
            var result = wallet.UpdateProfile(newProvider, newDisplayName);

            // Should
            result.IsSuccess.ShouldBeTrue();
            wallet.Profile.Provider.ShouldBe(newProvider);
            wallet.Profile.DisplayName.ShouldBe(newDisplayName);
        }

        [Test]
        public async Task ShouldRaiseWalletMetaUpdatedEvent()
        {
            // Arrange
            var wallet = await CreateTestWallet();
            wallet.ClearDomainEvents();
            var newProvider = Builders.SiwsProvider;

            // Act
            var result = wallet.UpdateProfile(newProvider, "New Name");

            // Should
            result.IsSuccess.ShouldBeTrue();
            var events = wallet.DomainEvents;
            events.ShouldHaveCount(1);
            var metaEvent = events.First().ShouldBeOfType<WalletMetaUpdatedEvent>().Subject;
            metaEvent.WalletId.ShouldBe(wallet.Id);
            metaEvent.UpdatedFields.ShouldContain("provider");
            metaEvent.UpdatedFields.ShouldContain("displayName");
        }
    }

    [TestFixture]
    public class AddTag : WalletTests
    {
        [Test]
        public async Task WithValidTag_ShouldSucceed()
        {
            // Arrange
            var wallet = await CreateTestWallet();
            var tagValue = "defi-wallet";

            // Act
            var result = wallet.AddTag(tagValue);

            // Should
            result.IsSuccess.ShouldBeTrue();
            wallet.Tags.ShouldContain(tag => tag.Value == tagValue);
        }

        [Test]
        public async Task ShouldRaiseWalletTaggedEvent()
        {
            // Arrange
            var wallet = await CreateTestWallet();
            wallet.ClearDomainEvents();
            var tagValue = "nft-wallet";

            // Act
            var result = wallet.AddTag(tagValue);

            // Should
            result.IsSuccess.ShouldBeTrue();
            var events = wallet.DomainEvents;
            events.ShouldHaveCount(1);
            var taggedEvent = events.First().ShouldBeOfType<WalletTaggedEvent>().Subject;
            taggedEvent.WalletId.ShouldBe(wallet.Id);
            taggedEvent.TagValue.ShouldBe(tagValue);
        }

        [Test]
        public async Task WithDuplicateTag_ShouldBeIdempotent()
        {
            // Arrange
            var wallet = await CreateTestWallet();
            var tagValue = "existing-tag";
            
            // Add tag first time
            var firstResult = wallet.AddTag(tagValue);
            firstResult.IsSuccess.ShouldBeTrue();
            wallet.ClearDomainEvents();

            // Act - Add same tag again
            var result = wallet.AddTag(tagValue);

            // Should
            result.IsSuccess.ShouldBeTrue();
            wallet.Tags.Count(tag => tag.Value == tagValue).ShouldBe(1);
            wallet.DomainEvents.ShouldBeEmpty(); // No event for duplicate
        }

        [Test]
        public async Task WithTagValueObject_ShouldSucceed()
        {
            // Arrange
            var wallet = await CreateTestWallet();
            var tag = Builders.TestTag;

            // Act
            var result = wallet.AddTag(tag);

            // Should
            result.IsSuccess.ShouldBeTrue();
            wallet.Tags.ShouldContain(tag);
        }
    }

    [TestFixture]
    public class RemoveTag : WalletTests
    {
        [Test]
        public async Task WithExistingTag_ShouldSucceed()
        {
            // Arrange
            var wallet = await CreateTestWallet();
            var tagValue = "removable-tag";
            
            // Add tag first
            wallet.AddTag(tagValue);
            wallet.ClearDomainEvents();

            // Act
            var result = wallet.RemoveTag(tagValue);

            // Should
            result.IsSuccess.ShouldBeTrue();
            wallet.Tags.ShouldNotContain(tag => tag.Value == tagValue);
        }

        [Test]
        public async Task ShouldRaiseWalletUntaggedEvent()
        {
            // Arrange
            var wallet = await CreateTestWallet();
            var tagValue = "removable-tag";
            
            wallet.AddTag(tagValue);
            wallet.ClearDomainEvents();

            // Act
            var result = wallet.RemoveTag(tagValue);

            // Should
            result.IsSuccess.ShouldBeTrue();
            var events = wallet.DomainEvents;
            events.ShouldHaveCount(1);
            var untaggedEvent = events.First().ShouldBeOfType<WalletUntaggedEvent>().Subject;
            untaggedEvent.WalletId.ShouldBe(wallet.Id);
            untaggedEvent.TagValue.ShouldBe(tagValue);
        }

        [Test]
        public async Task WithNonexistentTag_ShouldBeIdempotent()
        {
            // Arrange
            var wallet = await CreateTestWallet();
            var nonexistentTag = "nonexistent-tag";

            // Act
            var result = wallet.RemoveTag(nonexistentTag);

            // Should
            result.IsSuccess.ShouldBeTrue();
            wallet.DomainEvents.ShouldBeEmpty(); // No event if tag wasn't present
        }
    }

    [TestFixture]
    public class SoftDelete : WalletTests
    {
        [Test]
        public async Task ActiveWallet_ShouldSucceed()
        {
            // Arrange
            var wallet = await CreateTestWallet();

            // Act
            var result = wallet.SoftDelete(_timeProvider);

            // Should
            result.IsSuccess.ShouldBeTrue();
            wallet.IsDeleted.ShouldBeTrue();
        }

        [Test]
        public async Task ShouldRaiseWalletSoftDeletedEvent()
        {
            // Arrange
            var wallet = await CreateTestWallet();
            wallet.ClearDomainEvents();

            // Act
            var result = wallet.SoftDelete(_timeProvider);

            // Should
            result.IsSuccess.ShouldBeTrue();
            var events = wallet.DomainEvents;
            events.ShouldHaveCount(1);
            var deletedEvent = events.First().ShouldBeOfType<WalletSoftDeletedEvent>().Subject;
            deletedEvent.WalletId.ShouldBe(wallet.Id);
        }

        [Test]
        public async Task AlreadyDeleted_ShouldBeIdempotent()
        {
            // Arrange
            var wallet = await CreateTestWallet();
            wallet.SoftDelete(_timeProvider);
            wallet.ClearDomainEvents();

            // Act
            var result = wallet.SoftDelete(_timeProvider);

            // Should
            result.IsSuccess.ShouldBeTrue();
            wallet.DomainEvents.ShouldBeEmpty(); // No event for already deleted
        }
    }

    [TestFixture]
    public class Restore : WalletTests
    {
        [Test]
        public async Task DeletedWallet_ShouldSucceed()
        {
            // Arrange
            var wallet = await CreateTestWallet();
            wallet.SoftDelete(_timeProvider);
            wallet.ClearDomainEvents();

            // Act
            var result = wallet.Restore(_timeProvider);

            // Should
            result.IsSuccess.ShouldBeTrue();
            wallet.IsDeleted.ShouldBeFalse();
        }

        [Test]
        public async Task ShouldRaiseWalletRestoredEvent()
        {
            // Arrange
            var wallet = await CreateTestWallet();
            wallet.SoftDelete(_timeProvider);
            wallet.ClearDomainEvents();

            // Act
            var result = wallet.Restore(_timeProvider);

            // Should
            result.IsSuccess.ShouldBeTrue();
            var events = wallet.DomainEvents;
            events.ShouldHaveCount(1);
            var restoredEvent = events.First().ShouldBeOfType<WalletRestoredEvent>().Subject;
            restoredEvent.WalletId.ShouldBe(wallet.Id);
        }

        [Test]
        public async Task ActiveWallet_ShouldBeIdempotent()
        {
            // Arrange
            var wallet = await CreateTestWallet();
            wallet.ClearDomainEvents();

            // Act
            var result = wallet.Restore(_timeProvider);

            // Should
            result.IsSuccess.ShouldBeTrue();
            wallet.DomainEvents.ShouldBeEmpty(); // No event for already active
        }
    }

    // Helper method to create test wallets
    private async Task<Wallet> CreateTestWallet()
    {
        var chainId = Builders.SolanaChain;
        var rawAddress = TestConstants.ValidSolanaAddress;
        var firstSeenAt = DateTimeOffset.UtcNow;

        ValueTask<bool> WalletExistsCheck(ChainId chain, Address address) => ValueTask.FromResult(false);

        var result = await Wallet.RegisterAsync(
            chainId, rawAddress, firstSeenAt, WalletExistsCheck);
        
        result.IsSuccess.ShouldBeTrue();
        return result.Value;
    }
}